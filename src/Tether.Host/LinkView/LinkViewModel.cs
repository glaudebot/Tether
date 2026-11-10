using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Tether.Core;

namespace Tether.Host.LinkView;

/// <summary>Timeline (newest first) + which app sits in each slot. All members are used on the UI thread.</summary>
public sealed class LinkViewModel
{
    private readonly string?[] slotApps = new string?[2];

    public LinkViewModel(PacketStore store, Router router, Dispatcher ui)
    {
        router.PacketChanged += p => ui.BeginInvoke(() => Apply(p));
        foreach (var p in store.List())
            if (!Items.Any(i => i.Info.Seq == p.Seq)) Items.Insert(0, new PacketItem(p));
    }

    public ObservableCollection<PacketItem> Items { get; } = [];

    /// <summary>Raised on the UI thread after a packet's row is updated.</summary>
    public event Action<PacketInfo>? Changed;

    /// <summary>Raised when a slot gets or loses an app.</summary>
    public event Action? SlotsChanged;

    public string? SlotApp(int slot) => slotApps[slot];

    public void SetSlot(int slot, string? appId)
    {
        slotApps[slot] = appId;
        SlotsChanged?.Invoke();
    }

    /// <summary>Node index (0 = A, 1 = B) of the requester and target; undocked apps fall back to A → B.</summary>
    public (int From, int To) Nodes(PacketInfo p)
    {
        var from = Array.IndexOf(slotApps, p.From);
        var to = Array.IndexOf(slotApps, p.To);
        if (from < 0 && to < 0) return (0, 1);
        if (from < 0) from = 1 - to;
        if (to < 0 || to == from) to = 1 - from;
        return (from, to);
    }

    private void Apply(PacketInfo p)
    {
        var row = new PacketItem(p);
        var i = Items.ToList().FindIndex(x => x.Info.Seq == p.Seq);
        if (i >= 0) Items[i] = row; else Items.Insert(0, row);
        Changed?.Invoke(p);
    }
}
