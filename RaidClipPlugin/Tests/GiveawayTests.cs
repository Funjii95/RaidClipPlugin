using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class GiveawayTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "raidclip-giveaway-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("!giveaway", 0)]
    [InlineData("!JOIN", 0)]
    [InlineData(" !giveaway   3 ", 3)]
    public void JoinCommandsAreDetected(string text, int tickets)
    {
        Assert.True(GiveawayService.TryParseJoinCommand(text, new GiveawayConfig(), out var actual));
        Assert.Equal(tickets, actual);
    }

    [Fact]
    public async Task StartJoinAndEndPersistsState()
    {
        var fixture = Create();
        Assert.True((await fixture.Service.StartAsync(default)).Success);
        Assert.True((await fixture.Service.JoinAsync(Message("1", "alice"), 0, default)).Success);
        Assert.True((await fixture.Service.EndAsync(default)).Success);
        var state = await fixture.Store.LoadAsync(default);
        Assert.Equal(GiveawayStatus.Ended, state.Status);
        Assert.Single(state.Participants);
    }

    [Fact]
    public async Task DuplicateAndBlacklistAreRejected()
    {
        var fixture = Create(configure: g => g.BlockedUsers.Add("blocked"));
        await fixture.Service.StartAsync(default);
        Assert.False((await fixture.Service.JoinAsync(Message("9", "blocked"), 0, default)).Success);
        Assert.True((await fixture.Service.JoinAsync(Message("1", "alice"), 0, default)).Success);
        Assert.False((await fixture.Service.JoinAsync(Message("1", "alice"), 0, default)).Success);
    }

    [Fact]
    public async Task EntryCostIsDeductedAndRefundedOnCancel()
    {
        var fixture = Create(configure: g => { g.EntryCost = 25; g.RefundPointsOnCancel = true; });
        await fixture.Points.AddPointsAsync("1", "Alice", 100, 0, default);
        await fixture.Service.StartAsync(default);
        Assert.True((await fixture.Service.JoinAsync(Message("1", "alice"), 0, default)).Success);
        Assert.Equal(75, await fixture.Points.GetPointsAsync("1", default));
        await fixture.Service.CancelAsync(default);
        Assert.Equal(100, await fixture.Points.GetPointsAsync("1", default));
    }

    [Fact]
    public async Task SecureDrawProducesDistinctWinners()
    {
        var fixture = Create(configure: g => { g.MaximumWinners = 2; g.AutoCloseAfterDraw = false; });
        await fixture.Service.StartAsync(default);
        await fixture.Service.JoinAsync(Message("1", "alice"), 0, default);
        await fixture.Service.JoinAsync(Message("2", "bob"), 0, default);
        var result = await fixture.Service.DrawConfiguredAsync(default);
        Assert.True(result.Success);
        Assert.Equal(2, result.Winners!.Select(x => x.UserId).Distinct().Count());
    }

    [Fact]
    public async Task ActiveGiveawayIsRestoredAfterRestart()
    {
        var first = Create();
        await first.Service.StartAsync(default);
        await first.Service.JoinAsync(Message("1", "alice"), 0, default);
        first.Service.Dispose();
        var restored = new GiveawayService("b", "s", first.Config, new MinigameConfig(),
            first.Twitch, first.Chat, first.Points, first.Store);
        await restored.InitializeAsync(default);
        var state = await restored.GetStateAsync(default);
        Assert.Equal(GiveawayStatus.Active, state.Status);
        Assert.Single(state.Participants);
        restored.Dispose();
    }

    [Fact]
    public async Task ExpiredGiveawayIsAutomaticallyDrawnOnRestore()
    {
        var fixture = Create();
        await fixture.Store.SaveAsync(new GiveawayRuntimeState
        {
            Id = "old", Status = GiveawayStatus.Active, Title = "Test", Prize = "Preis",
            Command = "!giveaway", StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            EndsAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Participants = new() { new GiveawayParticipant { UserId="1", UserLogin="alice", DisplayName="Alice", IsValid=true } }
        }, default);
        await fixture.Service.InitializeAsync(default);
        var state = await fixture.Service.GetStateAsync(default);
        Assert.Equal(GiveawayStatus.Ended, state.Status);
        Assert.Single(state.Winners);
    }

    [Fact]
    public async Task ChatTemplatesReplacePlaceholders()
    {
        var fixture = Create(configure: g => g.ChatMessages.Started.Text = "{title}|{prize}|{command}");
        await fixture.Service.StartAsync(default);
        Assert.Contains("Community Giveaway|Überraschung|!giveaway", fixture.Chat.Messages);
    }

    [Fact]
    public void InvalidAndCollidingCommandsAreRejected()
    {
        var config = new GiveawayConfig { Command = "punkte ohne ausrufezeichen" };
        Assert.Throws<InvalidOperationException>(() => ConfigurationService.ValidateGiveawaySettings(config));
    }

    private Fixture Create(Action<GiveawayConfig>? configure = null)
    {
        Directory.CreateDirectory(_root);
        var config = new GiveawayConfig { Enabled = true, LiveOnly = false };
        configure?.Invoke(config);
        var twitch = new FakeGiveawayTwitch();
        var chat = new FakeChat();
        var points = new ViewerPointStore(Path.Combine(_root, "points"));
        var store = new GiveawayStore(Path.Combine(_root, "state.json"));
        var service = new GiveawayService("b", "s", config, new MinigameConfig(), twitch, chat, points, store);
        return new Fixture(service, config, twitch, chat, points, store);
    }

    private static ChatMessage Message(string id, string login) => new()
    { UserId=id, UserLogin=login, UserName=char.ToUpperInvariant(login[0])+login[1..], Text="!giveaway" };

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    private sealed record Fixture(GiveawayService Service, GiveawayConfig Config,
        FakeGiveawayTwitch Twitch, FakeChat Chat, ViewerPointStore Points, GiveawayStore Store);

    private sealed class FakeChat : IClipChatClient
    {
        public List<string> Messages { get; } = new();
        public Task SendChatMessageAsync(string broadcasterId, string senderId, string message,
            CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class FakeGiveawayTwitch : IGiveawayTwitchClient
    {
        public DateTimeOffset? FollowedAt { get; set; } = DateTimeOffset.UtcNow.AddYears(-1);
        public Task<TwitchLiveStream?> GetLiveStreamAsync(string broadcasterId, CancellationToken cancellationToken) =>
            Task.FromResult<TwitchLiveStream?>(new("stream", broadcasterId, "channel", "Channel", "g", "Game", DateTimeOffset.UtcNow, true));
        public Task<DateTimeOffset?> GetFollowedAtAsync(string broadcasterId, string userId,
            CancellationToken cancellationToken) => Task.FromResult(FollowedAt);
        public Task<TwitchUser?> GetUserAsync(string login, CancellationToken cancellationToken) =>
            Task.FromResult<TwitchUser?>(new(login, login, login));
    }
}
