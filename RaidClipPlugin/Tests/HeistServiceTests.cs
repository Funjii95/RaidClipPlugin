using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class HeistServiceTests
{
    [Fact]
    public async Task BroadcasterUsingBotAccountCanStartHeist()
    {
        await using var fixture = new HeistFixture(new HeistConfig { Enabled = true, JoinDurationSeconds = 10 });
        await fixture.Service.StartAsync(Message("broadcaster", broadcaster: true), default);
        Assert.Contains(fixture.Chat.Messages, message => message.Contains("plant einen Heist", StringComparison.Ordinal));
        Assert.Equal(HeistState.Joining, fixture.Service.State);
    }

    [Fact]
    public async Task ModeratorCanStartEvenWhenViewerRolesAreDisabled()
    {
        var config = new HeistConfig
        {
            Enabled = true,
            JoinDurationSeconds = 10,
            AllowEveryone = false,
            AllowFollowers = false,
            AllowSubscribers = false,
            AllowVips = false,
            AllowModerators = false
        };
        await using var fixture = new HeistFixture(config);
        await fixture.Service.StartAsync(Message("moderator", moderator: true), default);
        Assert.Contains(fixture.Chat.Messages, message => message.StartsWith("@moderator plant", StringComparison.Ordinal));
        Assert.Equal(HeistState.Joining, fixture.Service.State);
    }

    [Fact]
    public async Task BroadcasterCanJoinHeistStartedByModerator()
    {
        await using var fixture = new HeistFixture(new HeistConfig { Enabled = true, JoinDurationSeconds = 10 });
        await fixture.Service.StartAsync(Message("moderator", moderator: true), default);
        fixture.Chat.Messages.Clear();
        await fixture.Service.ProcessAsync(Message("broadcaster", broadcaster: true, text: "!join"), "!join", default);
        Assert.Contains(fixture.Chat.Messages, message => message.StartsWith("@broadcaster nimmt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisabledHeistCommandExplainsWhyItCannotStart()
    {
        await using var fixture = new HeistFixture(new HeistConfig { Enabled = false });
        await fixture.Service.ProcessAsync(Message("viewer"), "!heist", default);
        Assert.Single(fixture.Chat.Messages);
        Assert.Contains("deaktiviert", fixture.Chat.Messages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestHeistSendsThreeMarkedMessagesWithoutChangingJackpot()
    {
        await using var fixture = new HeistFixture(new HeistConfig { Enabled = true });
        var before = await fixture.Points.GetJackpotAsync(1000, default);
        await fixture.Service.RunTestAsync(default);
        var after = await fixture.Points.GetJackpotAsync(1000, default);
        Assert.Equal(3, fixture.Chat.Messages.Count);
        Assert.All(fixture.Chat.Messages, message => Assert.StartsWith("[TEST]", message));
        Assert.Contains("Keine echte Auszahlung", fixture.Chat.Messages[2], StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    private static ChatMessage Message(string name, bool broadcaster = false, bool moderator = false, string text = "!heist") => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        UserId = broadcaster ? "b" : name,
        UserLogin = name,
        UserName = name,
        Text = text,
        IsBroadcaster = broadcaster,
        IsModerator = moderator
    };

    private sealed class HeistFixture : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "raidclip-heist-service-" + Guid.NewGuid().ToString("N"));
        public FakeHeistChat Chat { get; } = new();
        public ViewerPointStore Points { get; }
        public HeistService Service { get; }

        public HeistFixture(HeistConfig config)
        {
            Points = new ViewerPointStore(_directory);
            Service = new HeistService("b", "b", config, new MinigameConfig(), Chat, Points, new FixedRandom());
        }

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }

    private sealed class FakeHeistChat : IHeistTwitchClient
    {
        public List<string> Messages { get; } = new();
        public Task SendChatMessageAsync(string broadcasterId, string senderId, string message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public Task<bool> IsFollowerAsync(string broadcasterId, string userId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FixedRandom : IHeistRandom
    {
        public int NextInclusive(int minimum, int maximum) => Math.Clamp(42, minimum, maximum);
    }
}
