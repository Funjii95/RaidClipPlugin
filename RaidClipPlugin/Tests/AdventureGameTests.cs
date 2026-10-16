using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class AdventureGameTests
{
    [Fact]
    public async Task FishingCooldownShowsRemainingTimeWithoutExtendingOrChargingAgain()
    {
        using var f = new Fixture(1); await f.Seed("a");
        f.Config.Adventures.Fishing.CooldownSeconds = 30;
        await f.Send("a", "!fischen 100");
        f.Now = f.Now.AddSeconds(10);
        await f.Send("a", "!fischen 100");
        Assert.Contains("20 Sekunden", f.Messages.Last());
        Assert.Equal(900, await f.Balance("a"));
        f.Now = f.Now.AddSeconds(20);
        await f.Send("a", "!fischen 100");
        Assert.Equal(800, await f.Balance("a"));
        Assert.DoesNotContain("wieder verfügbar", f.Messages.Last());
    }
    [Fact]
    public async Task PluginRaceStartUsesConnectedBroadcasterNameAndId()
    {
        using var f = new Fixture(); await f.Seed("42812664");
        var messages = new List<string>();
        var service = new AdventureGameService(f.Points, f.Config,
            (text, _) => { messages.Add(text); return Task.CompletedTask; }, broadcasterId: "42812664");
        await service.ControlAsync("race", 100, default, new TwitchUser("42812664", "funjii", "Funjii"));
        Assert.Contains(messages, m => m.Contains("@Funjii ist im Rennen"));
        Assert.DoesNotContain(messages, m => m.Contains("@Streamer"));
        Assert.Equal(1, service.Status.RacePlayers);
        Assert.Equal(1000, await f.Balance("42812664"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ControlAsync("race", 100, default,
            new TwitchUser("wrong", "other", "Other")));
    }
    [Fact]
    public async Task CollectionAndBiggestFishPersistAcrossStoreReload()
    {
        using var f = new Fixture(100); await f.Seed("a");
        await f.Send("a", "!fischen 100");
        var profile = await f.Points.GetProfileAsync("a", default);
        Assert.Equal(1, profile.Entry.FishCollection["Kraken"]);
        Assert.True(profile.Entry.BiggestFishGrams >= 5000);
        // A new store must read the on-disk data, not the previous instance's memory.
        var pathField = typeof(ViewerPointStore).GetField("_filePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var reloaded = new ViewerPointStore(Path.GetDirectoryName((string)pathField.GetValue(f.Points)!)!);
        var persisted = await reloaded.GetProfileAsync("a", default);
        Assert.Equal("Kraken", persisted.Entry.BiggestFishName);
        Assert.Equal(1, persisted.Entry.FishCollection["Kraken"]);
        await f.Send("a", "!sammlung");
        Assert.Contains(f.Messages, m => m.Contains("Fischsammlung") && m.Contains("Kraken: 1x"));
    }

    [Fact]
    public async Task DeniedPayoutDoesNotCollectFish()
    {
        using var f = new Fixture(100); await f.Seed("a");
        f.Config.DailyWinLimitEnabled = true; f.Config.DailyWinLimit = 1;
        await f.Send("a", "!fischen 100");
        Assert.Empty((await f.Points.GetProfileAsync("a", default)).Entry.FishCollection);
    }

    [Fact]
    public async Task ManualControlsPublishStatusAndCancelWithoutCharging()
    {
        using var f = new Fixture();
        await f.Service.ControlAsync("boss", 0, default);
        Assert.True(f.Service.Status.BossActive);
        Assert.Equal(60, f.Service.Status.BossHp);
        await f.Service.ControlAsync("cancelboss", 0, default);
        Assert.False(f.Service.Status.BossActive);
        await f.Seed("a"); await f.Send("a", "!start race 100");
        Assert.True(f.Service.Status.RaceActive);
        Assert.Equal(1, f.Service.Status.RacePlayers);
        await f.Service.ControlAsync("cancelrace", 0, default);
        Assert.False(f.Service.Status.RaceActive);
        Assert.Equal(1000, await f.Balance("a"));
        Assert.Contains(f.Messages, m => m.Contains("manuell abgebrochen"));
    }

    [Fact]
    public async Task BossVictoriesCountEvenWithZeroRewardAndRaceWinnerCounts()
    {
        using var f = new Fixture(30); await f.Seed("a"); await f.Seed("b");
        f.Config.Adventures.Boss.RewardPool = 0;
        await f.Send("mod", "!boss", moderator:true);
        await f.Send("a", "!joinboss"); await f.Send("b", "!joinboss");
        Assert.Equal(1, (await f.Points.GetProfileAsync("a", default)).Entry.BossDefeats);
        Assert.Equal(1, (await f.Points.GetProfileAsync("b", default)).Entry.BossDefeats);
        await f.Send("a", "!start race 100"); await f.Send("b", "!joinrace");
        f.Now = f.Now.AddSeconds(6); await f.Service.TickAsync(default);
        Assert.Equal(1, (await f.Points.GetProfileAsync("b", default)).Entry.RaceWins);
        Assert.Equal(0, (await f.Points.GetProfileAsync("a", default)).Entry.RaceWins);
    }

    [Fact]
    public async Task CustomTemplatesAreSubstitutedAndBounded()
    {
        using var f = new Fixture(100); await f.Seed("a");
        f.Config.Adventures.Fishing.ResultMessage = "{USER}: {stake} -> {payout} / {balance}";
        await f.Send("a", "!fischen 100");
        Assert.Equal("a: 100 -> 800 / 1700", Assert.Single(f.Messages));
        f.Config.Adventures.Boss.StartMessage = new string('x', 600);
        await f.Send("mod", "!boss", moderator:true);
        Assert.True(f.Messages.Last().Length <= 480);
        Assert.EndsWith("…", f.Messages.Last());
        Assert.Equal("a b", AdventureGameService.Format("a\nb"));
    }

    [Fact]
    public async Task OverlayOnlyServesReadOnlyLoopbackStateAndStopsCleanly()
    {
        using var cts = new CancellationTokenSource();
        var reservation = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((System.Net.IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var url = $"http://127.0.0.1:{port}/";
        var server = new AdventureOverlayServer(() => new AdventureStatus(BossActive:true, BossHp:42, BossMaxHp:100), port);
        var run = server.RunAsync(cts.Token);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var html = await http.GetStringAsync(url);
            Assert.Contains("!joinboss", html); Assert.Contains("background:transparent", html);
            var json = await http.GetStringAsync(url + "state");
            var status = System.Text.Json.JsonSerializer.Deserialize<AdventureStatus>(json)!;
            Assert.True(status.BossActive); Assert.Equal(42, status.BossHp);
            using var post = await http.PostAsync(url + "state", null);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, post.StatusCode);
            using var unknown = await http.GetAsync(url + "unknown");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally { cts.Cancel(); await run.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
    [Fact]
    public async Task BossStartIsModeratorOnlyButAttacksAndRaceAreOpenToViewers()
    {
        using var f = new Fixture(30); await f.Seed("a");
        await f.Send("a", "!boss", CommandAuthorization.Allowed);
        Assert.DoesNotContain(f.Messages, m => m.Contains("erscheint!"));
        await f.Send("mod", "!boss", moderator: true);
        await f.Send("a", "!joinboss");
        Assert.Contains(f.Messages, m => m.Contains("@a trifft"));
        await f.Send("a", "!start race 100");
        Assert.Contains(f.Messages, m => m.Contains("Rennen eröffnet"));
        var config = new AppConfig(); config.Minigame.Enabled = true; config.Heist.Enabled = true;
        config.Commands.CommandRoleOverrides["adventure.boss"] = "Viewer";
        config.Commands.CommandRoleOverrides["heist.start"] = "Viewer";
        var registry = new CommandRegistry(); registry.Update(config);
        Assert.Equal(CommandRole.Moderator, registry.Commands.Single(c => c.CommandId == "adventure.boss").RequiredRole);
        Assert.Equal(CommandRole.Moderator, registry.Commands.Single(c => c.CommandId == "heist.start").RequiredRole);
        Assert.Equal(CommandRole.Viewer, registry.Commands.Single(c => c.CommandId == "adventure.race").RequiredRole);
    }
    private sealed class FixedRandom(int value) : IDuelRandom
    { public int NextInclusive(int minimum, int maximum) => Math.Clamp(value, minimum, maximum); }
    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipAdventureTests", Guid.NewGuid().ToString("N"));
        public ViewerPointStore Points { get; }
        public MinigameConfig Config { get; } = new() { Enabled = true, PointsEnabled = true, ChatPointsEnabled = false };
        public List<string> Messages { get; } = new();
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public AdventureGameService Service { get; }
        public Fixture(int roll = 100)
        {
            Points = new(_directory);
            Config.Adventures.Race.JoinSeconds = 5;
            Config.Adventures.Boss.HitPoints = 60;
            Config.Adventures.Boss.RewardPool = 100;
            Service = new(Points, Config, (text, _) => { Messages.Add(text); return Task.CompletedTask; }, new FixedRandom(roll), () => Now);
        }
        public Task Seed(string id, long amount = 1000) => Points.AddPointsAsync(id, id, amount, 0, default);
        public Task<long> Balance(string id) => Points.GetPointsAsync(id, default);
        public Task Send(string id, string text, CommandAuthorization authorization = CommandAuthorization.Default, bool moderator = false)
        {
            var parsed = ChatMinigameService.ParseMinigameCommand(text);
            if (parsed.Command == "!start")
            {
                var translated = EventStartCommand.Translate(parsed.Parts)!;
                return Service.HandleAsync(new ChatMessage { Id = Guid.NewGuid().ToString(), UserId = id,
                    UserName = id, UserLogin = id, Text = text, IsModerator = moderator, CommandAuthorization = authorization },
                    translated[0], translated, default);
            }
            return Service.HandleAsync(new ChatMessage { Id = Guid.NewGuid().ToString(), UserId = id, UserName = id,
                UserLogin = id, Text = text, IsModerator = moderator, CommandAuthorization = authorization }, parsed.Command, parsed.Parts, default);
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }

    [Theory]
    [InlineData("!fischen", 1, 900)]
    [InlineData("!fischen", 80, 1000)]
    [InlineData("!fischen", 97, 1100)]
    [InlineData("!fischen", 100, 1700)]
    [InlineData("!schatzsuche", 1, 900)]
    [InlineData("!schatzsuche", 75, 1000)]
    [InlineData("!schatzsuche", 95, 1100)]
    [InlineData("!schatzsuche", 100, 1500)]
    public async Task SoloOutcomesAreAccountedExactlyOnce(string command, int roll, long expected)
    {
        using var f = new Fixture(roll);
        await f.Seed("a");
        await f.Send("a", command + " 100");
        await f.Send("a", command + " 100");
        Assert.Equal(expected, await f.Balance("a"));
        Assert.Equal(2, f.Messages.Count);
        Assert.Contains("Sekunden wieder verfügbar", f.Messages.Last());
    }

    [Theory]
    [InlineData("!fischen -100")]
    [InlineData("!fischen 0")]
    [InlineData("!fischen 999999999999999999999")]
    [InlineData("!schatzsuche 2000")]
    [InlineData("!schatzsuche")]
    public async Task InvalidOrUnaffordableStakesDoNotChangeBalance(string text)
    {
        using var f = new Fixture();
        await f.Seed("a"); await f.Send("a", text);
        Assert.Equal(1000, await f.Balance("a"));
        Assert.Contains("Nutzung", Assert.Single(f.Messages));
    }

    [Fact]
    public async Task AllUsesMaximumBetAndDailyLimitsAreHonored()
    {
        using var f = new Fixture(1);
        await f.Seed("a", 5000);
        f.Config.Adventures.Fishing.MaximumBet = 200;
        await f.Send("a", "!fischen all");
        Assert.Equal(4800, await f.Balance("a"));
        f.Now = f.Now.AddSeconds(60);
        f.Config.DailyLossLimitEnabled = true; f.Config.DailyLossLimit = 200;
        await f.Send("a", "!fischen 100");
        Assert.Equal(4800, await f.Balance("a"));
        Assert.Contains(f.Messages, s => s.Contains("Verlustlimit"));
    }

    [Fact]
    public async Task DisabledDeniedAndBlacklistedUsersCannotPlay()
    {
        using var f = new Fixture(); await f.Seed("a");
        await f.Send("a", "!fischen 100", CommandAuthorization.Denied);
        f.Config.PointsBlacklist.Add("a"); await f.Send("a", "!fischen 100");
        f.Config.PointsBlacklist.Clear(); f.Config.Adventures.Fishing.Enabled = false;
        await f.Send("a", "!fischen 100");
        Assert.Empty(f.Messages); Assert.Equal(1000, await f.Balance("a"));
    }

    [Fact]
    public async Task RaceUsesDedicatedJoinCommandAndAtomicPotWithoutDuplicateEntries()
    {
        using var f = new Fixture(0); await f.Seed("a"); await f.Seed("b");
        await f.Send("a", "!start race 100");
        await f.Send("b", "!joinrace"); await f.Send("b", "!joinrace");
        Assert.Equal(1000, await f.Balance("a")); Assert.Equal(1000, await f.Balance("b"));
        f.Now = f.Now.AddSeconds(6); await f.Service.TickAsync(default); await f.Service.TickAsync(default);
        Assert.Equal(1100, await f.Balance("a")); Assert.Equal(900, await f.Balance("b"));
        Assert.Single(f.Messages.Where(s => s.Contains("Zieleinlauf")));
        Assert.False(AdventureGameService.Recognizes("!join"));
    }

    [Fact]
    public async Task RaceCancelsWithoutChargingAnybodyWhenBalanceChanges()
    {
        using var f = new Fixture(0); await f.Seed("a"); await f.Seed("b");
        await f.Send("a", "!start race 100"); await f.Send("b", "!joinrace");
        await f.Points.RemovePointsAsync("b", "b", 950, 0, default);
        f.Now = f.Now.AddSeconds(6); await f.Service.TickAsync(default);
        Assert.Equal(1000, await f.Balance("a")); Assert.Equal(50, await f.Balance("b"));
        Assert.Contains(f.Messages, s => s.Contains("Keine Einsätze abgebucht"));
    }

    [Fact]
    public async Task RaceCancelAndMissingParticipantsNeverLoseStakes()
    {
        using var f = new Fixture(); await f.Seed("a");
        await f.Send("a", "!start race 100"); await f.Service.CancelAsync();
        f.Now = f.Now.AddMinutes(2); await f.Service.TickAsync(default);
        Assert.Equal(1000, await f.Balance("a"));
        await f.Send("a", "!start race 100"); f.Now = f.Now.AddSeconds(6); await f.Service.TickAsync(default);
        Assert.Equal(1000, await f.Balance("a"));
        Assert.Contains(f.Messages, s => s.Contains("weniger als 2"));
    }

    [Fact]
    public async Task BossSharesRewardAcrossUniqueAttackersAndEnforcesAttackCooldown()
    {
        using var f = new Fixture(30); await f.Seed("a"); await f.Seed("b");
        await f.Send("a", "!boss", moderator: true);
        await f.Send("a", "!joinboss"); await f.Send("a", "!joinboss");
        Assert.Equal(1000, await f.Balance("a"));
        await f.Send("b", "!joinboss");
        Assert.Equal(1050, await f.Balance("a")); Assert.Equal(1050, await f.Balance("b"));
        await f.Send("b", "!joinboss");
        Assert.Equal(1050, await f.Balance("b"));
        Assert.Single(f.Messages.Where(s => s.Contains("Boss besiegt")));
    }

    [Fact]
    public async Task BossAutomaticallyStartsOncePerIntervalAndExpiresWithoutReward()
    {
        using var f = new Fixture(); await f.Seed("a");
        f.Config.Adventures.Boss.AutoStartEnabled = true;
        f.Config.Adventures.Boss.AutoStartMinutes = 1;
        f.Config.Adventures.Boss.DurationSeconds = 10;
        f.Service.UpdateConfig(f.Config);
        f.Now = f.Now.AddSeconds(59); await f.Service.TickAsync(default); Assert.Empty(f.Messages);
        f.Now = f.Now.AddSeconds(1); await f.Service.TickAsync(default); await f.Service.TickAsync(default);
        Assert.Single(f.Messages.Where(s => s.Contains("erscheint!")));
        f.Now = f.Now.AddSeconds(11); await f.Service.TickAsync(default);
        Assert.Contains(f.Messages, s => s.Contains("Zeit abgelaufen"));
        Assert.Equal(1000, await f.Balance("a"));
        f.Config.Adventures.Boss.AutoStartEnabled = false;
        f.Now = f.Now.AddHours(1); await f.Service.TickAsync(default);
        Assert.Single(f.Messages.Where(s => s.Contains("erscheint!")));
    }

    [Fact]
    public void RegistryIncludesSeparateJoinCommandsAndRespectsDisabledGames()
    {
        var config = new AppConfig(); config.Minigame.Enabled = true;
        var registry = new CommandRegistry(); registry.Update(config);
        Assert.Contains(registry.Commands, c => c.CommandText == "!joinrace" && c.Enabled);
        Assert.Contains(registry.Commands, c => c.CommandText == "!joinboss" && c.Enabled);
        Assert.DoesNotContain(registry.FindCollisions(false), c => c.Command == "!join");
        config.Minigame.Adventures.Race.Enabled = false; registry.Update(config);
        Assert.DoesNotContain(registry.Commands, c => c.ModuleId == "race" && c.Enabled);
    }

    [Fact]
    public async Task ParallelRaceJoinsAndSettlementDoNotDuplicateMoney()
    {
        using var f = new Fixture(0); await f.Seed("a"); await f.Seed("b");
        await f.Send("a", "!start race 100");
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => f.Send("b", "!joinrace")));
        f.Now = f.Now.AddSeconds(6);
        await Task.WhenAll(f.Service.TickAsync(default), f.Service.TickAsync(default));
        Assert.Equal(2000, await f.Balance("a") + await f.Balance("b"));
        Assert.Single(f.Messages.Where(m => m.Contains("Zieleinlauf")));
    }

    [Fact]
    public async Task BossAutoRepeatsAfterIntervalAndRoundCooldownButNotWhileDisabled()
    {
        using var f = new Fixture(); await f.Seed("a");
        f.Config.Adventures.Boss.AutoStartEnabled = true;
        f.Config.Adventures.Boss.AutoStartMinutes = 1;
        f.Config.Adventures.Boss.RoundCooldownSeconds = 1;
        f.Config.Adventures.Boss.DurationSeconds = 10;
        f.Service.UpdateConfig(f.Config);
        f.Now = f.Now.AddSeconds(60); await f.Service.TickAsync(default);
        f.Now = f.Now.AddSeconds(11); await f.Service.TickAsync(default);
        f.Now = f.Now.AddSeconds(49); await f.Service.TickAsync(default);
        Assert.Equal(2, f.Messages.Count(m => m.Contains("erscheint!")));
        f.Config.Enabled = false; f.Now = f.Now.AddHours(1); await f.Service.TickAsync(default);
        Assert.Equal(2, f.Messages.Count(m => m.Contains("erscheint!")));
    }

    [Fact]
    public void LegacyHeistJoinMigratesWithoutChangingCustomNames()
    {
        var config = new AppConfig(); config.Heist.JoinCommand = "!join";
        var normalize = typeof(ConfigurationService).GetMethod("Normalize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        normalize.Invoke(null, new object[] { config, false });
        Assert.Equal("!joinheist", config.Heist.JoinCommand);
        config.Heist.JoinCommand = "!raidbeitritt";
        normalize.Invoke(null, new object[] { config, false });
        Assert.Equal("!raidbeitritt", config.Heist.JoinCommand);
        Assert.Equal("!joinheist", new HeistConfig().JoinCommand);
    }
}
