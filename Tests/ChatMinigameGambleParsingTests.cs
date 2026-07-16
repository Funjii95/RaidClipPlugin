using RaidClipPlugin.Services;

namespace RaidClipPlugin.Tests;

public sealed class ChatMinigameGambleParsingTests
{
    [Theory]
    [InlineData("!gamble 100", "!gamble", "100", 1)]
    [InlineData("!gamble     100", "!gamble", "100", 1)]
    [InlineData("!gamble\t100", "!gamble", "100", 1)]
    [InlineData("!GAMBLE 100", "!gamble", "100", 1)]
    [InlineData("!gamble all", "!gamble", "all", 1)]
    [InlineData("!gamble ALL", "!gamble", "ALL", 1)]
    [InlineData(" !gamble\u00A0100 ", "!gamble", "100", 1)]
    [InlineData("\u200B!gamble\u200D 100", "!gamble", "100", 1)]
    public void ParseMinigameCommand_NormalizesSupportedGambleInputs(
        string raw,
        string expectedCommand,
        string expectedArguments,
        int expectedArgumentCount)
    {
        var parsed = ChatMinigameService.ParseMinigameCommand(raw);

        Assert.True(parsed.IsCommand);
        Assert.Equal(expectedCommand, parsed.Command);
        Assert.Equal(expectedArguments, parsed.Arguments);
        Assert.Equal(expectedArgumentCount, parsed.ArgumentCount);
    }

    [Theory]
    [InlineData("100", false, 100)]
    [InlineData("all", true, 0)]
    [InlineData("ALL", true, 0)]
    [InlineData("\t100", false, 100)]
    [InlineData("\u00A0100\u00A0", false, 100)]
    [InlineData("\u200B100\u200D", false, 100)]
    public void ParseGambleArgument_AcceptsValidArguments(
        string rawArgument,
        bool expectedAllIn,
        long expectedStake)
    {
        var parsed = ChatMinigameService.ParseGambleArgument(rawArgument);

        Assert.True(parsed.IsValid);
        Assert.Equal(expectedAllIn, parsed.IsAllIn);
        Assert.Equal(expectedStake, parsed.Stake);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("nope")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100 200")]
    public void ParseGambleArgument_RejectsInvalidArgumentsExactlyOnce(
        string rawArgument)
    {
        var parsed = ChatMinigameService.ParseGambleArgument(rawArgument);

        Assert.False(parsed.IsValid);
    }
}
