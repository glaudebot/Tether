using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Tether.Automation.Native;
using Tether.Core.Config;

namespace Tether.Host.Docking;

/// <summary>A foreign top-level window docked into a slot, with the rect to restore it to.</summary>
public sealed record DockedWindow(IntPtr Hwnd, User32.RECT Original, string? AppId, EmbedState? Embed = null);

/// <summary>
/// Follow docking: a window dropped (end of user move) with the cursor inside an empty slot is
/// kept over that slot as the host moves, resizes and minimizes. Every exit path calls UndockAll.
/// </summary>
public sealed class DockManager : IDisposable
{
    private readonly Window host;
    private readonly IReadOnlyList<FrameworkElement> slots;
    private readonly IReadOnlyList<AppConfig> apps;
    private readonly DockedWindow?[] docked;
    private readonly User32.WinEventProc proc; // keep alive: native code holds the pointer
    private readonly IntPtr hook;
    private readonly IntPtr hostHwnd;
    private readonly DispatcherTimer aliveTimer;

    public DockManager(Window host, IReadOnlyList<FrameworkElement> slots, IReadOnlyList<AppConfig> apps)
    {
        this.host = host;
        this.slots = slots;
        this.apps = apps;
        docked = new DockedWindow?[slots.Count];
        hostHwnd = new WindowInteropHelper(host).EnsureHandle();

        proc = OnMoveSizeEnd;
        hook = User32.SetWinEventHook(User32.EVENT_SYSTEM_MOVESIZEEND, User32.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, proc, 0, 0, User32.WINEVENT_OUTOFCONTEXT);

        host.LocationChanged += (_, _) => FollowAll();
        host.SizeChanged += (_, _) => FollowAll();
        host.StateChanged += (_, _) => OnHostStateChanged();
        host.Activated += (_, _) => RaiseAll();
        foreach (var s in slots) s.SizeChanged += (_, _) => FollowAll();

        aliveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        aliveTimer.Tick += (_, _) => DropClosed();
        aliveTimer.Start();
    }

    /// <summary>(slot index, app id from tether.json by process name, or null when emptied / unknown)</summary>
    public event Action<int, string?>? SlotChanged;

    /// <summary>M5 experiment: windows docked from now on become child windows of the host instead of following it.</summary>
    public bool EmbedMode { get; set; }

    public void UndockAll()
    {
        for (var i = 0; i < docked.Length; i++) Release(i, restore: true);
    }

    public void Dispose()
    {
        aliveTimer.Stop();
        User32.UnhookWinEvent(hook);
        UndockAll();
    }

    private void OnMoveSizeEnd(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var root = User32.GetAncestor(hwnd, User32.GA_ROOT);
        User32.GetCursorPos(out var pt);
        var target = SlotAt(pt);
        if (idObject != 0 || hwnd == IntPtr.Zero) return;
        if (root == hostHwnd) return;

        var current = Array.FindIndex(docked, d => d?.Hwnd == root);

        if (current >= 0)
        {
            if (current == target) { Follow(current); return; }
            Release(current, restore: false); // dragged out: leave it where the user dropped it
        }
        if (target >= 0 && docked[target] is null) Dock(target, root);
    }

    private void Dock(int slot, IntPtr hwnd)
    {
        User32.GetWindowRect(hwnd, out var original);
        if (User32.IsZoomed(hwnd) || User32.IsIconic(hwnd)) User32.ShowWindow(hwnd, User32.SW_RESTORE);
        var appId = AppIdOf(hwnd);
        var embed = EmbedMode ? EmbedStrategy.Embed(hwnd, hostHwnd) : null;
        docked[slot] = new DockedWindow(hwnd, original, appId, embed);
        Follow(slot);
        SlotChanged?.Invoke(slot, appId);
    }

