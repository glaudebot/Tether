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
        vm = new MainViewModel(i > 0 && i + 1 < args.Length ? args[i + 1] : "config/tether.json");
        DataContext = vm;
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

    protected override void OnClosing(CancelEventArgs e)
    {
        dock?.Dispose();
        vm.Dispose();
        base.OnClosing(e);
    }
}
