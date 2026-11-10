using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Tether.Core;

namespace Tether.Host.LinkView;

public partial class LinkView : UserControl
{
    private LinkViewModel? vm;
    private PacketStore? store;
    private bool? strip;

    public LinkView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ApplyLayout();
        Timeline.PreviewMouseLeftButtonUp += OnTimelineClick;
    }

    public void Attach(LinkViewModel model, PacketStore packets)
    {
        vm = model;
        store = packets;
        Timeline.ItemsSource = vm.Items;
        vm.SlotsChanged += UpdateNodes;
        vm.Changed += OnPacketChanged;
        UpdateNodes();
    }

    /// <summary>Graph above the timeline in the narrow column, beside it in the wide strip of the stacked layout.</summary>
    private void ApplyLayout()
    {
        var wide = ActualWidth > ActualHeight * 2;
        if (strip == wide) return;
        strip = wide;
        Root.RowDefinitions.Clear();
        Root.ColumnDefinitions.Clear();
        if (wide)
        {
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            Root.ColumnDefinitions.Add(new ColumnDefinition());
        }
        else
        {
            Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.RowDefinitions.Add(new RowDefinition());
        }
        Grid.SetRow(Timeline, wide ? 0 : 1);
        Grid.SetColumn(Timeline, wide ? 1 : 0);
    }

    private void UpdateNodes()
    {
        if (vm is null) return;
        SetNode(NodeA, LabelA, vm.SlotApp(0));
        SetNode(NodeB, LabelB, vm.SlotApp(1));
    }

    private static void SetNode(Border node, TextBlock label, string? app)
    {
        label.Text = app ?? "비어 있음";
        node.BorderBrush = app is null ? Brushes.DimGray : Brushes.MediumSeaGreen;
        node.Opacity = app is null ? 0.5 : 1;
    }

    private void OnPacketChanged(PacketInfo p)
    {
        LinkLine.Fill = PacketItem.StatusBrush(p.Status);
        var (from, to) = vm!.Nodes(p);
        StopPulse();
        switch (p.Status)
        {
            case PacketStatus.Delivering: FlowDot(from, to); break;
            case PacketStatus.Replied: FlowDot(to, from); break;
            case PacketStatus.Generating: Pulse(to == 0 ? NodeA : NodeB); break;
        }
    }

    /// <summary>A dot runs along the line from node a to node b in about 0.8 s.</summary>
    private void FlowDot(int a, int b)
    {
        var end = Math.Max(0, DotLane.ActualWidth - Dot.Width);
        var anim = new DoubleAnimation(a == 0 ? 0 : end, b == 0 ? 0 : end, TimeSpan.FromSeconds(0.8));
        anim.Completed += (_, _) => Dot.Visibility = Visibility.Hidden;
        Dot.Visibility = Visibility.Visible;
        Dot.BeginAnimation(Canvas.LeftProperty, anim);
    }

    private static void Pulse(Border node) =>
        node.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(0.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        });

    private void StopPulse()
    {
        NodeA.BeginAnimation(OpacityProperty, null);
        NodeB.BeginAnimation(OpacityProperty, null);
        UpdateNodes(); // restore docked/empty opacity
    }

    private void OnTimelineClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is PacketItem item && store is not null)
            ShowPreview(item.Info, store.GetPacketDir(item.Info.Seq));
    }

    /// <summary>request.md, reply.md and reply images of one packet in a separate window.</summary>
    private void ShowPreview(PacketInfo p, string dir)
    {
        var panel = new StackPanel { Margin = new Thickness(8) };
        void Header(string t) =>
            panel.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 2) });
        void Text(string path)
        {
            Header(Path.GetFileName(path));
            panel.Children.Add(new TextBox
            {
                Text = File.Exists(path) ? File.ReadAllText(path) : "(없음)",
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
            });
        }

        if (p.Error is not null)
        {
            Header("오류");
            panel.Children.Add(new TextBlock { Text = p.Error, Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap });
        }
        Text(Path.Combine(dir, "request.md"));
        foreach (var f in p.ReplyFiles)
        {
            var path = Path.Combine(dir, f);
            if (f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) { Text(path); continue; }
            if (!File.Exists(path)) continue;
            Header(f);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // don't keep the file locked
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            panel.Children.Add(new Image { Source = bmp, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left });
        }

        new Window
        {
            Title = $"Tether #{p.Seq:D6} {p.From}→{p.To} {p.Status}",
            Width = 820,
            Height = 700,
            Owner = Window.GetWindow(this),
            Content = new ScrollViewer { Content = panel },
        }.Show();
    }
}
