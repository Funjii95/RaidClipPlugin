using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;


namespace RaidClipPlugin.Tests;


public sealed class SharedChatCommandGuardTests
{
    [Fact]
    public void CommandWithoutSourceOriginIsAllowed()
    {
        var decision = new SharedChatCommandGuard("current").Evaluate(
            Message("!gamble 100", broadcasterId: "current"));
        Assert.True(decision.Allowed);
        Assert.True(decision.IsCommand);
    }


    [Fact]
    public void CommandFromCurrentChannelIsAllowed()
    {
        var decision = new SharedChatCommandGuard("current").Evaluate(
            Message("!clip", broadcasterId: "current", sourceBroadcasterId: "current"));
        Assert.True(decision.Allowed);
    }


    [Fact]
    public void CommandFromForeignSharedChatChannelIsBlocked()
    {
        var decision = new SharedChatCommandGuard("current").Evaluate(
            Message("!duel target 100", broadcasterId: "current", sourceBroadcasterId: "foreign"));
        Assert.False(decision.Allowed);
        Assert.Equal("!duel", decision.Command);
        Assert.Equal("foreign", decision.SourceRoomId);
        Assert.Equal("current", decision.CurrentRoomId);
    }


    [Fact]
    public void ForeignNormalChatMessageRemainsAllowedForDisplay()
    {
        var decision = new SharedChatCommandGuard("current").Evaluate(
            Message("Hallo aus dem Shared Chat", broadcasterId: "current", sourceBroadcasterId: "foreign"));
        Assert.True(decision.Allowed);
        Assert.False(decision.IsCommand);
    }


    [Fact]
    public void ProtectionCanBeDisabledExplicitly()
    {
        var decision = new SharedChatCommandGuard("current", false).Evaluate(
            Message("!punkte", broadcasterId: "current", sourceBroadcasterId: "foreign"));
        Assert.True(decision.Allowed);
    }


    [Fact]
    public void SourceRoomIdFallbackIsProtected()
    {
        var decision = new SharedChatCommandGuard("current").Evaluate(new ChatMessage
        {
            UserId = "u",
            UserName = "Viewer",
            Text = "!join",
            RoomId = "current",
            SourceRoomId = "foreign"
        });
        Assert.False(decision.Allowed);
    }


    private static ChatMessage Message(
        string text,
        string broadcasterId,
        string sourceBroadcasterId = "") => new()
    {
        UserId = "u",
        UserName = "Viewer",
        Text = text,
        BroadcasterUserId = broadcasterId,
        SourceBroadcasterUserId = sourceBroadcasterId
    };
}
