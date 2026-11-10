using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tether.Core;
using Tether.Core.Config;

namespace Tether.Automation;

/// <summary>
/// Pastes the notice into the Claude conversation that is open on screen.
/// Waits while the input box has text (the person is typing) and never sends into a Claude Code session.
/// </summary>
public class ClaudeNotifier(AppConfig app) : INotifier
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private const string CodeSessionSuffix = " - Claude Code";

    public string AppId => app.Id;

    public Task NotifyAsync(string message, CancellationToken ct) => Task.Run(() =>
    {
        var (p, win) = UiaFinder.MainWindow(app);
        var sw = Stopwatch.StartNew();
        var chatOpen = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var title = UiaFinder.FindAll(win, app.Selector("conversation")).Select(d => d.Current.Name).FirstOrDefault(n => n.Length > 0);
            chatOpen = title is not null && !title.EndsWith(CodeSessionSuffix, StringComparison.Ordinal);
            var input = UiaFinder.FindAll(win, app.Selector("input")).FirstOrDefault(e => !e.Current.IsOffscreen);
            if (chatOpen && input is not null && UiaFinder.Value(input).Trim().Length == 0)
            {
                Keys.PasteAndEnter(p.MainWindowHandle, input, message);
                return;
            }
            if (sw.Elapsed > MaxWait) break;
            Thread.Sleep(Interval);
        }
        throw new InvalidOperationException(chatOpen ? $"{app.Id} 입력창이 사용 중입니다" : $"{app.Id} Chat 대화가 열려 있지 않습니다");
    }, ct);
}
