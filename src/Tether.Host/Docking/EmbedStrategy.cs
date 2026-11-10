using System;
using Tether.Automation.Native;

namespace Tether.Host.Docking;

/// <summary>What Embed changed on a window, so Restore can put it back.</summary>
public sealed record EmbedState(IntPtr OldParent, IntPtr Style, IntPtr ExStyle);

/// <summary>
/// M5 experiment: make a foreign top-level window a child of the host (no caption/frame).
/// Risk: if the host process is killed, its children are destroyed with it.
/// </summary>
public static class EmbedStrategy
{
    public static EmbedState Embed(IntPtr hwnd, IntPtr host)
    {
        var style = User32.GetWindowLongPtr(hwnd, User32.GWL_STYLE);
        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        var child = (style.ToInt64() & ~(User32.WS_CAPTION | User32.WS_THICKFRAME | User32.WS_POPUP)) | User32.WS_CHILD;
        User32.SetWindowLongPtr(hwnd, User32.GWL_STYLE, new IntPtr(child));
        var oldParent = User32.SetParent(hwnd, host);
        return new EmbedState(oldParent, style, exStyle);
    }

    /// <summary>Places the child over a slot given in physical screen pixels.</summary>
    public static void Place(IntPtr hwnd, IntPtr host, User32.RECT screen)
    {
        var p = new User32.POINT { X = screen.Left, Y = screen.Top };
        User32.ScreenToClient(host, ref p);
        User32.SetWindowPos(hwnd, IntPtr.Zero, p.X, p.Y, screen.Right - screen.Left, screen.Bottom - screen.Top,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_FRAMECHANGED | User32.SWP_SHOWWINDOW);
    }

    public static void Restore(IntPtr hwnd, EmbedState s, User32.RECT original)
    {
        User32.SetParent(hwnd, s.OldParent);
        User32.SetWindowLongPtr(hwnd, User32.GWL_STYLE, s.Style);
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, s.ExStyle);
        User32.SetWindowPos(hwnd, IntPtr.Zero, original.Left, original.Top,
            original.Right - original.Left, original.Bottom - original.Top,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_FRAMECHANGED | User32.SWP_SHOWWINDOW);
    }
}
