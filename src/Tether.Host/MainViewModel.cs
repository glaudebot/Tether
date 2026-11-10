using System;
using System.ComponentModel;
using System.Threading;
using Tether.Automation;
using Tether.Core;
using Tether.Core.Config;

namespace Tether.Host;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TetherRuntime runtime;
    private readonly CancellationTokenSource cts = new();
    private string statusText = "시작 중...";

    public MainViewModel(string configPath)
    {
        runtime = new TetherRuntime(TetherConfig.Load(configPath));
        Router.PacketChanged += p =>
            StatusText = $"{DateTime.Now:HH:mm:ss}  #{p.Seq:D6} {p.From}→{p.To} {p.Status}{(p.Error is null ? "" : $" ({p.Error})")}";
        runtime.Start(cts.Token, m => StatusText = m);
        StatusText = $"감시 중: {runtime.OutboxDir}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Router Router => runtime.Router;
    public PacketStore Store => runtime.Store;
    public System.Collections.Generic.IReadOnlyList<AppConfig> Apps => runtime.Config.Apps;

    public string StatusText
    {
        get => statusText;
        set { statusText = value; PropertyChanged?.Invoke(this, new(nameof(StatusText))); }
    }

    public void Dispose()
    {
        cts.Cancel();
        runtime.Dispose();
    }
}
