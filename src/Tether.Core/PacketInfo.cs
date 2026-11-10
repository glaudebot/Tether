namespace Tether.Core;

public record PacketInfo(
    long Seq, string From, string To, PacketStatus Status, string? Error,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<string> ReplyFiles);
