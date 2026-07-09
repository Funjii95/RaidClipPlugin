using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;


namespace RaidClipPlugin.Tests;


public sealed class DuelServiceTests
{
    [Fact]
    public async Task ValidChallengeReservesStakeAndAnnouncesRequest()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.ProcessAsync(f.Message("c", "Challenger", "!duel Target 100"), default);
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
        Assert.Contains(f.Chat.Messages, x => x.Contains("@Target", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task SelfDuelIsRejectedWithoutPointChange()
    {
        await using var f = await Fixture.CreateAsync();
        f.Chat.Users["challenger"] = new TwitchUser("c", "challenger", "Challenger");
        await f.Service.ProcessAsync(f.Message("c", "Challenger", "!duel Challenger 100"), default);
        Assert.Equal(1000, await f.Points.GetPointsAsync("c", default));
        Assert.Contains(f.Chat.Messages, x => x.Contains("selbst", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task BotAndBlacklistedTargetsAreRejected()
    {
        await using var bot = await Fixture.CreateAsync(chatUserId: "t");
        await bot.Service.ProcessAsync(bot.Message("c", "Challenger", "!duel Target 100"), default);
        Assert.Equal(1000, await bot.Points.GetPointsAsync("c", default));
        await using var blocked = await Fixture.CreateAsync(blacklist: new() { "target" });
        await blocked.Service.ProcessAsync(blocked.Message("c", "Challenger", "!duel Target 100"), default);
        Assert.Equal(1000, await blocked.Points.GetPointsAsync("c", default));
    }


    [Fact]
    public async Task InsufficientChallengerPointsAreRejected()
    {
        await using var f = await Fixture.CreateAsync(challengerPoints: 50);
        await f.Service.ProcessAsync(f.Message("c", "Challenger", "!duel Target 100"), default);
        Assert.Equal(50, await f.Points.GetPointsAsync("c", default));
        Assert.Contains(f.Chat.Messages, x => x.Contains("nicht genug", StringComparison.OrdinalIgnoreCase));
    }


    [Theory]
    [InlineData("!accept")]
    [InlineData("!deny")]
    public async Task AcceptOrDenyWithoutPendingRequestGetsAnswer(string command)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.ProcessAsync(f.Message("t", "Target", command), default);
        Assert.Contains(f.Chat.Messages, x => x.Contains("keine offene", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task ChallengerCannotAcceptOwnRequest()
    {
        await using var f = await Fixture.CreateAsync();
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("c", "Challenger", "!accept"), default);
        Assert.Contains(f.Chat.Messages, x => x.Contains("Duel-Anfrage", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
    }


    [Fact]
    public async Task TargetWithoutPointsCannotAcceptAndRequestRemainsOpen()
    {
        await using var f = await Fixture.CreateAsync(targetPoints: 50);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("t", "Target", "!accept"), default);
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(50, await f.Points.GetPointsAsync("t", default));
        Assert.Contains(f.Chat.Messages, x => x.Contains("nicht genug", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task AcceptPaysCompletePotExactlyOnce()
    {
        await using var f = await Fixture.CreateAsync(randomRoll: 1);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("t", "Target", "!accept"), default);
        Assert.Equal(1100, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(900, await f.Points.GetPointsAsync("t", default));
        await f.Service.ProcessAsync(f.Message("t", "Target", "!accept"), default);
        Assert.Equal(2000, await f.TotalPointsAsync());
    }


    [Fact]
    public async Task TargetCanWinCompletePot()
    {
        await using var f = await Fixture.CreateAsync(randomRoll: 100);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("t", "Target", "!accept"), default);
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(1100, await f.Points.GetPointsAsync("t", default));
    }


    [Fact]
    public async Task DenyRefundsStakeOnlyOnce()
    {
        await using var f = await Fixture.CreateAsync();
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("t", "Target", "!deny"), default);
        await f.Service.ProcessAsync(f.Message("t", "Target", "!deny"), default);
        Assert.Equal(1000, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(1000, await f.Points.GetPointsAsync("t", default));
    }


    [Fact]
    public async Task TimeoutRefundsReservedStake()
    {
        await using var f = await Fixture.CreateAsync(timeoutSeconds: 1);
        await f.ChallengeAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(6);
        while (DateTimeOffset.UtcNow < deadline &&
               await f.Points.GetPointsAsync("c", default) != 1000)
        {
            await Task.Delay(100);
        }
        Assert.Equal(1000, await f.Points.GetPointsAsync("c", default));
        Assert.Contains(f.Chat.Messages, x => x.Contains("abgelaufen", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task CancelDuringPendingRequestRefundsStake()
    {
        await using var f = await Fixture.CreateAsync();
        await f.ChallengeAsync();
        await f.Service.CancelAllAsync(false);
        Assert.Equal(1000, await f.Points.GetPointsAsync("c", default));
    }


    [Fact]
    public async Task ParallelAcceptAndDenyPreserveTotalWithoutDoubleSettlement()
    {
        await using var f = await Fixture.CreateAsync(randomRoll: 1);
        await f.ChallengeAsync();
        await Task.WhenAll(
            f.Service.ProcessAsync(f.Message("t", "Target", "!accept"), default),
            f.Service.ProcessAsync(f.Message("t", "Target", "!deny"), default));
        Assert.Equal(2000, await f.TotalPointsAsync());
    }


    [Fact]
    public async Task UserCannotHaveSeveralOpenRequests()
    {
        await using var f = await Fixture.CreateAsync();
        f.Chat.Users["other"] = new TwitchUser("o", "other", "Other");
        await f.Points.SetPointsAsync("o", "Other", 1000, 0, default);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("c", "Challenger", "!duel Other 100"), default);
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
        Assert.Contains(f.Chat.Messages, x => x.Contains("bereits eine offene", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task SeparateUsersCanCreateIndependentRequests()
    {
        await using var f = await Fixture.CreateAsync();
        f.Chat.Users["other"] = new TwitchUser("o", "other", "Other");
        f.Chat.Users["fourth"] = new TwitchUser("f", "fourth", "Fourth");
        await f.Points.SetPointsAsync("o", "Other", 1000, 0, default);
        await f.Points.SetPointsAsync("f", "Fourth", 1000, 0, default);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(f.Message("o", "Other", "!duel Fourth 100"), default);
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(900, await f.Points.GetPointsAsync("o", default));
    }


    [Fact]
    public async Task AllInUsesAvailableBalanceWithinMaximum()
    {
        await using var f = await Fixture.CreateAsync(challengerPoints: 500);
        await f.Service.ProcessAsync(f.Message("c", "Challenger", "!duel Target all"), default);
        Assert.Equal(0, await f.Points.GetPointsAsync("c", default));
    }


    [Theory]
    [InlineData(true, 10, 50, true)]
    [InlineData(true, 90, 51, false)]
    [InlineData(false, 45, 45, true)]
    [InlineData(false, 45, 46, false)]
    public void WinRulesRespectFairAndConfiguredChance(bool fair, int chance, int roll, bool expected) =>
        Assert.Equal(expected, DuelRules.ChallengerWins(fair, chance, roll));


    [Fact]
    public async Task DisabledLoserTimeoutDoesNotCallModerationApi()
    {
        await using var f = await Fixture.CreateAsync(
            randomRoll: 1, timeoutLoser: false);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(
            f.Message("t", "Target", "!accept"), default);

        Assert.Empty(f.Chat.TimeoutCalls);
        Assert.Equal(1100, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(900, await f.Points.GetPointsAsync("t", default));
    }


    [Fact]
    public async Task EnabledLoserTimeoutTargetsLoserAfterSuccessfulPayout()
    {
        await using var f = await Fixture.CreateAsync(
            randomRoll: 1, timeoutLoser: true);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(
            f.Message("t", "Target", "!accept"), default);

        var call = Assert.Single(f.Chat.TimeoutCalls);
        Assert.Equal("t", call.UserId);
        Assert.Equal(60, call.DurationSeconds);
        Assert.Equal("Duel verloren", call.Reason);
        Assert.Equal(1100, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(900, await f.Points.GetPointsAsync("t", default));
    }


    [Fact]
    public async Task TimeoutApiFailureDoesNotRollbackDuelPayout()
    {
        await using var f = await Fixture.CreateAsync(
            randomRoll: 100, timeoutLoser: true, timeoutFails: true);
        await f.ChallengeAsync();
        await f.Service.ProcessAsync(
            f.Message("t", "Target", "!accept"), default);

        Assert.Single(f.Chat.TimeoutCalls);
        Assert.Equal("c", f.Chat.TimeoutCalls[0].UserId);
        Assert.Equal(900, await f.Points.GetPointsAsync("c", default));
        Assert.Equal(1100, await f.Points.GetPointsAsync("t", default));
    }


    [Fact]
    public void CommandCollisionsIncludeDuelModule()
    {
        var config = new AppConfig();
        config.Duel.Enabled = true;
        config.Duel.DuelCommand = "!clip";
        config.ClipCommand.Enabled = true;
        config.ClipCommand.Command = "!clip";
        var registry = new CommandRegistry();
        registry.Update(config);
        Assert.Contains(registry.FindCollisions(), x => x.Command == "!clip" && x.Message.Contains("Duel"));
    }


    [Fact]
    public void DuelCommandsAppearInRegistryAndCommandsOutputSource()
    {
        var config = new AppConfig();
        config.Duel.Enabled = true;
        var registry = new CommandRegistry();
        registry.Update(config);
        Assert.Contains(registry.Commands, x => x.CommandId == "duel.challenge" && x.Enabled);
        Assert.Contains(registry.Commands, x => x.CommandId == "duel.accept" && x.Enabled);
        Assert.Contains(registry.Commands, x => x.CommandId == "duel.deny" && x.Enabled);
    }


    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _directory;
        public ViewerPointStore Points { get; }
        public FakeDuelTwitch Chat { get; } = new();
        public DuelService Service { get; }


        private Fixture(string directory, ViewerPointStore points, DuelService service, FakeDuelTwitch chat)
        { _directory = directory; Points = points; Service = service; Chat = chat; }


        public static async Task<Fixture> CreateAsync(int challengerPoints = 1000,
            int targetPoints = 1000, int randomRoll = 1, int timeoutSeconds = 30,
            string chatUserId = "bot", List<string>? blacklist = null,
            bool timeoutLoser = false, bool timeoutFails = false)
        {
            var directory = Path.Combine(Path.GetTempPath(), "raidclip-duel-" + Guid.NewGuid().ToString("N"));
            var points = new ViewerPointStore(directory);
            await points.SetPointsAsync("c", "Challenger", challengerPoints, 0, default);
            await points.SetPointsAsync("t", "Target", targetPoints, 0, default);
            var chat = new FakeDuelTwitch { TimeoutFails = timeoutFails };
            chat.Users["target"] = new TwitchUser("t", "target", "Target");
            var config = new DuelConfig { Enabled = true, RequestTimeoutSeconds = timeoutSeconds,
                UserCooldownSeconds = 0, GlobalCooldownSeconds = 0,
                TimeoutLoserEnabled = timeoutLoser, LoserTimeoutSeconds = 60,
                LoserTimeoutReason = "Duel verloren" };
            var minigame = new MinigameConfig { MinimumPoints = 0, HistoryLimit = 500,
                PointsBlacklist = blacklist ?? new() { "nightbot" } };
            var service = new DuelService("b", chatUserId, config, minigame, chat, points,
                new FixedDuelRandom(randomRoll));
            return new Fixture(directory, points, service, chat);
        }


        public ChatMessage Message(string id, string name, string text) => new()
        { Id = Guid.NewGuid().ToString("N"), UserId = id, UserLogin = name.ToLowerInvariant(), UserName = name, Text = text };


        public Task ChallengeAsync() => Service.ProcessAsync(Message("c", "Challenger", "!duel Target 100"), default);


        public async Task<long> TotalPointsAsync() =>
            await Points.GetPointsAsync("c", default) + await Points.GetPointsAsync("t", default);


        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }


    private sealed record TimeoutCall(string UserId, int DurationSeconds, string Reason);


    private sealed class FakeDuelTwitch : IDuelTwitchClient, IDuelModerationClient
    {
        public Dictionary<string, TwitchUser> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Messages { get; } = new();
        public List<TimeoutCall> TimeoutCalls { get; } = new();
        public bool TimeoutFails { get; init; }
        public Task<TwitchUser?> GetUserAsync(string login, CancellationToken cancellationToken) =>
            Task.FromResult(Users.TryGetValue(login, out var user) ? user : null);
        public Task SendChatMessageAsync(string broadcasterId, string senderId, string message,
            CancellationToken cancellationToken) { lock (Messages) Messages.Add(message); return Task.CompletedTask; }
        public Task<bool> IsFollowerAsync(string broadcasterId, string userId,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task TimeoutUserAsync(string broadcasterId, string moderatorId,
            string userId, int durationSeconds, string reason,
            CancellationToken cancellationToken)
        {
            TimeoutCalls.Add(new TimeoutCall(userId, durationSeconds, reason));
            return TimeoutFails
                ? Task.FromException(new HttpRequestException("Testfehler"))
                : Task.CompletedTask;
        }
    }


    private sealed class FixedDuelRandom(int roll) : IDuelRandom
    {
        public int NextInclusive(int minimum, int maximum) => Math.Clamp(roll, minimum, maximum);
    }
}
