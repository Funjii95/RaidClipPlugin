using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CustomCommandTests
{
    [Fact]
    public async Task RaidExampleSendsExactConfiguredResponse()
    {
        var fake = new FakeTwitch();
        var config = Config(new CustomChatCommandConfig
        {
            Enabled = true,
            Command = "!raid",
            Response = "funjiiRaid Funjii's Otter-Familie ist angekommen! Viel Liebe und gute Vibes für den Stream! funjiiRaid",
            UserCooldownSeconds = 0,
            GlobalCooldownSeconds = 0
        });
        var service = new CustomCommandService("channel", "bot", config, fake, fake);
        Assert.True(await service.HandleMessageAsync(Message("!raid"), CancellationToken.None));
        Assert.Equal(config.CustomCommands[0].Response, Assert.Single(fake.Messages));
    }

    [Fact]
    public async Task PlaceholdersAndAliasesAreSupported()
    {
        var fake = new FakeTwitch();
        var config = Config(new CustomChatCommandConfig
        {
            Enabled = true, Command = "!hello", Aliases = new() { "!hi" },
            Response = "@{user} grüßt {target}: {args}",
            UserCooldownSeconds = 0, GlobalCooldownSeconds = 0
        });
        var service = new CustomCommandService("channel", "bot", config, fake, fake);
        await service.HandleMessageAsync(Message("!hi @Otter gute Vibes"), CancellationToken.None);
        Assert.Equal("@Viewer grüßt Otter: @Otter gute Vibes", Assert.Single(fake.Messages));
    }

    [Fact]
    public async Task ExplicitRoleOverrideIsActuallyEnforced()
    {
        var fake = new FakeTwitch();
        var config = new AppConfig();
        config.Minigame.PointsEnabled = true;
        config.Commands.CommandRoleOverrides["points.add"] = "Broadcaster";
        var registry = new CommandRegistry();
        registry.Update(config);
        var service = new CommandPermissionService("channel", "bot",
            config.Commands, fake, fake, registry);
        var message = Message("!addpoints @Otter 10");
        await service.AuthorizeAsync(message, CancellationToken.None);
        Assert.Equal(CommandAuthorization.Denied, message.CommandAuthorization);
        Assert.Contains("nicht verwenden", Assert.Single(fake.Messages));
    }

    [Fact]
    public void DuplicateCustomCommandsAreReportedAsCollision()
    {
        var config = new AppConfig();
        config.Commands.CustomCommands = new()
        {
            new() { Id = "one", Enabled = true, Command = "!same", Response = "1" },
            new() { Id = "two", Enabled = true, Command = "!same", Response = "2" }
        };
        var registry = new CommandRegistry();
        registry.Update(config);
        Assert.Contains(registry.FindCollisions(false), item => item.Command == "!same");
    }

    [Fact]
    public void SubscriberAndVipHaveNoAutomaticGiveawayAdvantage()
    {
        var viewer = new GiveawayParticipant();
        var subscriberVip = new GiveawayParticipant { IsSubscriber = true, IsVip = true };
        Assert.Equal(GiveawayService.CalculateTicketWeight(viewer),
            GiveawayService.CalculateTicketWeight(subscriberVip));
        subscriberVip.ExtraTickets = 2;
        Assert.Equal(3, GiveawayService.CalculateTicketWeight(subscriberVip));
    }

    private static CommandsConfig Config(CustomChatCommandConfig command) => new()
    {
        CustomCommandsEnabled = true,
        CustomCommands = new() { command }
    };

    private static ChatMessage Message(string text) => new()
    {
        UserId = "viewer", UserLogin = "viewer", UserName = "Viewer", Text = text
    };

    private sealed class FakeTwitch : IClipChatClient, ITwitchClipClient
    {
        public List<string> Messages { get; } = new();
        public Task SendChatMessageAsync(string broadcasterId, string senderId,
            string message, CancellationToken cancellationToken)
        { Messages.Add(message); return Task.CompletedTask; }
        public Task<bool> IsFollowerAsync(string broadcasterId, string userId,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<TwitchCreatedClip> CreateClipAsync(TwitchClipRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PublishedClip?> GetClipByIdAsync(string clipId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TwitchLiveStream?> GetLiveStreamAsync(string broadcasterId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
