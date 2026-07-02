using Xunit;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin.Tests;

public sealed class MusicRequestRulesTests
{
    private const string Id = "4iV5W9uYEdYUVa79Axb7Rh";

    [Theory]
    [InlineData("https://open.spotify.com/track/4iV5W9uYEdYUVa79Axb7Rh")]
    [InlineData("https://open.spotify.com/track/4iV5W9uYEdYUVa79Axb7Rh?si=test")]
    [InlineData("https://open.spotify.com/intl-de/track/4iV5W9uYEdYUVa79Axb7Rh?si=test")]
    [InlineData("https://open.spotify.com/embed/track/4iV5W9uYEdYUVa79Axb7Rh")]
    [InlineData("spotify:track:4iV5W9uYEdYUVa79Axb7Rh")]
    public void ValidTrackInputIsParsed(string input)
    {
        Assert.True(MusicRequestService.TryExtractSpotifyTrackId(input, out var id));
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://open.spotify.com/playlist/4iV5W9uYEdYUVa79Axb7Rh")]
    [InlineData("https://open.spotify.com/album/4iV5W9uYEdYUVa79Axb7Rh")]
    [InlineData("https://evil.example/track/4iV5W9uYEdYUVa79Axb7Rh")]
    [InlineData("spotify:episode:4iV5W9uYEdYUVa79Axb7Rh")]
    public void InvalidOrNonTrackInputIsRejected(string input) =>
        Assert.False(MusicRequestService.TryExtractSpotifyTrackId(input, out _));

    [Fact]
    public void DurationLimitIsApplied()
    {
        var config = new MusicRequestConfig { MaximumTrackDurationMinutes = 10 };
        Assert.Null(MusicRequestRules.ValidateTrack(config,
            Track(durationMs: 599_000), Array.Empty<string>()));
        Assert.Equal("too-long", MusicRequestRules.ValidateTrack(config,
            Track(durationMs: 601_000), Array.Empty<string>()));
    }

    [Fact]
    public void ExplicitTrackIsRejectedWhenDisabled()
    {
        var config = new MusicRequestConfig { AllowExplicitTracks = false };
        Assert.Equal("explicit", MusicRequestRules.ValidateTrack(config,
            Track(explicitTrack: true), Array.Empty<string>()));
    }

    [Fact]
    public void BlacklistsIgnoreCase()
    {
        var config = new MusicRequestConfig
        {
            ArtistBlacklist = new() { "LINKIN PARK" },
            SongTitleBlacklist = new() { "nUmB" }
        };
        Assert.Equal("blacklist", MusicRequestRules.ValidateTrack(config,
            Track(), Array.Empty<string>()));
        config.ArtistBlacklist.Clear();
        Assert.Equal("blacklist", MusicRequestRules.ValidateTrack(config,
            Track(), Array.Empty<string>()));
        config.SongTitleBlacklist.Clear();
        config.UserBlacklist.Add("nightbot");
        Assert.True(MusicRequestRules.IsUserBlacklisted(config, "@NightBot"));
    }

    [Fact]
    public void DuplicateTrackIsRejected()
    {
        var config = new MusicRequestConfig { AllowDuplicateTracks = false };
        Assert.Equal("duplicate", MusicRequestRules.ValidateTrack(
            config, Track(), new[] { Id }));
    }

    [Fact]
    public void QueueAndUserLimitsAreApplied()
    {
        var now = DateTimeOffset.UtcNow;
        var config = new MusicRequestConfig
        {
            MaximumQueueLength = 1,
            MaximumRequestsPerUser = 1,
            UserCooldownMinutes = 0
        };
        var entry = Entry("user-1", MusicRequestStatus.Queued, now);
        Assert.Equal("queue-full", MusicRequestRules.ValidateLimits(
            config, new[] { entry }, "user-2", now, out _));
        config.MaximumQueueLength = 25;
        Assert.Equal("user-limit", MusicRequestRules.ValidateLimits(
            config, new[] { entry }, "user-1", now, out _));
    }

    [Fact]
    public void CooldownIsApplied()
    {
        var now = DateTimeOffset.UtcNow;
        var config = new MusicRequestConfig
        {
            UserCooldownMinutes = 5,
            MaximumRequestsPerUser = 2
        };
        var completed = Entry("user-1", MusicRequestStatus.Completed,
            now.AddMinutes(-2));
        Assert.Equal("cooldown", MusicRequestRules.ValidateLimits(
            config, new[] { completed }, "user-1", now, out var remaining));
        Assert.InRange(remaining.TotalMinutes, 2.9, 3.1);
    }

    [Fact]
    public void OnlyConfiguredRewardWithInputIsAccepted()
    {
        var config = new MusicRequestConfig
        {
            Enabled = true,
            SelectedRewardId = "reward-1"
        };
        var valid = Redemption("reward-1", "Numb", "unfulfilled");
        Assert.True(MusicRequestRules.ShouldAcceptRedemption(config, valid));
        Assert.False(MusicRequestRules.ShouldAcceptRedemption(
            config, Redemption("reward-2", "Numb", "unfulfilled")));
        Assert.False(MusicRequestRules.ShouldAcceptRedemption(
            config, Redemption("reward-1", "", "unfulfilled")));
        Assert.False(MusicRequestRules.ShouldAcceptRedemption(
            config, Redemption("reward-1", "Numb", "fulfilled")));
    }

    [Fact]
    public async Task ProcessedRedemptionSurvivesRestart()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "raidclip-music-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var first = new MusicRequestStore(path);
            var entry = Entry("user-1", MusicRequestStatus.Queued,
                DateTimeOffset.UtcNow);
            await first.AddOrUpdateAsync(entry, true, CancellationToken.None);
            var second = new MusicRequestStore(path);
            Assert.True(await second.IsProcessedAsync(
                entry.RedemptionId, CancellationToken.None));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ConcurrentWritesDoNotCreateDuplicateEntries()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "raidclip-music-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new MusicRequestStore(path);
            var entry = Entry("user-1", MusicRequestStatus.Checking,
                DateTimeOffset.UtcNow);
            await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
                store.AddOrUpdateAsync(entry, true, CancellationToken.None)));
            Assert.Single(await store.GetEntriesAsync(CancellationToken.None));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static MusicRequestRedemption Redemption(
        string rewardId, string input, string status) =>
        new(Guid.NewGuid().ToString("N"), rewardId, "Musikwunsch",
            "user-1", "viewer", "Viewer", input,
            DateTimeOffset.UtcNow, status);

    private static SpotifyTrack Track(
        int durationMs = 180_000, bool explicitTrack = false) =>
        new(Id, "spotify:track:" + Id, "Numb", "Linkin Park",
            durationMs, explicitTrack, true,
            "https://open.spotify.com/track/" + Id);

    private static MusicRequestEntry Entry(
        string userId, MusicRequestStatus status, DateTimeOffset updated) =>
        new()
        {
            RedemptionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Status = status,
            UpdatedAt = updated,
            Track = Track()
        };
}
