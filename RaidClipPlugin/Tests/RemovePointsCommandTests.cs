using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RemovePointsCommandTests
{
    [Fact]
    public void BroadcasterBadge_IsAllowed()
    {
        var message = new ChatMessage
        {
            UserId = "broadcaster",
            IsBroadcaster = true
        };

        Assert.True(ChatMinigameService.CanUseRemovePointsCommand(
            message, "other-id"));
    }

    [Fact]
    public void BroadcasterId_IsAllowed_WhenBadgeIsMissing()
    {
        var message = new ChatMessage { UserId = "123" };

        Assert.True(ChatMinigameService.CanUseRemovePointsCommand(
            message, "123"));
    }

    [Fact]
    public void Moderator_IsNotAllowed()
    {
        var message = new ChatMessage
        {
            UserId = "moderator",
            IsModerator = true
        };

        Assert.False(ChatMinigameService.CanUseRemovePointsCommand(
            message, "broadcaster"));
    }
}
