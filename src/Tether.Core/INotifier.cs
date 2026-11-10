namespace Tether.Core;

/// <summary>An app that is told when its request finished (e.g. Claude).</summary>
public interface INotifier
{
    string AppId { get; }
    Task NotifyAsync(string message, CancellationToken ct);
}
