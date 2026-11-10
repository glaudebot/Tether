using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tether.Automation;
using Tether.Core.Config;

Console.OutputEncoding = Encoding.UTF8;

var argList = args.ToList();
var configPath = "config/tether.json";
var ci = argList.IndexOf("--config");
if (ci >= 0 && ci + 1 < argList.Count) { configPath = argList[ci + 1]; argList.RemoveRange(ci, 2); }

if (argList.Count == 0)
{
    Console.WriteLine("""
        사용법:
          tether run [--config config/tether.json]
          tether test-gemini "<prompt>"
          tether test-notify "<text>"
        """);
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    using var tether = new TetherRuntime(TetherConfig.Load(configPath));
    switch (argList[0])
    {
        case "run":
            tether.Router.PacketChanged += p => Log($"#{p.Seq:D6} {p.From}→{p.To} {p.Status}{(p.Error is null ? "" : $" ({p.Error})")}");
            tether.Start(cts.Token, Log);
            Log($"감시 중: {tether.OutboxDir}  (Ctrl+C로 종료)");
            try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }
            Log("종료");
            return 0;
        case "test-gemini" when argList.Count > 1:
            var gemini = tether.Responders.Single(r => r.AppId == "gemini");
            await gemini.DeliverAsync(argList[1], cts.Token);
            Log("전송 완료, 응답 대기 중...");
            var reply = await gemini.CollectReplyAsync(cts.Token);
            Log($"DONE {string.Join(", ", reply.ImagePaths)}");
            return 0;
        case "test-notify" when argList.Count > 1:
            await tether.Notifiers.Single(n => n.AppId == "claude").NotifyAsync(argList[1], cts.Token);
            Log("DONE");
            return 0;
        default:
            Console.Error.WriteLine($"알 수 없는 명령: {string.Join(' ', argList)}");
            return 1;
    }
}
catch (OperationCanceledException) { Log("중지됨"); return 0; }
catch (Exception e) { Console.Error.WriteLine($"실패: {e.Message}"); return 1; }

static void Log(string m) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {m}");
