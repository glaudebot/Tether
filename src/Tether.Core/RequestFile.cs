namespace Tether.Core;

public static class RequestFile
{
    /// <summary>Reads the whole file, retrying while another process still holds it open.</summary>
    public static async Task<string> ReadWhenReadyAsync(string path, int attempts = 10, TimeSpan? delay = null)
    {
        for (var i = 1; ; i++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(fs);
                return await reader.ReadToEndAsync();
            }
            catch (IOException) when (i < attempts)
            {
                await Task.Delay(delay ?? TimeSpan.FromMilliseconds(200));
            }
        }
    }
}
