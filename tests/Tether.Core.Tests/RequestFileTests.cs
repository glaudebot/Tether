using System.Text;
using Tether.Core;

namespace Tether.Core.Tests;

public class RequestFileTests
{
    [Fact]
    public async Task ReadWhenReady_WaitsForLockRelease()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tether-{Guid.NewGuid():N}.md");
        try
        {
            var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            var writer = Task.Run(async () =>
            {
                await Task.Delay(300);
                var bytes = Encoding.UTF8.GetBytes("to: gemini\n완성본");
                fs.Write(bytes);
                fs.Dispose();
            });

            var text = await RequestFile.ReadWhenReadyAsync(path);
            await writer;

            Assert.Equal("to: gemini\n완성본", text);
        }
        finally { File.Delete(path); }
    }
}
