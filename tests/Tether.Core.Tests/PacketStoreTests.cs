using Microsoft.Extensions.Time.Testing;
using Tether.Core;

namespace Tether.Core.Tests;

public sealed class PacketStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"tether-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly PacketStore store;

    public PacketStoreTests() => store = new PacketStore(root, clock);

    public void Dispose() => Directory.Delete(root, recursive: true);

    private string TempFile(string ext, string content)
    {
        var path = Path.Combine(root, $"src-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Create_First_IsSeq1_WritesFiles()
    {
        var p = store.Create("claude", "gemini", "to: gemini\n사과");
        Assert.Equal(1, p.Seq);
        Assert.Equal(PacketStatus.Queued, p.Status);
        Assert.Equal("to: gemini\n사과", File.ReadAllText(Path.Combine(root, "packets", "000001", "request.md")));
        Assert.Equal(p, store.Get(1), PacketEq);
        Assert.Contains("\"status\": \"Queued\"", File.ReadAllText(Path.Combine(root, "packets", "000001", "status.json")));
    }

    [Fact]
    public void Create_AfterRestart_ContinuesSeq()
    {
        store.Create("claude", "gemini", "첫째");
        store.Create("claude", "gemini", "둘째");

        var restarted = new PacketStore(root, clock);
        Assert.Equal(3, restarted.Create("claude", "gemini", "셋째").Seq);
        Assert.Equal("첫째", File.ReadAllText(Path.Combine(root, "packets", "000001", "request.md")));
    }

    [Fact]
    public void SetStatus_UpdatesStatusErrorAndTime()
    {
        var created = store.Create("claude", "gemini", "x");
        clock.Advance(TimeSpan.FromSeconds(5));

        var p = store.SetStatus(1, PacketStatus.Failed, "x");

        Assert.Equal(PacketStatus.Failed, p.Status);
        Assert.Equal("x", p.Error);
        Assert.Equal(created.CreatedAt + TimeSpan.FromSeconds(5), p.UpdatedAt);
        Assert.Equal(p, store.Get(1), PacketEq);
    }

    [Fact]
    public void SaveReply_WritesTextAndCopiesImages()
    {
        store.Create("claude", "gemini", "x");
        var png = TempFile(".png", "png");
        var jpg = TempFile(".jpg", "jpg");

        var p = store.SaveReply(1, new AppReply("설명", [png, jpg]));

        Assert.Equal(["reply.md", "reply-1.png", "reply-2.jpg"], p.ReplyFiles);
        Assert.Equal(PacketStatus.Queued, p.Status);
        var dir = store.GetPacketDir(1);
        Assert.Equal("설명", File.ReadAllText(Path.Combine(dir, "reply.md")));
        Assert.Equal("jpg", File.ReadAllText(Path.Combine(dir, "reply-2.jpg")));
    }

    [Fact]
    public void List_IsSeqAscending()
    {
        store.Create("claude", "gemini", "a");
        store.Create("claude", "gemini", "b");
        Assert.Equal([1L, 2L], store.List().Select(p => p.Seq));
    }

    [Fact]
    public void Writes_LeaveNoTmpFiles()
    {
        store.Create("claude", "gemini", "x");
        store.SetStatus(1, PacketStatus.Delivering);
        store.SaveReply(1, new AppReply("t", [TempFile(".png", "p")]));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "packets"), "*.tmp", SearchOption.AllDirectories));
    }

    // records with IReadOnlyList compare by reference; compare by content instead
    private static readonly IEqualityComparer<PacketInfo> PacketEq = EqualityComparer<PacketInfo>.Create(
        (a, b) => a!.Equals(b! with { ReplyFiles = a.ReplyFiles }) && a.ReplyFiles.SequenceEqual(b!.ReplyFiles));
}
