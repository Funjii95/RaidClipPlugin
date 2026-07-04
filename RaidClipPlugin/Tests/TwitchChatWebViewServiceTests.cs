using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class TwitchChatWebViewServiceTests
{
    [Fact]
    public void ChatUriUsesNormalizedConfiguredChannel()
    {
        var uri = TwitchChatWebViewService.CreateChatUri(" @Funjii ");
        Assert.Equal("https://www.twitch.tv/popout/funjii/chat?popout=", uri.AbsoluteUri);
    }

    [Fact]
    public void MissingChannelReturnsUnderstandableError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TwitchChatWebViewService.CreateChatUri("  "));
        Assert.Equal("Kein Twitch-Kanal konfiguriert.", exception.Message);
    }

    [Fact]
    public void PopoutDimensionsAreNormalizedWithoutChangingOtherSettings()
    {
        var config = new LiveChatConfig
        {
            ShowBadges = false,
            PopoutWidth = 10,
            PopoutHeight = 9000,
            PopoutLeft = -10,
            PopoutTop = -20
        };
        var result = LiveChatService.NormalizeConfig(config);
        Assert.False(result.ShowBadges);
        Assert.Equal(520, result.PopoutWidth);
        Assert.Equal(760, result.PopoutHeight);
        Assert.Equal(-1, result.PopoutLeft);
        Assert.Equal(-1, result.PopoutTop);
    }

    [Fact]
    public void OfficialExtensionDefaultsAndSourcesAreStable()
    {
        var config = new LiveChatConfig();
        Assert.True(config.EnableOfficialSevenTvExtension);
        Assert.True(config.EnableOfficialBttvExtension);
        Assert.Equal("github.com", ChatExtensionManager.SevenTvPackageUri.Host);
        Assert.Contains("SevenTV/Extension",
            ChatExtensionManager.SevenTvPackageUri.AbsoluteUri);
        Assert.Equal("github.com", ChatExtensionManager.BetterTtvPackageUri.Host);
        Assert.Contains("night/betterttv",
            ChatExtensionManager.BetterTtvPackageUri.AbsoluteUri);
    }

    [Fact]
    public async Task PlaceholderProviderHasStableConnectionLifecycle()
    {
        var provider = new PlaceholderChatProvider();
        await provider.ConnectAsync("Funjii", CancellationToken.None);
        Assert.True(provider.IsConnected);
        await provider.DisconnectAsync();
        Assert.False(provider.IsConnected);
    }
}
