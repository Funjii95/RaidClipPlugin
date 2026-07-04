using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class ClipCommandTests
{
    [Theory]
    [InlineData("!clip Mein Moment", "Mein Moment")]
    [InlineData("  !CLIP    Ein   Titel  ", "Ein   Titel")]
    [InlineData("!createclip Alias", "Alias")]
    public void CommandIsDetectedCaseInsensitive(string input, string expected)
    {
        var config = new ClipCommandConfig();
        Assert.True(ClipCommandService.TryParseCommand(input, config, out var title));
        Assert.Equal(expected, title);
    }

    [Fact]
    public void EmptyTitleUsesConfiguredTemplate()
    {
        var service = new ClipTemplateService();
        var stream = Stream("Just Chatting");
        var message = Message("TestUser");
        var title = service.CreateTitle("", "{username} clippt {game}",
            100, message, stream, DateTimeOffset.Now);
        Assert.Equal("TestUser clippt Just Chatting", title);
    }

    [Fact]
    public void TitleRemovesControlCharactersAndAppliesMaximumLength()
    {
        Assert.Equal("abcdef", ClipTemplateService.SanitizeTitle(
            "abc\0defghi", 6));
    }

    [Fact]
    public async Task BlacklistAlwaysWins()
    {
        var client = new FakeTwitchClipClient { IsFollower = true };
        var service = new ClipPermissionService(client);
        var config = new ClipCommandConfig
        {
            AllowedUsers = new() { "testuser" },
            BlockedUsers = new() { "TESTUSER" }
        };
        var result = await service.CheckAsync(
            Message("TestUser"), "b", config, CancellationToken.None);
        Assert.False(result.Allowed);
        Assert.Equal("blacklist", result.Reason);
    }

    [Fact]
    public async Task ConfiguredRolesAndFollowersAreAllowed()
    {
        var client = new FakeTwitchClipClient { IsFollower = true };
        var service = new ClipPermissionService(client);
        var config = new ClipCommandConfig();
        config.AllowedRoles.Broadcaster = false;
        config.AllowedRoles.Moderators = false;
        config.AllowedRoles.Vips = false;
        config.AllowedRoles.Followers = true;
        var result = await service.CheckAsync(
            Message("Follower"), "b", config, CancellationToken.None);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CooldownsAndLimitsAreEnforcedPerStream()
    {
        var service = new ClipCooldownService();
        var config = new ClipCommandConfig
        {
            GlobalCooldownSeconds = 30,
            UserCooldownSeconds = 120,
            DuplicateWindowSeconds = 5,
            MaximumClipsPerStream = 1,
            MaximumClipsPerUserPerStream = 1
        };
        var now = DateTimeOffset.UtcNow;
        Assert.True(service.TryAccept("stream", "u1", now, config).Accepted);
        Assert.False(service.TryAccept("stream", "u2", now.AddSeconds(1), config).Accepted);
        service.RecordSuccess("stream", "u1");
        var limited = service.TryAccept("stream", "u2", now.AddMinutes(3), config);
        Assert.False(limited.Accepted);
        Assert.Equal("stream-limit", limited.Reason);
        Assert.True(service.TryAccept("new-stream", "u2", now.AddMinutes(3), config).Accepted);
    }

    [Fact]
    public void GlobalAndPersonalCooldownsAreReported()
    {
        var service = new ClipCooldownService();
        var config = new ClipCommandConfig
        {
            GlobalCooldownSeconds = 10,
            UserCooldownSeconds = 30,
            DuplicateWindowSeconds = 1,
            MaximumClipsPerStream = 50,
            MaximumClipsPerUserPerStream = 5
        };
        var now = DateTimeOffset.UtcNow;
        Assert.True(service.TryAccept("stream", "u1", now, config).Accepted);
        Assert.Equal("global-cooldown",
            service.TryAccept("stream", "u2", now.AddSeconds(2), config).Reason);
        Assert.Equal("user-cooldown",
            service.TryAccept("stream", "u1", now.AddSeconds(11), config).Reason);
    }

    [Fact]
    public void UserStreamLimitIsEnforced()
    {
        var service = new ClipCooldownService();
        var config = new ClipCommandConfig
        {
            GlobalCooldownSeconds = 0,
            UserCooldownSeconds = 0,
            DuplicateWindowSeconds = 1,
            MaximumClipsPerStream = 50,
            MaximumClipsPerUserPerStream = 1
        };
        var now = DateTimeOffset.UtcNow;
        Assert.True(service.TryAccept("stream", "u1", now, config).Accepted);
        service.RecordSuccess("stream", "u1");
        var result = service.TryAccept("stream", "u1", now.AddSeconds(2), config);
        Assert.False(result.Accepted);
        Assert.Equal("user-limit", result.Reason);
    }

    [Fact]
    public async Task TwitchClipPollingReturnsPublishedClip()
    {
        var client = new FakeTwitchClipClient
        {
            PublishedAfterCalls = 2
        };
        var service = new TwitchClipService(client,
            TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1));
        var clip = await service.CreateAndWaitAsync(
            new TwitchClipRequest("b", "Titel", 30), CancellationToken.None);
        Assert.Equal("clip-id", clip.Id);
    }

    [Fact]
    public async Task TwitchClipPollingTimesOutCleanly()
    {
        var client = new FakeTwitchClipClient
        {
            PublishedAfterCalls = int.MaxValue
        };
        var service = new TwitchClipService(client,
            TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(15));
        await Assert.ThrowsAsync<TimeoutException>(() =>
            service.CreateAndWaitAsync(
                new TwitchClipRequest("b", "Titel", 30), CancellationToken.None));
    }

    [Fact]
    public async Task DiscordContinuesAfterSingleChannelFailure()
    {
        var config = DiscordConfig("1", "2", "3");
        var client = new FakeDiscordClipClient { FailedChannel = "2" };
        using var service = new DiscordClipService(
            config, new DiscordCredentials(), client,
            new ClipTemplateService());
        var result = await service.PostClipAsync(Context(), CancellationToken.None);
        Assert.Equal(2, result.SuccessfulChannels);
        Assert.Equal(1, result.FailedChannels);
        Assert.Equal(3, client.Attempts.Count);
    }

    [Fact]
    public async Task DiscordPostsToAllEnabledChannels()
    {
        var client = new FakeDiscordClipClient();
        using var service = new DiscordClipService(
            DiscordConfig("1", "2"), new DiscordCredentials(), client,
            new ClipTemplateService());
        var result = await service.PostClipAsync(Context(), CancellationToken.None);
        Assert.True(result.AllSucceeded);
        Assert.Equal(2, client.Attempts.Count);
    }

    [Fact]
    public void DiscordPayloadSuppressesUserMentions()
    {
        var config = DiscordConfig("1");
        var context = Context() with
        {
            RequestedTitle = "Hallo @everyone @here <@&123456>"
        };
        var payload = DiscordClipService.BuildPayload(
            config, "{clipTitle} {clipUrl}", context,
            new ClipTemplateService());
        var json = JsonSerializer.Serialize(payload);
        Assert.False(json.Contains("@everyone", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("@here", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("<@&123456>", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscordEmbedUsesPublishedClipPreviewAndNeverCreatesEmptyFields()
    {
        var config = DiscordConfig("1");
        config.UseEmbed = true;
        config.UseThumbnail = true;
        var context = Context() with
        {
            Clip = new PublishedClip("clip-id", "https://clips.twitch.tv/clip-id",
                "Twitch-Titel", "https://clips-media-assets2.twitch.tv/preview.jpg", 30),
            Game = "",
            ThumbnailUrl = "https://static-cdn.jtvnw.net/profile.jpg"
        };
        var payload = DiscordClipService.BuildPayload(
            config, "{clipTitle} {clipUrl}", context, new ClipTemplateService());
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var embed = document.RootElement.GetProperty("embeds")[0];
        Assert.Equal("https://clips.twitch.tv/clip-id", embed.GetProperty("url").GetString());
        Assert.Equal("https://clips-media-assets2.twitch.tv/preview.jpg",
            embed.GetProperty("thumbnail").GetProperty("url").GetString());
        Assert.Contains("Twitch-Titel", embed.GetProperty("title").GetString());
        Assert.All(embed.GetProperty("fields").EnumerateArray(), field =>
            Assert.False(string.IsNullOrWhiteSpace(field.GetProperty("value").GetString())));
    }

    [Fact]
    public void MissingClipScopeIsDetected()
    {
        var session = new TwitchSession("token", "refresh", "id", "login",
            new[] { "user:read:chat" });
        Assert.False(session.HasScope("clips:edit"));
    }

    private static ChatMessage Message(string name) => new()
    {
        UserId = "user-id", UserLogin = name.ToLowerInvariant(),
        UserName = name, Text = "!clip", IsBroadcaster = false
    };

    private static TwitchLiveStream Stream(string game) => new(
        "stream-id", "b", "channel", "Channel", "game-id", game,
        DateTimeOffset.UtcNow, true);

    private static ClipDiscordContext Context() => new(
        new PublishedClip("clip-id", "https://clips.twitch.tv/clip-id",
            "Titel", "", 30),
        "Mein Clip", "Viewer", "Channel", "Game", DateTimeOffset.Now);

    private static DiscordClipsConfig DiscordConfig(params string[] ids) => new()
    {
        Enabled = true,
        GuildId = "100",
        Channels = ids.Select(id => new DiscordClipChannelConfig
        {
            ChannelId = id,
            Enabled = true
        }).ToList()
    };

    private sealed class FakeTwitchClipClient : ITwitchClipClient
    {
        public bool IsFollower { get; set; }
        public int PublishedAfterCalls { get; set; } = 1;
        private int _calls;

        public Task<TwitchCreatedClip> CreateClipAsync(
            TwitchClipRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new TwitchCreatedClip("clip-id", "edit"));

        public Task<PublishedClip?> GetClipByIdAsync(
            string clipId, CancellationToken cancellationToken)
        {
            _calls++;
            PublishedClip? clip = _calls >= PublishedAfterCalls
                ? new PublishedClip("clip-id", "https://clips.twitch.tv/clip-id",
                    "Titel", "", 30)
                : null;
            return Task.FromResult(clip);
        }

        public Task<TwitchLiveStream?> GetLiveStreamAsync(
            string broadcasterId, CancellationToken cancellationToken) =>
            Task.FromResult<TwitchLiveStream?>(Stream("Game"));

        public Task<bool> IsFollowerAsync(
            string broadcasterId, string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(IsFollower);
    }

    private sealed class FakeDiscordClipClient : IDiscordClipClient
    {
        public string FailedChannel { get; set; } = "";
        public List<string> Attempts { get; } = new();

        public Task<DiscordChannelValidation> ValidateChannelAsync(
            string guildId, string channelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DiscordChannelValidation(
                channelId, channelId, true, true, true, true));

        public Task<DiscordChannelValidation> ValidateWebhookAsync(
            string webhookUrl, string guildId, string channelId,
            CancellationToken cancellationToken) =>
            ValidateChannelAsync(guildId, channelId, cancellationToken);

        public Task SendMessageAsync(
            string channelId, object payload,
            CancellationToken cancellationToken)
        {
            Attempts.Add(channelId);
            if (channelId == FailedChannel)
                throw new HttpRequestException("Fehler");
            return Task.CompletedTask;
        }

        public Task SendWebhookAsync(
            string webhookUrl, object payload,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
