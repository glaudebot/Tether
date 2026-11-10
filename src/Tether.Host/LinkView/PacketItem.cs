using System.Windows.Media;
using Tether.Core;

namespace Tether.Host.LinkView;

/// <summary>One timeline row. Immutable: a status change replaces the row.</summary>
public sealed record PacketItem(PacketInfo Info)
{
    public string Text =>
        $"{Info.UpdatedAt.ToLocalTime():HH:mm:ss} #{Info.Seq:D6} {Info.From}→{Info.To} {Info.Status}" +
        (Info.Error is null ? "" : $"\n{Info.Error}");

    public Brush Brush => StatusBrush(Info.Status);

    public static Brush StatusBrush(PacketStatus s) => s switch
    {
        PacketStatus.Delivering or PacketStatus.Generating => Brushes.DodgerBlue,
        PacketStatus.Replied or PacketStatus.Notified => Brushes.MediumSeaGreen,
        PacketStatus.Failed => Brushes.IndianRed,
        _ => Brushes.Gray,
    };
}