    private void Release(int slot, bool restore)
    {
        var d = docked[slot];
        if (d is null) return;
        docked[slot] = null;
        if (host.Dispatcher.CheckAccess()) host.MinWidth = host.MinHeight = 0; // re-learned on the next follow
        if (d.Embed is { } e)
        {
            // an embedded window must always get its parent and frame back, whatever `restore` says
            if (User32.IsWindow(d.Hwnd)) EmbedStrategy.Restore(d.Hwnd, e, d.Original);
        }
        else if (restore && User32.IsWindow(d.Hwnd))
        {
            if (User32.IsIconic(d.Hwnd)) User32.ShowWindow(d.Hwnd, User32.SW_SHOWNOACTIVATE);
            var r = d.Original;
            User32.SetWindowPos(d.Hwnd, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
        }
        SlotChanged?.Invoke(slot, null);
    }

    private void FollowAll()
    {
        for (var i = 0; i < docked.Length; i++) Follow(i);
    }

    private void Follow(int slot)
    {
        var d = docked[slot];
        if (d is null || host.WindowState == WindowState.Minimized) return;
        if (SlotRect(slot) is not { } r) return;
        if (d.Embed is not null) { EmbedStrategy.Place(d.Hwnd, hostHwnd, r); return; }
        var (w, h) = (r.Right - r.Left, r.Bottom - r.Top);
        var actual = User32.PlaceVisible(d.Hwnd, r.Left, r.Top, w, h);

        // The docked window refused to shrink (its own minimum size): grow the host's minimum so the slot fits it.
        var dpi = VisualTreeHelper.GetDpi(host);
        if (actual.Width > w) host.MinWidth = Math.Max(host.MinWidth, host.ActualWidth + (actual.Width - w) / dpi.DpiScaleX);
        if (actual.Height > h) host.MinHeight = Math.Max(host.MinHeight, host.ActualHeight + (actual.Height - h) / dpi.DpiScaleY);
    }

    private void OnHostStateChanged()
    {
        var minimized = host.WindowState == WindowState.Minimized;
        foreach (var d in docked.OfType<DockedWindow>().Where(d => d.Embed is null)) // children follow the host by themselves
            User32.ShowWindow(d.Hwnd, minimized ? User32.SW_SHOWMINNOACTIVE : User32.SW_SHOWNOACTIVATE);
        if (!minimized) { FollowAll(); RaiseAll(); }
    }

    /// <summary>
    /// Keep docked windows above the host. Windows won't let us raise another process's window,
    /// so instead the host slips just below the lowest docked window (after activation settles).
    /// </summary>
    private void RaiseAll() =>
        host.Dispatcher.BeginInvoke(() =>
        {
            var handles = docked.OfType<DockedWindow>().Where(d => d.Embed is null).Select(d => d.Hwnd).ToHashSet();
            var lowest = handles.FirstOrDefault(h => !HasBelow(h, handles));
            if (lowest != IntPtr.Zero)
                User32.SetWindowPos(hostHwnd, lowest, 0, 0, 0, 0, User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
        }, DispatcherPriority.Background);

    private static bool HasBelow(IntPtr h, HashSet<IntPtr> others)
    {
        for (var w = User32.GetWindow(h, User32.GW_HWNDNEXT); w != IntPtr.Zero; w = User32.GetWindow(w, User32.GW_HWNDNEXT))
            if (others.Contains(w)) return true;
        return false;
    }

    private void DropClosed()
    {
        for (var i = 0; i < docked.Length; i++)
            if (docked[i] is { } d && !User32.IsWindow(d.Hwnd)) Release(i, restore: false);
    }

    private int SlotAt(User32.POINT pt)
    {
        for (var i = 0; i < slots.Count; i++)
            if (SlotRect(i) is { } r && pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom) return i;
        return -1;
    }

    /// <summary>Slot bounds in physical screen pixels.</summary>
    private User32.RECT? SlotRect(int slot)
    {
        var s = slots[slot];
        if (!s.IsVisible || PresentationSource.FromVisual(s) is null) return null;
        var p = s.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(s);
        var x = (int)Math.Round(p.X);
        var y = (int)Math.Round(p.Y);
        return new User32.RECT
        {
            Left = x, Top = y,
            Right = x + (int)Math.Round(s.ActualWidth * dpi.DpiScaleX),
            Bottom = y + (int)Math.Round(s.ActualHeight * dpi.DpiScaleY),
        };
    }

    private string? AppIdOf(IntPtr hwnd)
    {
        User32.GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            var name = Process.GetProcessById((int)pid).ProcessName;
            return apps.FirstOrDefault(a => string.Equals(a.Process, name, StringComparison.OrdinalIgnoreCase))?.Id;
        }
        catch (ArgumentException) { return null; }
    }
}
