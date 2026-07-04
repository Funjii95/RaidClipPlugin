using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class LiveChatServiceTests
{
    [Fact]
    public void MessageIsAddedAndOldestIsRemovedAtLimit()
    {
        var service = Create(new LiveChatConfig { MaxMessages = 100 });
        for (var i = 0; i < 105; i++) service.ProcessMessage(Message(i.ToString(), "Text " + i));
        var items = service.GetVisibleSnapshot();
        Assert.Equal(100, items.Count);
        Assert.Equal("5", items[0].Id);
        Assert.Equal("104", items[^1].Id);
    }

    [Fact]
    public void HideCommandsOnlyFiltersDisplay()
    {
        var registry = Registry();
        var service = new LiveChatService(new LiveChatConfig { HideCommands = true }, "bot", Array.Empty<string>(), registry);
        var commandProcessed = false;
        var source = Message("1", "!punkte");
        service.ProcessMessage(source);
        commandProcessed = source.Text == "!punkte";
        Assert.Empty(service.GetVisibleSnapshot());
        Assert.Equal(1, service.StoredCount);
        Assert.True(commandProcessed);
    }

    [Fact]
    public void BotMessagesCanBeHidden()
    {
        var service = new LiveChatService(new LiveChatConfig { HideBotMessages = true }, "bot",
            new[] { "nightbot" }, Registry());
        service.ProcessMessage(Message("1", "hello", "nightbot"));
        Assert.Empty(service.GetVisibleSnapshot());
        Assert.Equal(1, service.StoredCount);
    }

    [Fact]
    public void PauseAndDisablePreventVisibleAdditions()
    {
        var service = Create(new LiveChatConfig());
        service.SetPaused(true); service.ProcessMessage(Message("1", "paused"));
        Assert.Equal(0, service.StoredCount);
        service.SetPaused(false); service.UpdateConfig(new LiveChatConfig { Enabled = false });
        service.ProcessMessage(Message("2", "disabled"));
        Assert.Equal(0, service.StoredCount);
    }

    [Fact]
    public void ClearOnlyClearsLiveChatHistory()
    {
        var service = Create(new LiveChatConfig());
        var unrelatedLog = new List<string> { "log" };
        var unrelatedClips = new List<string> { "clip" };
        service.ProcessMessage(Message("1", "hello")); service.Clear();
        Assert.Equal(0, service.StoredCount);
        Assert.Single(unrelatedLog); Assert.Single(unrelatedClips);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(10001, 1000)]
    [InlineData(99, 1000)]
    [InlineData(100, 100)]
    public void InvalidMaximumIsCorrected(int input, int expected)
    {
        var config = LiveChatService.NormalizeConfig(new LiveChatConfig { MaxMessages = input });
        Assert.Equal(expected, config.MaxMessages);
    }

    [Theory]
    [InlineData(15, 28)]
    [InlineData(65, 28)]
    [InlineData(16, 16)]
    [InlineData(64, 64)]
    public void InvalidEmoteSizeIsCorrected(int input, int expected)
    {
        var config = LiveChatService.NormalizeConfig(new LiveChatConfig { EmoteSize = input });
        Assert.Equal(expected, config.EmoteSize);
    }

    [Fact]
    public async Task DisabledExternalProvidersPerformNoRequests()
    {
        var bttv = new FakeProvider("BTTV"); var seven = new FakeProvider("7TV");
        using var catalog = new EmoteCatalogService(new[] { bttv, seven });
        await catalog.InitializeAsync("channel", new LiveChatConfig(), default);
        Assert.Equal(0, bttv.Calls); Assert.Equal(0, seven.Calls);
    }

    [Fact]
    public async Task ProviderFailureDoesNotStopCatalog()
    {
        var failing = new FakeProvider("BTTV", true);
        using var catalog = new EmoteCatalogService(new[] { failing });
        await catalog.InitializeAsync("channel", new LiveChatConfig { EnableBttvEmotes = true }, default);
        Assert.Empty(catalog.Emotes); Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public void TwitchEmoteMetadataAndTextFallbackArePreserved()
    {
        var service = Create(new LiveChatConfig());
        var message = Message("1", "Kappa");
        message = new ChatMessage { Id = message.Id, UserId = message.UserId, UserLogin = message.UserLogin,
            UserName = message.UserName, Text = message.Text, Emotes = new[] { new ChatEmoteFragment("25", "Kappa") } };
        service.ProcessMessage(message);
        var stored = Assert.Single(service.GetVisibleSnapshot());
        Assert.Equal("Kappa", stored.Message);
        Assert.Single(stored.Emotes);
    }

    [Fact]
    public void SearchAndHighVolumeRemainBounded()
    {
        var service = Create(new LiveChatConfig { MaxMessages = 1000 });
        for (var i = 0; i < 5000; i++) service.ProcessMessage(Message(i.ToString(), i % 2 == 0 ? "alpha" : "beta"));
        Assert.Equal(1000, service.StoredCount);
        Assert.Equal(500, service.GetVisibleSnapshot("alpha").Count);
    }

    private static LiveChatService Create(LiveChatConfig config) =>
        new(config, "bot", Array.Empty<string>(), Registry());

    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        registry.Update(new AppConfig { Minigame = new MinigameConfig { PointsEnabled = true } });
        return registry;
    }

    private static ChatMessage Message(string id, string text, string login = "viewer") => new()
    { Id = id, UserId = login, UserLogin = login, UserName = login, Text = text, ReceivedAt = DateTimeOffset.Now };

    private sealed class FakeProvider(string name, bool fail = false) : IExternalEmoteProvider
    {
        public string Name => name;
        public int Calls { get; private set; }
        public Task<IReadOnlyDictionary<string, ExternalEmote>> LoadAsync(string channelId, CancellationToken cancellationToken)
        {
            Calls++;
            if (fail) throw new InvalidOperationException("test");
            return Task.FromResult<IReadOnlyDictionary<string, ExternalEmote>>(
                new Dictionary<string, ExternalEmote> { ["Kappa"] = new("Kappa", "https://example.invalid", false, Name) });
        }
    }
}
