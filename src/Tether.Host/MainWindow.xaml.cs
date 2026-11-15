using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Tether.Host.Docking;
using Tether.Host.LinkView;

namespace Tether.Host;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private DockManager? dock;

    public MainWindow()
    {
        InitializeComponent();
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "--config");
        vm = new MainViewModel(i > 0 && i + 1 < args.Length ? args[i + 1] : DefaultConfig());
        DataContext = vm;
        RestorePlacement();
        SizeChanged += (_, _) => ApplyLayout();

        Loaded += (_, _) =>
        {
            var linkVm = new LinkViewModel(vm.Store, vm.Router, Dispatcher);
            Link.Attach(linkVm, vm.Store);
            dock = new DockManager(this, [SlotA, SlotB], vm.Apps);
            dock.SlotChanged += (slot, appId) =>
            {
                vm.StatusText = $"슬롯 {(char)('A' + slot)}: {appId ?? "-"}";
                linkVm.SetSlot(slot, appId);
            };
            UndockButton.Click += (_, _) => dock.UndockAll();
            EmbedToggle.Click += (_, _) => dock.EmbedMode = EmbedToggle.IsChecked == true;

            // restore docked windows on every exit path
            Application.Current.DispatcherUnhandledException += (_, _) => dock?.UndockAll();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => dock?.UndockAll();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => dock?.UndockAll();
        };
    }

    /// <summary>config/tether.json in the working directory (dev runs from the repo), else next to the exe (packaged).</summary>
    private static string DefaultConfig()
    {
        const string rel = "config/tether.json";
        return System.IO.File.Exists(rel) ? rel : System.IO.Path.Combine(AppContext.BaseDirectory, rel);
    }

    private bool? stacked;

    /// <summary>Slots side by side on a wide window, stacked (A / Link View / B) on a tall one (portrait monitor).</summary>
    private void ApplyLayout()
    {
        var tall = ActualHeight > ActualWidth;
        if (stacked == tall) return;
        stacked = tall;
        MinWidth = MinHeight = 0; // min size learned for the old layout no longer applies

        SlotGrid.ColumnDefinitions.Clear();
        SlotGrid.RowDefinitions.Clear();
        UIElement[] cells = [SlotA, LinkArea, SlotB];
        for (var i = 0; i < cells.Length; i++)
        {
            var size = i == 1 ? new GridLength(tall ? 160 : 240) : new GridLength(1, GridUnitType.Star);
            if (tall) SlotGrid.RowDefinitions.Add(new RowDefinition { Height = size });
            else SlotGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = size });
            Grid.SetRow(cells[i], tall ? i : 0);
            Grid.SetColumn(cells[i], tall ? 0 : i);
        }
    }

    private static readonly string PlacementFile =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tether", "window.txt");

    /// <summary>Last position/size ("left top width height maximized"), if it still lands on a screen.</summary>
    private void RestorePlacement()
    {
        try
        {
            var v = System.IO.File.ReadAllText(PlacementFile).Split(' ');
            var (l, t, w, h) = (Num(v[0]), Num(v[1]), Num(v[2]), Num(v[3]));
            var onScreen = l + w > SystemParameters.VirtualScreenLeft + 50 && t + 20 > SystemParameters.VirtualScreenTop
                && l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50
                && t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50;
            if (!onScreen || w < 200 || h < 150) return; // monitor gone or garbage: keep the default
            WindowStartupLocation = WindowStartupLocation.Manual;
            (Left, Top, Width, Height) = (l, t, w, h);
            if (v.Length > 4 && v[4] == "1") WindowState = WindowState.Maximized;
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or FormatException or IndexOutOfRangeException) { }

        static double Num(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SavePlacement()
    {
        try
        {
            var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PlacementFile)!);
            System.IO.File.WriteAllText(PlacementFile, FormattableString.Invariant(
                $"{b.Left} {b.Top} {b.Width} {b.Height} {(WindowState == WindowState.Maximized ? 1 : 0)}"));
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        dock?.Dispose();
        vm.Dispose();
        base.OnClosing(e);
    }
}
