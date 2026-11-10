using Tether.Core;
using static Tether.Core.PacketStatus;

namespace Tether.Core.Tests;

public sealed class RouterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"tether-{Guid.NewGuid():N}");
    private readonly PacketStore store;
    private readonly FakeResponder gemini = new("gemini");
    private readonly FakeNotifier claude = new("claude");
    private readonly List<PacketStatus> changedStatuses = [];
    private readonly string tmpPng;

    public RouterTests()
    {
        store = new PacketStore(root, TimeProvider.System);
        tmpPng = Path.Combine(root, "gen.png");
        File.WriteAllText(tmpPng, "png");
        gemini.Reply = new AppReply(null, [tmpPng]);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private Router NewRouter(int max = 20)
    {
        var r = new Router(store, [gemini], [claude], max);
        r.PacketChanged += p => { lock (changedStatuses) changedStatuses.Add(p.Status); };
        return r;
    }

    [Fact]
    public async Task Submit_Valid_FlowsToNotified()
    {
        var p = await NewRouter().SubmitAsync("claude", "to: gemini\n사과", default);

        Assert.Equal(Notified, p.Status);
        Assert.Equal([Queued, Delivering, Generating, Replied, Notified], changedStatuses);
        Assert.Equal("사과", gemini.Prompts.Single());
        Assert.Equal($"[Tether] #000001 gemini 응답 도착: {store.GetPacketDir(1)} (reply-1.png)", claude.Messages.Single());
        Assert.Equal(Notified, store.Get(1).Status);
    }

    [Fact]
    public async Task Submit_BadFormat_FailsAndNotifies()
    {
        var p = await NewRouter().SubmitAsync("claude", "그냥 본문", default);

        Assert.Equal(Failed, p.Status);
        Assert.Equal("?", p.To);
        Assert.Equal("그냥 본문", File.ReadAllText(Path.Combine(store.GetPacketDir(1), "request.md")));
        Assert.Equal("[Tether] #000001 실패: 첫 줄은 'to: <앱id>' 형식이어야 합니다", claude.Messages.Single());
    }

    [Fact]
    public async Task Submit_UnknownApp_Fails()
    {
        var p = await NewRouter().SubmitAsync("claude", "to: dalle\nx", default);
        Assert.Equal(Failed, p.Status);
        Assert.Equal("알 수 없는 앱: dalle", p.Error);
        Assert.Equal("[Tether] #000001 실패: 알 수 없는 앱: dalle", claude.Messages.Single());
    }

    [Fact]
    public async Task Submit_ResponderThrows_Fails()
    {
        gemini.CollectError = new InvalidOperationException("gemini 창을 찾을 수 없습니다");

        var p = await NewRouter().SubmitAsync("claude", "to: gemini\nx", default);

        Assert.Equal(Failed, p.Status);
        Assert.Equal("gemini 창을 찾을 수 없습니다", p.Error);
        Assert.Equal("[Tether] #000001 실패: gemini 창을 찾을 수 없습니다", claude.Messages.Single());
    }

    [Fact]
    public async Task Submit_OverLimit_Fails()
    {
        var router = NewRouter(max: 1);
        await router.SubmitAsync("claude", "to: gemini\n하나", default);

        var p = await router.SubmitAsync("claude", "to: gemini\n둘", default);

        Assert.Equal(Failed, p.Status);
        Assert.Equal("패킷 상한 초과 (1)", p.Error);
        Assert.Single(gemini.Prompts);
    }

    [Fact]
    public async Task Submit_NotifierThrows_StaysReplied()
    {
        claude.Error = new InvalidOperationException("x");

        var p = await NewRouter().SubmitAsync("claude", "to: gemini\nx", default);

        Assert.Equal(Replied, p.Status);
        Assert.Equal("알림 실패: x", p.Error);
        Assert.True(File.Exists(Path.Combine(store.GetPacketDir(1), "reply-1.png")));
    }

    [Fact]
    public async Task Submit_FailureNoticeThrows_RecordsBothErrors()
    {
        claude.Error = new InvalidOperationException("입력창 사용 중");

        var p = await NewRouter().SubmitAsync("claude", "그냥 본문", default);

        Assert.Equal(Failed, p.Status);
        Assert.Equal("첫 줄은 'to: <앱id>' 형식이어야 합니다 / 알림 실패: 입력창 사용 중", p.Error);
    }

    [Fact]
    public async Task Submit_NoNotifierForRequester_EndsWithoutNotice()
    {
        var p = await NewRouter().SubmitAsync("codex", "to: gemini\nx", default);
        Assert.Equal(Replied, p.Status);
        Assert.Empty(claude.Messages);
    }

    [Fact]
    public async Task Submit_SameTarget_IsSerialized()
    {
        gemini.Delay = TimeSpan.FromMilliseconds(100);
        var router = NewRouter();

        await Task.WhenAll(
            router.SubmitAsync("claude", "to: gemini\na", default),
            router.SubmitAsync("claude", "to: gemini\nb", default));

        Assert.Equal(1, gemini.MaxConcurrent);
        Assert.Equal(2, gemini.Prompts.Count);
    }

    [Fact]
    public async Task Submit_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewRouter().SubmitAsync("claude", "to: gemini\nx", cts.Token));
    }
}

internal sealed class FakeResponder(string appId) : IResponder
{
    private int running;
    public string AppId => appId;
    public List<string> Prompts { get; } = [];
    public AppReply Reply { get; set; } = new(null, []);
    public Exception? CollectError { get; set; }
    public TimeSpan Delay { get; set; }
    public int MaxConcurrent { get; private set; }

    public async Task DeliverAsync(string prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = Interlocked.Increment(ref running);
        lock (Prompts) { Prompts.Add(prompt); MaxConcurrent = Math.Max(MaxConcurrent, now); }
        await Task.Delay(Delay, ct);
    }

    public Task<AppReply> CollectReplyAsync(CancellationToken ct)
    {
        Interlocked.Decrement(ref running);
        return CollectError is null ? Task.FromResult(Reply) : Task.FromException<AppReply>(CollectError);
    }
}

internal sealed class FakeNotifier(string appId) : INotifier
{
    public string AppId => appId;
    public List<string> Messages { get; } = [];
    public Exception? Error { get; set; }

    public Task NotifyAsync(string message, CancellationToken ct)
    {
        if (Error is not null) return Task.FromException(Error);
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
