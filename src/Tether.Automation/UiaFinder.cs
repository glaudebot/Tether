using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Tether.Automation.Native;
using Tether.Core.Config;

namespace Tether.Automation;

public static class UiaFinder
{
    /// <summary>Main window of the app's process, restored if minimized (Electron builds no tree while minimized).</summary>
    public static (Process Process, AutomationElement Window) MainWindow(AppConfig app)
    {
        var p = Process.GetProcessesByName(app.Process).FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero)
            ?? throw new InvalidOperationException($"{app.Id} 창을 찾을 수 없습니다");
        if (User32.IsIconic(p.MainWindowHandle))
        {
            User32.ShowWindow(p.MainWindowHandle, User32.SW_RESTORE);
            Thread.Sleep(1500);
        }
        return (p, AutomationElement.FromHandle(p.MainWindowHandle));
    }

    public static List<AutomationElement> FindAll(AutomationElement root, SelectorConfig s)
    {
        Condition cond = Condition.TrueCondition;
        var parts = new List<Condition>();
        if (s.AutomationId is not null && !s.AutomationId.Contains('*')) parts.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, s.AutomationId));
        if (s.ClassName is not null) parts.Add(new PropertyCondition(AutomationElement.ClassNameProperty, s.ClassName));
        if (s.ControlType is not null) parts.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlTypeOf(s.ControlType)));
        if (s.Name is not null && !s.Name.Contains('*')) parts.Add(new PropertyCondition(AutomationElement.NameProperty, s.Name));
        if (parts.Count == 1) cond = parts[0];
        else if (parts.Count > 1) cond = new AndCondition(parts.ToArray());

        var all = root.FindAll(TreeScope.Descendants, cond).Cast<AutomationElement>();
        if (s.Name is not null && s.Name.Contains('*')) all = all.Where(e => NameMatches(e.Current.Name, s.Name));
        if (s.AutomationId is not null && s.AutomationId.Contains('*')) all = all.Where(e => NameMatches(e.Current.AutomationId, s.AutomationId));
        return all.ToList();
    }

    /// <summary>Waits until at least one element matches (Electron fills its tree lazily).</summary>
    public static List<AutomationElement> WaitAll(AutomationElement root, SelectorConfig s, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            var found = FindAll(root, s);
            if (found.Count > 0 || sw.Elapsed > timeout) return found;
            Thread.Sleep(500);
        }
    }

    public static void Invoke(AutomationElement e) =>
        ((InvokePattern)e.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    public static string Value(AutomationElement e) =>
        e.TryGetCurrentPattern(ValuePattern.Pattern, out var p) ? ((ValuePattern)p).Current.Value : "";

    private static bool NameMatches(string name, string pattern)
    {
        var core = pattern.Trim('*');
        return (pattern.StartsWith('*'), pattern.EndsWith('*')) switch
        {
            (true, true) => name.Contains(core, StringComparison.Ordinal),
            (true, false) => name.EndsWith(core, StringComparison.Ordinal),
            (false, true) => name.StartsWith(core, StringComparison.Ordinal),
            _ => name == core,
        };
    }

    private static ControlType ControlTypeOf(string name) => name switch
    {
        "Edit" => ControlType.Edit,
        "Button" => ControlType.Button,
        "Document" => ControlType.Document,
        "Image" => ControlType.Image,
        "Hyperlink" => ControlType.Hyperlink,
        "Group" => ControlType.Group,
        "Text" => ControlType.Text,
        "Window" => ControlType.Window,
        _ => throw new ArgumentException($"지원하지 않는 controlType: {name}"),
    };
}
