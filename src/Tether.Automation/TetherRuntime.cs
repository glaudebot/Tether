using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Tether.Core;
using Tether.Core.Config;

namespace Tether.Automation;

/// <summary>Wires config → adapters → Router → OutboxWatcher. Shared by the CLI and the Host.</summary>
public sealed class TetherRuntime : IDisposable
{
    private OutboxWatcher? watcher;

    public TetherRuntime(TetherConfig config)
    {
        Config = config;
        Responders = config.Apps.Where(a => a.Role == "responder").Select(CreateResponder).ToList();
        Requesters = config.Apps.Where(a => a.Role == "requester").Select(a => a.Id).ToList();
        Notifiers = config.Apps.Where(a => a.Role == "requester").Select(CreateNotifier).OfType<INotifier>().ToList();
        Store = new PacketStore(config.ExchangeRoot, TimeProvider.System);
        Router = new Router(Store, Responders, Notifiers, config.MaxPacketsPerRun);
    }

    public TetherConfig Config { get; }
    public IReadOnlyList<IResponder> Responders { get; }
    public IReadOnlyList<string> Requesters { get; }
    public IReadOnlyList<INotifier> Notifiers { get; }
    public PacketStore Store { get; }
    public Router Router { get; }
    public string OutboxDir => Path.Combine(Config.ExchangeRoot, "outbox");

    /// <summary>Writes PROTOCOL.md, creates each requester's outbox and starts watching.</summary>
    public void Start(CancellationToken ct, Action<string>? log = null)
    {
        ProtocolDoc.Write(Config.ExchangeRoot, Responders.Select(r => r.AppId));
        foreach (var id in Requesters) Directory.CreateDirectory(Path.Combine(OutboxDir, id));
        watcher = new OutboxWatcher(Config.ExchangeRoot, (from, content) => Router.SubmitAsync(from, content, ct), log);
        watcher.Start();
    }

    public void Dispose() => watcher?.Dispose();

    private static IResponder CreateResponder(AppConfig app) => app.Id switch
    {
        "gemini" => new GeminiResponder(app),
        _ => throw new InvalidDataException($"지원하지 않는 responder: {app.Id}"),
    };

    /// <returns>null for a requester that polls packets/&lt;seq&gt;/status.json itself instead of being notified.</returns>
    private static INotifier? CreateNotifier(AppConfig app) => app.Id switch
    {
        "claude" => new ClaudeNotifier(app),
        "claude-code" => null, // never paste into a Claude Code conversation

        _ => throw new InvalidDataException($"지원하지 않는 requester: {app.Id}"),
    };
}
