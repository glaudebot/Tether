using System.Text.Json;

namespace Tether.Core.Config;

public record TetherConfig(string ExchangeRoot, int MaxPacketsPerRun, IReadOnlyList<AppConfig> Apps)
{
    public static TetherConfig Load(string path) =>
        JsonSerializer.Deserialize<TetherConfig>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidDataException($"설정 파일이 비어 있습니다: {path}");
}

public record AppConfig(string Id, string Role, string Process, IReadOnlyDictionary<string, SelectorConfig> Selectors, TimeoutConfig? Timeouts)
{
    public SelectorConfig Selector(string key) =>
        Selectors.TryGetValue(key, out var s) ? s : throw new InvalidDataException($"{Id} 설정에 '{key}' 셀렉터가 없습니다");
}

/// <summary>Name and AutomationId may use '*' at either end for contains / starts-with / ends-with matching.</summary>
public record SelectorConfig(string? AutomationId, string? Name, string? ControlType, string? ClassName);

/// <param name="TextIdleSec">How long a text answer (no new image) must stay unchanged before it counts as finished.</param>
public record TimeoutConfig(int ResponseSec = 180, int StableSec = 3, int DownloadSec = 90, int TextIdleSec = 20);
