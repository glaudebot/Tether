namespace Tether.Core;

/// <summary>An app that receives a prompt and produces a reply (e.g. Gemini).</summary>
public interface IResponder
{
    string AppId { get; }

    /// <summary>Pastes only <see cref="AppRequest.Body"/>; Thread decides whether a new conversation is opened first.</summary>
    Task DeliverAsync(AppRequest request, CancellationToken ct);

    /// <summary>Waits for the answer to the request just delivered; Reply decides whether to wait for an image.</summary>
    Task<AppReply> CollectReplyAsync(AppRequest request, CancellationToken ct);
}
