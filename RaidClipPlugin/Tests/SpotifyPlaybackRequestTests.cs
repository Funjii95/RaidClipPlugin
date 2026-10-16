using System.Text;
using System.Text.Json;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class SpotifyPlaybackRequestTests
{
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
