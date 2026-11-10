using System.Text.RegularExpressions;

namespace Tether.Core;

public static partial class RequestParser
{
    [GeneratedRegex(@"^to:\s*(\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ToLine();

    public static AppRequest Parse(string from, string content)
    {
        var lines = content.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var first = Array.FindIndex(lines, l => l.Trim().Length > 0);
        var m = first < 0 ? Match.Empty : ToLine().Match(lines[first].Trim());
        if (!m.Success) throw new RequestFormatException("첫 줄은 'to: <앱id>' 형식이어야 합니다");

        var body = string.Join("\n", lines[(first + 1)..]).Trim();
        if (body.Length == 0) throw new RequestFormatException("요청 본문이 비어 있습니다");

        return new AppRequest(from, m.Groups[1].Value.ToLowerInvariant(), body);
    }
}
