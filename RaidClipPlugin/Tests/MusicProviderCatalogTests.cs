using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;


namespace RaidClipPlugin.Tests;


public sealed class MusicProviderCatalogTests
{
    [Fact]
    public void SpotifySupportsCurrentRaidClipPlaybackFeatures()
    {
        var capabilities = MusicProviderCatalog.GetCapabilities(
            MusicProviderType.Spotify);

        Assert.True(capabilities.SupportsSearch);
        Assert.True(capabilities.SupportsMetadata);
        Assert.True(capabilities.SupportsQueueControl);
        Assert.True(capabilities.SupportsPlaybackControl);
        Assert.True(capabilities.SupportsDeviceSelection);
    }


    [Fact]
    public void TidalIsRestrictedToOfficialCatalogLinksAndEmbeds()
    {
        var capabilities = MusicProviderCatalog.GetCapabilities(
            MusicProviderType.Tidal);

        Assert.True(capabilities.SupportsAuthentication);
        Assert.True(capabilities.SupportsSearch);
        Assert.True(capabilities.SupportsMetadata);
        Assert.True(capabilities.SupportsLinks);
        Assert.True(capabilities.SupportsEmbeds);
        Assert.False(capabilities.SupportsQueueControl);
        Assert.False(capabilities.SupportsPlaybackControl);
        Assert.False(capabilities.SupportsDeviceSelection);
        Assert.False(capabilities.SupportsConnectDevices);
    }


    [Fact]
    public void UnsupportedTidalPlaybackIsRejectedBeforeSideEffects()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            MusicProviderCatalog.Require(
                MusicProviderType.Tidal,
                capabilities => capabilities.SupportsPlaybackControl,
                "Automatische Wiedergabe"));

        Assert.Contains("nicht offiziell unterstützt", error.Message);
        Assert.Contains("TIDAL Connect", error.Message);
    }


    [Fact]
    public void InternalQueueRequiresNoProviderAuthentication()
    {
        var capabilities = MusicProviderCatalog.GetCapabilities(
            MusicProviderType.InternalQueue);

        Assert.True(capabilities.SupportsInternalQueue);
        Assert.False(capabilities.SupportsAuthentication);
        Assert.False(capabilities.SupportsSearch);
        Assert.False(capabilities.SupportsPlaybackControl);
    }


    [Fact]
    public void DisabledProviderHasNoQueueCapability()
    {
        var capabilities = MusicProviderCatalog.GetCapabilities(
            MusicProviderType.Disabled);

        Assert.False(capabilities.SupportsInternalQueue);
        Assert.False(capabilities.SupportsSearch);
        Assert.False(capabilities.SupportsPlaybackControl);
    }
}
