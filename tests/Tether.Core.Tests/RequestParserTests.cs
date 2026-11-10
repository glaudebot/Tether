using Tether.Core;

namespace Tether.Core.Tests;

public class RequestParserTests
{
    [Fact]
    public void Parse_Valid()
    {
        var r = RequestParser.Parse("claude", "to: Gemini\n고양이 그림\n두 번째 줄");
        Assert.Equal(new AppRequest("claude", "gemini", "고양이 그림\n두 번째 줄"), r);
    }

    [Fact]
    public void Parse_BomCrlfAndLeadingBlankLines()
    {
        var r = RequestParser.Parse("claude", "﻿\r\n\r\nTO:gemini\r\n본문\r\n");
        Assert.Equal("gemini", r.To);
        Assert.Equal("본문", r.Body);
    }

    [Fact]
    public void Parse_MissingToLine() =>
        Assert.Equal("첫 줄은 'to: <앱id>' 형식이어야 합니다",
            Assert.Throws<RequestFormatException>(() => RequestParser.Parse("claude", "그냥 본문")).Message);

    [Fact]
    public void Parse_EmptyBody() =>
        Assert.Equal("요청 본문이 비어 있습니다",
            Assert.Throws<RequestFormatException>(() => RequestParser.Parse("claude", "to: gemini\n  \n")).Message);
}
