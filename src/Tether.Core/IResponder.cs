namespace Tether.Core;

/// <summary>An app that receives a prompt and produces a reply (e.g. Gemini).</summary>
public interface IResponder
{
    string AppId { get; }
    Task DeliverAsync(string prompt, CancellationToken ct);
    Task<AppReply> CollectReplyAsync(CancellationToken ct);
}
