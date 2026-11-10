using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tether.Core;

/// <summary>Packets live in {exchangeRoot}/packets/{seq:D6}/ — request.md, status.json, reply.md, reply-N.*</summary>
public class PacketStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Korean readable

        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string packetsDir;
    private readonly TimeProvider clock;
    private readonly Lock gate = new();
    private long lastSeq;

    public PacketStore(string exchangeRoot, TimeProvider clock)
    {
        packetsDir = Path.Combine(exchangeRoot, "packets");
        this.clock = clock;
        Directory.CreateDirectory(packetsDir);
        lastSeq = SeqDirs().Select(d => d.Seq).DefaultIfEmpty(0).Max();
    }

    public string GetPacketDir(long seq) => Path.Combine(packetsDir, seq.ToString("D6"));

    public PacketInfo Create(string from, string to, string rawRequest)
    {
        lock (gate)
        {
            var seq = ++lastSeq;
            var dir = GetPacketDir(seq);
            Directory.CreateDirectory(dir);
            WriteAtomic(Path.Combine(dir, "request.md"), rawRequest);
            var now = clock.GetUtcNow();
            return Save(new PacketInfo(seq, from, to, PacketStatus.Queued, null, now, now, []));
        }
    }

    public PacketInfo SetStatus(long seq, PacketStatus status, string? error = null)
    {
        lock (gate)
            return Save(Get(seq) with { Status = status, Error = error, UpdatedAt = clock.GetUtcNow() });
    }

    public PacketInfo SaveReply(long seq, AppReply reply)
    {
        lock (gate)
        {
            var dir = GetPacketDir(seq);
            var files = new List<string>();
            if (reply.Text is not null)
            {
                WriteAtomic(Path.Combine(dir, "reply.md"), reply.Text);
                files.Add("reply.md");
            }
            for (var i = 0; i < reply.ImagePaths.Count; i++)
            {
                var name = $"reply-{i + 1}{Path.GetExtension(reply.ImagePaths[i])}";
                var tmp = Path.Combine(dir, name + ".tmp");
                File.Copy(reply.ImagePaths[i], tmp, overwrite: true);
                File.Move(tmp, Path.Combine(dir, name), overwrite: true);
                files.Add(name);
            }
            return Save(Get(seq) with { ReplyFiles = files, UpdatedAt = clock.GetUtcNow() });
        }
    }

    public PacketInfo Get(long seq) =>
        JsonSerializer.Deserialize<PacketInfo>(File.ReadAllText(StatusPath(seq)), Json)!;

    public IReadOnlyList<PacketInfo> List() =>
        SeqDirs().OrderBy(d => d.Seq).Where(d => File.Exists(StatusPath(d.Seq))).Select(d => Get(d.Seq)).ToList();

    private PacketInfo Save(PacketInfo p)
    {
        WriteAtomic(StatusPath(p.Seq), JsonSerializer.Serialize(p, Json));
        return p;
    }

    private string StatusPath(long seq) => Path.Combine(GetPacketDir(seq), "status.json");

    private IEnumerable<(string Dir, long Seq)> SeqDirs() =>
        Directory.EnumerateDirectories(packetsDir)
            .Select(d => (Dir: d, Name: Path.GetFileName(d)))
            .Where(x => x.Name.Length == 6 && x.Name.All(char.IsAsciiDigit))
            .Select(x => (x.Dir, long.Parse(x.Name)));

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
