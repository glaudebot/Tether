using System.Text.RegularExpressions;

namespace Tether.Core;

/// <summary>
/// Request file: `to: &lt;app&gt;`, then optional `key: value` header lines ended by a blank line, then the body.
/// Old format (body right after the to line) still works. A blank line right after `to:` means "no headers".
/// </summary>
public static partial class RequestParser
{
    [GeneratedRegex(@"^to:\s*(\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ToLine();

    [GeneratedRegex(@"^([A-Za-z][A-Za-z0-9_-]*)\s*:(.*)$")]
    private static partial Regex HeaderLine();

    public static AppRequest Parse(string from, string content)
    {
        var lines = content.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var first = Array.FindIndex(lines, l => l.Trim().Length > 0);
        var m = first < 0 ? Match.Empty : ToLine().Match(lines[first].Trim());
        if (!m.Success) throw new RequestFormatException("첫 줄은 'to: <앱id>' 형식이어야 합니다");

        var reply = ReplyKind.Image;
        var thread = ThreadMode.Continue;
        var i = first + 1;
        if (i < lines.Length && HeaderLine().IsMatch(lines[i].Trim()))
        {
            // header block: every line up to the blank line must be a known `key: value`
            for (; i < lines.Length && lines[i].Trim().Length > 0; i++)
            {
                var h = HeaderLine().Match(lines[i].Trim());
                if (!h.Success) throw new RequestFormatException("머리줄 다음에는 빈 줄이 와야 합니다");
                var key = h.Groups[1].Value.ToLowerInvariant();
                var value = h.Groups[2].Value.Trim().ToLowerInvariant();
                switch (key)
                {
                    case "reply":
                        reply = value switch
                        {
                            "text" => ReplyKind.Text,
                            "image" => ReplyKind.Image,
                            _ => throw new RequestFormatException("reply 값은 text 또는 image 여야 합니다"),
                        };
                        break;
                    case "thread":
                        thread = value switch
                        {
                            "new" => ThreadMode.New,
                            "continue" => ThreadMode.Continue,
                            _ => throw new RequestFormatException("thread 값은 new 또는 continue 여야 합니다"),
                        };
                        break;
                    default:
                        throw new RequestFormatException($"알 수 없는 머리줄: {key}");
                }
            }
        }

        var body = string.Join("\n", lines[Math.Min(i, lines.Length)..]).Trim();
        if (body.Length == 0) throw new RequestFormatException("요청 본문이 비어 있습니다");

        return new AppRequest(from, m.Groups[1].Value.ToLowerInvariant(), body, reply, thread);
    }
}
