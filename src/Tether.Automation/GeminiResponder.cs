using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using Tether.Automation.Native;
using Tether.Core;
using Tether.Core.Config;

namespace Tether.Automation;

/// <summary>
/// Pastes the prompt into Gemini desktop, waits for generation to finish, and saves new images.
/// Text-only replies return no files (see ledger). Selectors come from config (docs/uia-notes.md).
/// </summary>
public class GeminiResponder(AppConfig app) : IResponder
{
    private static readonly TimeSpan FindTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeoutConfig timeouts = app.Timeouts ?? new TimeoutConfig();
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), "tether");
    private int[]? lastImageBefore;

    public string AppId => app.Id;

    /// <summary>
    /// Generated images in conversation order. Their download/copy buttons are only built near the viewport,
    /// so images (not buttons) are what we count; small icons are skipped.
    /// </summary>
    private static List<AutomationElement> Images(AutomationElement win) =>
        win.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image))
            .Cast<AutomationElement>()
            .Where(e => e.Current.BoundingRectangle is { Width: >= 200, Height: >= 200 })
            .ToList();

    /// <summary>Images after the one that was last before the prompt (all of them if it is gone).</summary>
    private List<AutomationElement> NewImages(AutomationElement win)
    {
        var images = Images(win);
        if (lastImageBefore is null) return images;
        var i = images.FindIndex(e => e.GetRuntimeId().SequenceEqual(lastImageBefore));
        if (i >= 0) return images.Skip(i + 1).ToList();
        return images.Count > 0 && !images[^1].GetRuntimeId().SequenceEqual(lastImageBefore) ? [images[^1]] : [];
    }

    public Task DeliverAsync(string prompt, CancellationToken ct) => Task.Run(() =>
    {
        var (p, win) = UiaFinder.MainWindow(app);
        User32.Activate(p.MainWindowHandle);
        var input = UiaFinder.WaitAll(win, app.Selector("input"), FindTimeout).FirstOrDefault()
            ?? throw new InvalidOperationException($"{app.Id} 입력창을 찾을 수 없습니다");
        lastImageBefore = Images(win).LastOrDefault()?.GetRuntimeId();
        ct.ThrowIfCancellationRequested();
        Keys.PasteAndEnter(p.MainWindowHandle, input, prompt);
    }, ct);

    public Task<AppReply> CollectReplyAsync(CancellationToken ct) => Task.Run(() =>
    {
        var (p, win) = UiaFinder.MainWindow(app);
        var sw = Stopwatch.StartNew();
        var stable = Stopwatch.StartNew();
        var lastCount = -1;
        var stopSeen = false;

        while (sw.Elapsed.TotalSeconds < timeouts.ResponseSec)
        {
            ct.ThrowIfCancellationRequested();
            var generating = UiaFinder.FindAll(win, app.Selector("stopButton")).Count > 0;
            stopSeen |= generating;
            var fresh = NewImages(win);
            var count = fresh.Count;
            if (generating || count != lastCount) { stable.Restart(); lastCount = count; }

            // images can render a while after the stop button disappears
            var idle = stable.Elapsed.TotalSeconds;
            var done = !generating && (count > 0 ? idle >= timeouts.StableSec : stopSeen && idle >= timeouts.TextIdleSec);
            if (done) return new AppReply(null, SaveNewImages(p, win, fresh, ct));
            Thread.Sleep(1000);
        }
        throw new TimeoutException($"{app.Id} 응답 시간 초과 ({timeouts.ResponseSec}초)");
    }, ct);

    private List<string> SaveNewImages(Process p, AutomationElement win, List<AutomationElement> images, CancellationToken ct)
    {
        Directory.CreateDirectory(tempDir);
        var paths = new List<string>();
        foreach (var image in images)
        {
            ct.ThrowIfCancellationRequested();
            var path = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
            if (!TryDownload(p, win, image, path) && !TryCopy(win, image, path))
                throw new InvalidOperationException($"{app.Id} 이미지를 가져오지 못했습니다");
            paths.Add(path);
        }
        return paths;
    }

    /// <summary>
    /// The button belonging to an image: its toolbar sits just above the image (seen ~105px above the top),
    /// so take the closest button between a bit above the image top and the image bottom.
    /// </summary>
    private AutomationElement? ButtonFor(AutomationElement win, string selector, AutomationElement image)
    {
        var img = image.Current.BoundingRectangle;
        return UiaFinder.FindAll(win, app.Selector(selector))
            .Select(b => (b, y: b.Current.BoundingRectangle.Y))
            .Where(t => t.y >= img.Top - 300 && t.y <= img.Bottom)
            .OrderBy(t => Math.Abs(t.y - img.Top))
            .Select(t => t.b)
            .FirstOrDefault();
    }

    /// <summary>Original size: click download, then drive the Windows save dialog to our path.</summary>
    private bool TryDownload(Process p, AutomationElement win, AutomationElement image, string path)
    {
        if (ButtonFor(win, "imageDownload", image) is not { } button) return false;
        var existing = User32.FindWindows(p.Id, "#32770").ToHashSet();
        UiaFinder.Invoke(button);

        // The dialog shows only after the original has been fetched (can take tens of seconds).
        var limit = TimeSpan.FromSeconds(timeouts.DownloadSec);
        var dialog = WaitFor(limit, () => User32.FindWindows(p.Id, "#32770").FirstOrDefault(h => !existing.Contains(h)));
        if (dialog == IntPtr.Zero) return false;
        Keys.FillSaveDialog(dialog, path);
        if (WaitForFile(path, TimeSpan.FromSeconds(30))) return true;

        // never leave a save dialog (or its overwrite prompt) open behind us
        foreach (var h in User32.FindWindows(p.Id, "#32770").Where(h => !existing.Contains(h)))
            User32.PostMessage(h, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return false;
    }

    /// <summary>Fallback (reduced size): copy button puts the image on the clipboard.</summary>
    private bool TryCopy(AutomationElement win, AutomationElement image, string path)
    {
        if (ButtonFor(win, "imageCopy", image) is not { } button) return false;
        return Sta.Run(() =>
        {
            using var clip = new ClipboardScope();
            Clipboard.Clear();
            UiaFinder.Invoke(button);
            var img = WaitFor(TimeSpan.FromSeconds(10), () => Clipboard.ContainsImage() ? Clipboard.GetImage() : null);
            if (img is null) return false;
            using var fs = File.Create(path);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            enc.Save(fs);
            return true;
        });
    }

    private static bool WaitForFile(string path, TimeSpan timeout)
    {
        long last = -1;
        return WaitFor(timeout, () =>
        {
            if (!File.Exists(path)) return false;
            var len = new FileInfo(path).Length;
            var settled = len > 0 && len == last;
            last = len;
            return settled;
        });
    }

    private static T WaitFor<T>(TimeSpan timeout, Func<T> probe)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            var v = probe();
            if (!EqualityComparer<T>.Default.Equals(v, default!) || sw.Elapsed > timeout) return v;
            Thread.Sleep(500);
        }
    }
}
