using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class TimerTestButtonTests
{
    private sealed class Client : IChatTimerClient
    {
        public string Text = "", Color = "";
        public int Sent;
        public Task<int> GetViewerCountAsync(string id, CancellationToken token) => throw new InvalidOperationException("Test must not query viewers");
        public Task SendChatMessageAsync(string broadcasterId, string senderId, string message, CancellationToken token)
        { Text = message; Sent++; return Task.CompletedTask; }
        public Task SendChatAnnouncementAsync(string broadcasterId, string senderId, string message, string color, CancellationToken token)
        { Color = color; Text = message; Sent++; return Task.CompletedTask; }
    }
    [Theory]
    [InlineData("")] [InlineData("purple")]
    public async Task TestSendsExactlyOnceAndIgnoresScheduleAndViewers(string color)
    {
        var client = new Client();
        var entry = new ChatTimerEntryConfig { Enabled = false, Message = " Test 🦦 ", MinimumViewers = 9999, AnnouncementColor = color, IntervalMinutes = 60 };
        await ChatTimerService.SendTestAsync(entry, "channel", "bot", client, default);
        Assert.Equal(1, client.Sent); Assert.Equal("Test 🦦", client.Text); Assert.Equal(color, client.Color);
        Assert.False(entry.Enabled); Assert.Equal(60, entry.IntervalMinutes);
    }
    [Fact]
    public async Task InvalidMessageDoesNotSend()
    {
        var client = new Client();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await ChatTimerService.SendTestAsync(new() { Message = " " }, "c", "b", client, default));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await ChatTimerService.SendTestAsync(new() { Message = new string('x', 501) }, "c", "b", client, default));
        Assert.Equal(0, client.Sent);
    }
}
