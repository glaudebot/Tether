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
    public void Parse_NoHeaders_DefaultsToImageAndContinue()
    {
        var r = RequestParser.Parse("claude", "to: gemini\n본문");
        Assert.Equal(ReplyKind.Image, r.Reply);
        Assert.Equal(ThreadMode.Continue, r.Thread);
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

    [Fact]
    public void Parse_Headers_AreReadAndKeptOutOfBody()
    {
        var r = RequestParser.Parse("claude-code", "to: gemini\nreply: text\nthread: new\n\n한 줄로 자기소개 해줘\n둘째 줄");
        Assert.Equal(new AppRequest("claude-code", "gemini", "한 줄로 자기소개 해줘\n둘째 줄", ReplyKind.Text, ThreadMode.New), r);
    }

    [Fact]
    public void Parse_Headers_CaseInsensitiveWithBomAndCrlf()
    {
        var r = RequestParser.Parse("c", "﻿to: gemini\r\nReply : TEXT\r\nTHREAD:continue\r\n\r\n본문\r\n");
        Assert.Equal(ReplyKind.Text, r.Reply);
        Assert.Equal(ThreadMode.Continue, r.Thread);
        Assert.Equal("본문", r.Body);
    }

    [Fact]
    public void Parse_BlankLineWithoutHeaders_BodyMayLookLikeHeader()
    {
        var r = RequestParser.Parse("c", "to: gemini\n\nNote: 이건 본문\n다음 줄");
        Assert.Equal("Note: 이건 본문\n다음 줄", r.Body);
        Assert.Equal(ReplyKind.Image, r.Reply);
    }

    [Fact]
    public void Parse_UnknownKey_Rejected() =>
        Assert.Equal("알 수 없는 머리줄: rely",
            Assert.Throws<RequestFormatException>(() => RequestParser.Parse("c", "to: gemini\nrely: text\n\n본문")).Message);

    [Theory]
    [InlineData("reply: video", "reply 값은 text 또는 image 여야 합니다")]
    [InlineData("thread: old", "thread 값은 new 또는 continue 여야 합니다")]
    [InlineData("reply:", "reply 값은 text 또는 image 여야 합니다")]
    public void Parse_BadValue_Rejected(string header, string message) =>
        Assert.Equal(message,
            Assert.Throws<RequestFormatException>(() => RequestParser.Parse("c", $"to: gemini\n{header}\n\n본문")).Message);

    [Theory]
    [InlineData("to: gemini\nreply: text\n\n  \n")]
    [InlineData("to: gemini\nreply: text\n")]
    [InlineData("to: gemini\nreply: text")]
    public void Parse_HeadersWithEmptyBody_Rejected(string content) =>
        Assert.Equal("요청 본문이 비어 있습니다",
            Assert.Throws<RequestFormatException>(() => RequestParser.Parse("c", content)).Message);

    [Fact]
    public void Parse_HeadersWithoutBlankLine_Rejected() =>
        Assert.Equal("머리줄 다음에는 빈 줄이 와야 합니다",
            Assert.Throws<RequestFormatException>(() => RequestParser.Parse("c", "to: gemini\nreply: text\n본문이 바로 옴")).Message);
}
