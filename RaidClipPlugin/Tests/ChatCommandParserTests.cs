using Xunit;
using RaidClipPlugin.Services;

namespace RaidClipPlugin.Tests;

public sealed class ChatCommandParserTests
{
    [Theory]
    [InlineData("!punkte", "punkte", "")]
    [InlineData("  !GAMBLE all  ", "gamble", "all")]
    [InlineData("!songrequest https://open.spotify.com/track/abc", "songrequest", "https://open.spotify.com/track/abc")]
    public void Parse_NormalizesCommands(
        string input, string expectedCommand, string expectedArguments)
    {
        var result = ChatCommandParser.Parse(input);

        Assert.True(result.IsCommand);
        Assert.Equal("!", result.Prefix);
        Assert.Equal(expectedCommand, result.Command);
        Assert.Equal(expectedArguments, result.Arguments);
    }

    [Fact]
    public void Parse_IsCaseInsensitiveForCustomPrefix()
    {
        var result = ChatCommandParser.Parse("  RC Punkte  ", "rc");

        Assert.True(result.IsCommand);
        Assert.Equal("punkte", result.Command);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nur text")]
    [InlineData("!")]
    public void Parse_ExplainsIgnoredMessages(string input)
    {
        var result = ChatCommandParser.Parse(input);

        Assert.False(result.IsCommand);
        Assert.False(string.IsNullOrWhiteSpace(result.IgnoreReason));
    }
}
