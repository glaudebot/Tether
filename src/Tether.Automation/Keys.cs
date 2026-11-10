using System;
using System.Threading;
using System.Windows.Automation;
using Tether.Automation.Native;
using SendKeys = System.Windows.Forms.SendKeys;

namespace Tether.Automation;

/// <summary>Typing via clipboard paste: SendKeys of raw text is mangled by the Korean IME.</summary>
public static class Keys
{
    public static void PasteAndEnter(IntPtr window, AutomationElement input, string text) => Sta.Run(() =>
    {
        using var clip = new ClipboardScope();
        clip.SetText(text);
        User32.Activate(window);
        input.SetFocus();
        Thread.Sleep(300);
        SendKeys.SendWait("^v");
        Thread.Sleep(300);
        SendKeys.SendWait("{ENTER}");
        Thread.Sleep(300);
    });

    /// <summary>Fills the file name of a Windows save dialog (Alt+N focuses it) and confirms.</summary>
    public static void FillSaveDialog(IntPtr dialog, string path) => Sta.Run(() =>
    {
        using var clip = new ClipboardScope();
        clip.SetText(path);
        User32.Activate(dialog);
        Thread.Sleep(400);
        SendKeys.SendWait("%n");
        Thread.Sleep(200);
        SendKeys.SendWait("^a");
        SendKeys.SendWait("^v");
        Thread.Sleep(200);
        SendKeys.SendWait("{ENTER}");
        Thread.Sleep(300);
    });
}
