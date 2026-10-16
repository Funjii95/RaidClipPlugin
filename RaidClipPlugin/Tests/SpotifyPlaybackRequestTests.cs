using System.Text;
using System.Text.Json;
using RaidClipPlugin.Services;
using RaidClipPlugin.Models;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class SpotifyPlaybackRequestTests
{
    [Fact]
    public async Task ReturnRequestKeepsOriginalContextTrackAndPosition()
    {
        using var request = SpotifyService.CreatePlaybackRequest(
            "https://api.spotify.com/v1/me/player/play", "spotify:track:original",
            "spotify:playlist:original", 42000);
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("spotify:playlist:original", json.RootElement.GetProperty("context_uri").GetString());
        Assert.Equal("spotify:track:original", json.RootElement.GetProperty("offset").GetProperty("uri").GetString());
        Assert.Equal(42000, json.RootElement.GetProperty("position_ms").GetInt32());
    }

    [Theory]
    [InlineData(99000, true, "device", "context", true)]
    [InlineData(10000, true, "device", "context", false)]
    [InlineData(99000, false, "device", "context", false)]
    [InlineData(99000, true, "other-device", "context", false)]
    [InlineData(99000, true, "device", "other-context", false)]
    public void ReturnOnlyAfterNaturalEnd(int progress, bool playing, string device, string context, bool expected)
    {
        var track = new SpotifyTrack("request", "spotify:track:request", "Request", "Artist", 100000, false, true, "");
        var last = new SpotifyService.SpotifyReturnPoint(track, "context", "device", progress, playing);
        var current = new SpotifyService.SpotifyReturnPoint(track with { Id = "next" }, context, device, 500, true);
        Assert.Equal(expected, SpotifyService.IsNaturalTrackEnd(last, current));
    }

    [Fact]
    public void EmptyPlayerOnlyTriggersReturnAfterPlayingTrackNearEnd()
    {
        var track = new SpotifyTrack("request", "spotify:track:request", "Request", "Artist", 100000, false, true, "");
        var last = new SpotifyService.SpotifyReturnPoint(track, "context", "device", 99000, true);
        Assert.True(SpotifyService.IsNaturalTrackEnd(last, null));
        Assert.False(SpotifyService.IsNaturalTrackEnd(last with { IsPlaying = false }, null));
        Assert.False(SpotifyService.IsNaturalTrackEnd(last with { PositionMs = 1000 }, null));
    }

    [Fact]
    public async Task AlbumStartTargetsExactTrackWithoutQueueOrTrackList()
    {
        using var request = SpotifyService.CreatePlaybackRequest(
            "https://api.spotify.com/v1/me/player/play?device_id=test",
            "spotify:track:test", "spotify:album:test");
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("spotify:album:test", json.RootElement.GetProperty("context_uri").GetString());
        Assert.Equal("spotify:track:test", json.RootElement.GetProperty("offset").GetProperty("uri").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("position_ms").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("uris", out _));
    }

    [Fact]
    public async Task StartRequestContainsBufferedTrackAndStartPosition()
    {
        const string url = "https://api.spotify.com/v1/me/player/play?device_id=test";
        using var request = SpotifyService.CreatePlaybackRequest(url, "spotify:track:test");
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(url, request.RequestUri!.AbsoluteUri);
        Assert.NotNull(request.Content);
        var lengthBeforeReading = request.Content.Headers.ContentLength;
        Assert.NotNull(lengthBeforeReading);
        Assert.Equal("application/json", request.Content.Headers.ContentType!.MediaType);
        var body = await request.Content.ReadAsStringAsync();
        Assert.Equal((long)Encoding.UTF8.GetByteCount(body), lengthBeforeReading);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("spotify:track:test", json.RootElement.GetProperty("uris")[0].GetString());
        Assert.Equal(0, json.RootElement.GetProperty("position_ms").GetInt32());
    }
}
