namespace Tether.Core;

public class Router(PacketStore store, IEnumerable<IResponder> responders, IEnumerable<INotifier> notifiers, int maxPacketsPerRun = 20)
{
    private readonly Dictionary<string, IResponder> responders = responders.ToDictionary(r => r.AppId);
    private readonly Dictionary<string, INotifier> notifiers = notifiers.ToDictionary(n => n.AppId);
    private readonly Dictionary<string, SemaphoreSlim> locks = responders.ToDictionary(r => r.AppId, _ => new SemaphoreSlim(1));
    private int submitted;

    public event Action<PacketInfo>? PacketChanged;

    /// <summary>Runs one request end to end. Never throws, except on cancellation.</summary>
    public async Task<PacketInfo> SubmitAsync(string from, string rawContent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        AppRequest? request = null;
        string? error = null;
        try { request = RequestParser.Parse(from, rawContent); }
        catch (RequestFormatException e) { error = e.Message; }

        // Record every request as a packet first, so failures are visible too.
        var packet = Changed(store.Create(from, request?.To ?? "?", rawContent));

        if (error is null && !responders.ContainsKey(request!.To)) error = $"알 수 없는 앱: {request.To}";
        if (error is null && Interlocked.Increment(ref submitted) > maxPacketsPerRun) error = $"패킷 상한 초과 ({maxPacketsPerRun})";
        if (error is not null) return await FailAsync(packet, error, ct);

        var gate = locks[request!.To];
        await gate.WaitAsync(ct);
        try
        {
            var responder = responders[request.To];
            Set(packet.Seq, PacketStatus.Delivering);
            await responder.DeliverAsync(request.Body, ct);
            Set(packet.Seq, PacketStatus.Generating);
            var reply = await responder.CollectReplyAsync(ct);
            store.SaveReply(packet.Seq, reply);
            packet = Set(packet.Seq, PacketStatus.Replied);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Set(packet.Seq, PacketStatus.Failed, "취소됨");
            throw;
        }
        catch (Exception e)
        {
            return await FailAsync(packet, e.Message, ct);
        }
        finally { gate.Release(); }

        if (!notifiers.ContainsKey(from)) return packet;

        var files = packet.ReplyFiles.Count == 0 ? "응답 파일 없음" : string.Join(", ", packet.ReplyFiles);
        var message = $"[Tether] #{packet.Seq:D6} {packet.To} 응답 도착: {store.GetPacketDir(packet.Seq)} ({files})";
        var notifyError = await TryNotifyAsync(from, message, ct);
        return notifyError is null
            ? Set(packet.Seq, PacketStatus.Notified)
            : Set(packet.Seq, PacketStatus.Replied, $"알림 실패: {notifyError}");
    }

    private async Task<PacketInfo> FailAsync(PacketInfo packet, string error, CancellationToken ct)
    {
        var failed = Set(packet.Seq, PacketStatus.Failed, error);
        var notifyError = await TryNotifyAsync(packet.From, $"[Tether] #{packet.Seq:D6} 실패: {error}", ct);
        return notifyError is null ? failed : Set(packet.Seq, PacketStatus.Failed, $"{error} / 알림 실패: {notifyError}");
    }

    /// <returns>null on success or when the requester has no notifier; otherwise the error message.</returns>
    private async Task<string?> TryNotifyAsync(string appId, string message, CancellationToken ct)
    {
        if (!notifiers.TryGetValue(appId, out var notifier)) return null;
        try { await notifier.NotifyAsync(message, ct); return null; }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { return e.Message; }
    }

    private PacketInfo Set(long seq, PacketStatus status, string? error = null) => Changed(store.SetStatus(seq, status, error));

    private PacketInfo Changed(PacketInfo p)
    {
        PacketChanged?.Invoke(p);
        return p;
    }
}
