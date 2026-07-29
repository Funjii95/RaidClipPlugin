using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class ModerationCenterTests
{
    [Fact]
    public void LinkDetectionFindsCommonAndObfuscatedLinks()
    {
        var detector = new LinkDetectionService();
        var result = detector.Detect("schau https://example.com und test dot gg", new LinkFilterConfig());
        Assert.Contains(result.Links, link => link.Domain == "example.com");
        Assert.Contains(result.Links, link => link.Domain == "test.gg" && link.IsObfuscated);
    }

    [Theory]
    [InlineData("Version 1.2.4")]
    [InlineData("Preis 12.50")]
    [InlineData("Datei test.exe")]
    public void LinkDetectionDoesNotFlagVersionsNumbersOrFiles(string text)
    {
        var detector = new LinkDetectionService();
        Assert.False(detector.Detect(text, new LinkFilterConfig()).HasLinks);
    }

    [Fact]
    public void PermitIsChannelScopedAndSingleUse()
    {
        var permits = new PermitService();
        permits.Grant("channel-a", "A", "", "viewer", "viewer", "mod", "Mod", TimeSpan.FromMinutes(1), PermitMode.SingleMessage, "Test");
        Assert.False(permits.TryConsume("channel-b", "", "viewer", "m1", 1, out _, out _));
        Assert.True(permits.TryConsume("channel-a", "", "viewer", "m2", 1, out _, out _));
        Assert.False(permits.TryConsume("channel-a", "", "viewer", "m3", 1, out _, out _));
    }

    [Fact]
    public void SingleLinkPermitRejectsMultipleLinks()
    {
        var permits = new PermitService();
        permits.Grant("channel", "C", "", "viewer", "viewer", "mod", "Mod", TimeSpan.FromMinutes(1), PermitMode.SingleLink, "Test");
        Assert.False(permits.TryConsume("channel", "", "viewer", "m1", 2, out _, out var reason));
        Assert.Contains("einen Link", reason);
    }

    [Fact]
    public void ModerationCommandsAreRegisteredForModerators()
    {
        var config = new AppConfig { Moderation = { Enabled = true } };
        var registry = new CommandRegistry();
        registry.Update(config);
        Assert.Contains(registry.Commands, command => command.CommandId == "moderation.permit" && command.RequiredRole == CommandRole.Moderator);
        Assert.Contains(registry.Commands, command => command.CommandId == "moderation.unpermit" && command.RequiredRole == CommandRole.Moderator);
    }

    [Fact]
    public async Task LinkFilterBlocksLinksEvenWhenGeneralModerationIsDisabled()
    {
        var historyPath = Path.Combine(
            Path.GetTempPath(),
            "raidclip-moderation-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var service = new LinkModerationService(
            new PermitService(),
            new ModerationHistoryService(historyPath));
        var config = new AppConfig();
        config.Moderation.Enabled = false;
        config.Moderation.LinkFilter.Enabled = true;
        config.Moderation.LinkFilter.Action = LinkModerationAction.LogOnly;
        config.Moderation.LinkFilter.BotResponseEnabled = false;

        var message = new ChatMessage
        {
            Id = "message-1",
            UserId = "user-1",
            UserLogin = "viewer",
            UserName = "Viewer",
            Text = "schau mal example.com"
        };

        await service.ProcessAsync(
            message,
            config,
            moderation: null,
            new TwitchService("client", "token"),
            "channel-1",
            "channel",
            "moderator-1",
            _ => false,
            _ => { },
            CancellationToken.None);

        Assert.Equal(CommandAuthorization.Denied, message.CommandAuthorization);
        Assert.True(File.Exists(historyPath));
    }

    [Fact]
    public async Task VipLinkFilterExemptionCanBeDisabled()
    {
        var service = new LinkModerationService(
            new PermitService(),
            new ModerationHistoryService(Path.Combine(
                Path.GetTempPath(),
                "raidclip-moderation-" + Guid.NewGuid().ToString("N") + ".jsonl")));
        var config = new AppConfig();
        config.Moderation.LinkFilter.Enabled = true;
        config.Moderation.LinkFilter.Action = LinkModerationAction.LogOnly;
        config.Moderation.LinkFilter.BotResponseEnabled = false;

        var exemptMessage = CreateVipLinkMessage("message-exempt");
        await service.ProcessAsync(
            exemptMessage,
            config,
            moderation: null,
            new TwitchService("client", "token"),
            "channel-1",
            "channel",
            "moderator-1",
            _ => false,
            _ => { },
            CancellationToken.None);

        Assert.NotEqual(CommandAuthorization.Denied, exemptMessage.CommandAuthorization);

        config.Moderation.LinkFilter.ExemptVips = false;
        var blockedMessage = CreateVipLinkMessage("message-blocked");
        await service.ProcessAsync(
            blockedMessage,
            config,
            moderation: null,
            new TwitchService("client", "token"),
            "channel-1",
            "channel",
            "moderator-1",
            _ => false,
            _ => { },
            CancellationToken.None);

        Assert.Equal(CommandAuthorization.Denied, blockedMessage.CommandAuthorization);
    }

    private static ChatMessage CreateVipLinkMessage(string id) => new()
    {
        Id = id,
        UserId = "vip-1",
        UserLogin = "vipviewer",
        UserName = "VipViewer",
        Text = "schau mal example.com",
        IsVip = true
    };
}
