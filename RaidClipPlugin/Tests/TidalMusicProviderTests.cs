using System.Text.Json;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class TidalMusicProviderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "RaidClipTidal-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [Theory]
    [InlineData("https://tidal.com/track/123")]
    [InlineData("https://www.tidal.com/browse/track/123?u=share#ignored")]
    [InlineData("https://listen.tidal.com/track/123")]
    [InlineData("https://embed.tidal.com/tracks/123")]
    public void LinksAreCanonicalAndDoNotClaimTrackIsPlayable(string input)
    {
        Assert.True(TidalMusicProvider.TryResolveTrack(input, out var track));
        Assert.Equal("123", track.Id);
        Assert.Equal("https://tidal.com/track/123", track.ExternalUrl);
        Assert.Equal("https://embed.tidal.com/tracks/123", track.EmbedUrl);
        Assert.False(track.IsPlayable);
    }
    [Theory]
    [InlineData("http://tidal.com/track/123")]
    [InlineData("https://tidal.com.evil.test/track/123")]
    [InlineData("https://evil.test@tidal.com/track/123")]
    [InlineData("https://tidal.com:8080/track/123")]
    [InlineData("https://tidal.com/track/0")]
    [InlineData("https://tidal.com/track/1<script>")]
    [InlineData("https://tidal.com/album/123")]
    [InlineData("https://open.spotify.com/track/123")]
    [InlineData("file:///C:/secret")]
    [InlineData("tidal://track/123")]
    [InlineData("Songname Künstler")]
    public void UnsafeOrUnsupportedInputsAreRejected(string input) => Assert.False(TidalMusicProvider.TryResolveTrack(input, out _));

    [Fact]
    public async Task SeparatePersistentQueueDoesNotOverwriteSpotifyDataOrPretendPlayback()
    {
        Directory.CreateDirectory(directory);
        var spotify = Path.Combine(directory, "music-requests.json");
        await File.WriteAllTextAsync(spotify, "unchanged");
        var provider = new TidalMusicProvider(directory);
        var track = (await provider.SearchTracksAsync("https://tidal.com/track/123", default)).Single();
        var result = await provider.AddSongRequestAsync(track, default);
        Assert.True(result.Success); Assert.True(result.RequiresManualPlayback);
        Assert.Contains("nicht abgespielt", result.Message);
        var restored = new TidalMusicProvider(directory);
        Assert.Equal(track.Id, (await restored.GetEntriesAsync(default)).Single().Id);
        await restored.RemoveAsync(track.Id, default);
        Assert.Empty(await restored.GetEntriesAsync(default));
        Assert.Equal("unchanged", await File.ReadAllTextAsync(spotify));
    }
    [Fact]
    public async Task ConcurrentDuplicateSubmissionsOnlyCreateOneEntry()
    {
        var provider = new TidalMusicProvider(directory);
        TidalMusicProvider.TryResolveTrack("https://tidal.com/track/123", out var track);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => provider.AddSongRequestAsync(track, default)));
        Assert.Single(results, r => r.Success);
        Assert.Single(await provider.GetEntriesAsync(default));
    }
    [Fact]
    public async Task SavedEmbedUrlsAreNotTrustedAndCorruptDataIsPreserved()
    {
        var provider = new TidalMusicProvider(directory);
        TidalMusicProvider.TryResolveTrack("https://tidal.com/track/123", out var track);
        await provider.AddSongRequestAsync(track, default);
        var file = Path.Combine(directory, "tidal-test-queue.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new[] { track with { EmbedUrl = "https://evil.test/" } }));
        Assert.Equal(track.EmbedUrl, (await provider.GetEntriesAsync(default)).Single().EmbedUrl);
        await File.WriteAllTextAsync(file, "broken JSON");
        await Assert.ThrowsAsync<JsonException>(() => provider.AddSongRequestAsync(track, default));
        Assert.Equal("broken JSON", await File.ReadAllTextAsync(file));
    }
    [Fact]
    public async Task TrialNeverAdvertisesApiLoginOrRemotePlayback()
    {
        var provider = new TidalMusicProvider(directory);
        Assert.False(provider.Capabilities.SupportsAuthentication);
        Assert.False(provider.Capabilities.SupportsSearch);
        Assert.False(provider.Capabilities.SupportsPlaybackControl);
        Assert.False(provider.Capabilities.SupportsQueueControl);
        Assert.False(await provider.IsAuthenticatedAsync(default));
        Assert.False((await provider.AuthenticateAsync(default)).Success);
        Assert.True(TidalMusicProvider.IsTrustedNavigation("https://login.tidal.com/authorize"));
        Assert.False(TidalMusicProvider.IsTrustedNavigation("https://tidal.com.evil.test"));
        Assert.False(TidalMusicProvider.IsTrustedNavigation("javascript:alert(1)"));
    }
}
