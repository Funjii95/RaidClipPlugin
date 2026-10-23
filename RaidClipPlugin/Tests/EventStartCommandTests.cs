using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class EventStartCommandTests
{
    [Theory]
    [InlineData(" !START boss ", "!start boss")]
    [InlineData("!START race 100", "!startrace 100")]
    [InlineData("!start race all", "!startrace all")]
    [InlineData("!start heist", "!heist")]
    [InlineData("!START mimic", "!start mimic")]
    public void StartRoutesAfterActualChatParser(string input, string expected)
    {
        var parsed = ChatMinigameService.ParseMinigameCommand(input);
        Assert.True(parsed.IsCommand);
        Assert.Equal("!start", parsed.Command);
        Assert.Equal(expected, string.Join(' ', EventStartCommand.Translate(parsed.Parts)!));
    }
    [Theory]
    [InlineData("!start boss", "!start boss")]
    [InlineData("!start race 100", "!startrace 100")]
    [InlineData("!start race all", "!startrace all")]
    [InlineData("!start HEIST", "!heist")]
    public void StartsSupportedEvents(string input, string expected) =>
        Assert.Equal(expected, string.Join(' ', EventStartCommand.Translate(input.Split(' '))!));

    [Theory]
    [InlineData("!start")]
    [InlineData("!start quiz")]
    [InlineData("!start mimic extra")]
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

    [Fact]
    public void RaceStartIsOpenToViewersButBossAndHeistAreNot()
    {
        var viewer = new ChatMessage { UserId = "viewer" };
        Assert.True(EventStartCommand.IsAllowed(viewer, "channel", new[] { "!start", "race", "100" }));
        Assert.False(EventStartCommand.IsAllowed(viewer, "channel", new[] { "!start", "boss" }));
        Assert.False(EventStartCommand.IsAllowed(viewer, "channel", new[] { "!start", "heist" }));
        Assert.False(EventStartCommand.IsAllowed(new ChatMessage { CommandAuthorization = CommandAuthorization.Denied },
            "channel", new[] { "!start", "race", "100" }));
    }
}
