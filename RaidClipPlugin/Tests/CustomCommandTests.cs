using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CustomCommandTests
{
    [Theory]
    [InlineData("!command")]
    [InlineData("!commands")]
    public async Task BroadcasterUsingSendingAccountStillReceivesCommandList(string text)
    {
        var registry = new CommandRegistry();
        registry.Update(new AppConfig());
        var fake = new FakeTwitch();
        var service = new CommandListService(registry, "channel", "channel", fake, fake, _ => false);
        var message = new ChatMessage { UserId = "channel", UserLogin = "funjii",
            UserName = "Funjii", IsBroadcaster = true, IsBot = true, Text = text };
        Assert.True(await service.HandleMessageAsync(message, CancellationToken.None));
        var response = Assert.Single(fake.Messages);
        Assert.Contains("Deine Befehle", response);
        Assert.False(await service.HandleMessageAsync(new ChatMessage { UserId = "channel",
            IsBot = true, Text = response }, CancellationToken.None));
        Assert.Single(fake.Messages);
    }
    [Fact]
    public async Task CommandDirectoryHasOneDefinitionAndHonorsConfiguredAliasAndDisable()
    {
        var config = new AppConfig();
        config.Commands.Command = "!hilfe";
        var registry = new CommandRegistry();
        registry.Update(config);
        var definition = Assert.Single(registry.Commands.Where(c => c.CommandId == "commands.list"));
        Assert.Equal("!command", definition.CommandText);
        Assert.Contains("!commands", definition.Aliases);
        Assert.Contains("!hilfe", definition.Aliases);
        Assert.Empty(registry.FindCollisions(false).Where(c => c.Command == "!commands"));
        var fake = new FakeTwitch();
        var service = new CommandListService(registry, "channel", "bot", fake, fake, _ => false);
        Assert.True(await service.HandleMessageAsync(Message("!hilfe"), CancellationToken.None));
        Assert.Single(fake.Messages);
        config.Commands.Enabled = false;
        registry.Update(config);
        var disabledFake = new FakeTwitch();
        var disabled = new CommandListService(registry, "channel", "bot", disabledFake, disabledFake, _ => false);
        Assert.True(await disabled.HandleMessageAsync(Message("!command"), CancellationToken.None));
        Assert.Empty(disabledFake.Messages);
    }
    [Fact]
    public async Task CommandListFiltersDisabledAndModeratorCommandsAndIncludesCustom()
    {
        var config = new AppConfig();
        config.Minigame.PointsEnabled = true;
        config.Commands.CustomCommandsEnabled = true;
        config.Commands.CustomCommands.Add(new() { Id = "hello", Enabled = true, Command = "!hello", Response = "Hi" });
        config.Commands.CommandEnabledOverrides["points.daily"] = false;
        var registry = new CommandRegistry();
        registry.Update(config);
        var fake = new FakeTwitch();
        var service = new CommandListService(registry, "channel", "bot", fake, fake, _ => false);
        var available = await service.AvailableAsync(Message("!command"), CancellationToken.None);
        Assert.Contains(available, c => c.CommandText == "!command");
        Assert.Contains(available, c => c.CommandText == "!hello");
        Assert.DoesNotContain(available, c => c.CommandText == "!addpoints" || c.CommandText == "!daily");
    }

    [Fact]
    public async Task CommandListPermitOnlyAddsRegularMusicRequest()
    {
        var config = new AppConfig();
        config.MusicRequests.Enabled = true;
        config.MusicRequests.ChatCommandEnabled = true;
        var registry = new CommandRegistry();
        registry.Update(config);
        var fake = new FakeTwitch();
        var denied = new CommandListService(registry, "channel", "bot", fake, fake, _ => false);
        Assert.DoesNotContain(await denied.AvailableAsync(Message("!command"), CancellationToken.None), c => c.ModuleId == "music");
        var permitted = new CommandListService(registry, "channel", "bot", fake, fake, _ => true);
        var available = await permitted.AvailableAsync(Message("!command"), CancellationToken.None);
        Assert.Contains(available, c => c.CommandId == "music.request");
        Assert.DoesNotContain(available, c => c.ModuleId == "music" && c.CommandId != "music.request");
    }

    [Fact]
    public async Task CommandListAnswersOnceAndSupportsAlias()
    {
        var registry = new CommandRegistry();
        registry.Update(new AppConfig());
        var fake = new FakeTwitch();
        var service = new CommandListService(registry, "channel", "bot", fake, fake, _ => false);
        Assert.False(await service.HandleMessageAsync(Message("!commandfoo"), CancellationToken.None));
        Assert.True(await service.HandleMessageAsync(Message("!commands"), CancellationToken.None));
        Assert.True(await service.HandleMessageAsync(Message("!command"), CancellationToken.None));
        Assert.Contains("Deine Befehle", Assert.Single(fake.Messages));
        Assert.True(fake.Messages[0].Length <= 480);
    }

    [Fact]
    public async Task CommandListChecksFollowerRoleAndPaginatesWithoutLosingCommands()
    {
        var config = new AppConfig();
        config.Commands.CustomCommandsEnabled = true;
        config.Commands.CustomCommands.Add(new() { Id = "followers", Enabled = true,
            Command = "!followersonly", RequiredRole = "Follower", Response = "Hi" });
        for (var i = 0; i < 80; i++)
            config.Commands.CustomCommands.Add(new() { Id = "c" + i, Enabled = true,
                Command = "!example" + i, Response = "Hi" });
        var registry = new CommandRegistry();
        registry.Update(config);
        var fake = new FakeTwitch();
        var service = new CommandListService(registry, "channel", "bot", fake, fake, _ => false);
        var available = await service.AvailableAsync(Message("!command"), CancellationToken.None);
        Assert.DoesNotContain(available, c => c.CommandText == "!followersonly");
        var pages = CommandListService.BuildPages(available);
        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.True(page.Length <= 350));
        var all = string.Join(" | ", pages).Split(" | ");
        Assert.All(available, command => Assert.Contains(command.Usage, all));
    }

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
