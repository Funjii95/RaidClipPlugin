using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class EventStartCommandTests
{
    [Theory]
    [InlineData(" !START boss ", "!boss")]
    [InlineData("!START rennen 100", "!rennen 100")]
    [InlineData("!start race all", "!rennen all")]
    [InlineData("!start heist", "!heist")]
    public void StartRoutesAfterActualChatParser(string input, string expected)
    {
        var parsed = ChatMinigameService.ParseMinigameCommand(input);
        Assert.True(parsed.IsCommand);
        Assert.Equal("!start", parsed.Command);
        Assert.Equal(expected, string.Join(' ', EventStartCommand.Translate(parsed.Parts)!));
    }
    [Theory]
    [InlineData("!start boss", "!boss")]
    [InlineData("!start rennen 100", "!rennen 100")]
    [InlineData("!start race all", "!rennen all")]
    [InlineData("!start HEIST", "!heist")]
    public void StartsSupportedEvents(string input, string expected) =>
        Assert.Equal(expected, string.Join(' ', EventStartCommand.Translate(input.Split(' '))!));

    [Theory]
    [InlineData("!start")]
    [InlineData("!start quiz")]
    [InlineData("!start boss extra")]
    [InlineData("!start rennen")]
    public void RejectsInvalidSyntax(string input) => Assert.Null(EventStartCommand.Translate(input.Split(' ')));

    [Fact]
    public void StartIsModeratorOnlyAndDenialCannotBeOverridden()
    {
        Assert.False(EventStartCommand.IsAllowed(new ChatMessage { UserId = "viewer" }, "channel"));
        Assert.True(EventStartCommand.IsAllowed(new ChatMessage { IsModerator = true }, "channel"));
        Assert.True(EventStartCommand.IsAllowed(new ChatMessage { UserId = "channel", IsBot = true }, "channel"));
        Assert.False(EventStartCommand.IsAllowed(new ChatMessage { IsModerator = true, CommandAuthorization = CommandAuthorization.Denied }, "channel"));
    }
}
