using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Tether.Core;

namespace Tether.Automation;

/// <summary>
/// Watches exchange/outbox/{from}/*.md. Each file (including ones already there at start) is read once
/// it is fully written, deleted, then handed to onRequest.
/// </summary>
public sealed class OutboxWatcher(string exchangeRoot, Func<string, string, Task> onRequest, Action<string>? log = null) : IDisposable
{
    private readonly string outbox = Path.GetFullPath(Path.Combine(exchangeRoot, "outbox"));
    private readonly ConcurrentDictionary<string, byte> inFlight = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? watcher;

    public void Start()
    {
        Directory.CreateDirectory(outbox);
        watcher = new FileSystemWatcher(outbox, "*.md") { IncludeSubdirectories = true };
        watcher.Created += (_, e) => Handle(e.FullPath);
        watcher.Renamed += (_, e) => Handle(e.FullPath);
        watcher.EnableRaisingEvents = true;

        foreach (var file in Directory.EnumerateFiles(outbox, "*.md", SearchOption.AllDirectories)) Handle(file);
    }

    private void Handle(string path)
    {
        // only outbox/{from}/x.md, one level deep
        var from = Path.GetFileName(Path.GetDirectoryName(path));
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), outbox, StringComparison.OrdinalIgnoreCase)
            || !inFlight.TryAdd(path, 0)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var content = await RequestFile.ReadWhenReadyAsync(path);
                File.Delete(path);
                await onRequest(from!.ToLowerInvariant(), content);
            }
            catch (Exception e) { log?.Invoke($"요청 처리 실패 {path}: {e.Message}"); }
            finally { inFlight.TryRemove(path, out _); }
        });
    }

    public void Dispose() => watcher?.Dispose();
}
