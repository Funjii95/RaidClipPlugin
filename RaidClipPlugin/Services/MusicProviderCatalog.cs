using RaidClipPlugin.Models;


namespace RaidClipPlugin.Services;


public static class MusicProviderCatalog
{
    public static MusicProviderCapabilities GetCapabilities(
        MusicProviderType provider) => provider switch
        {
            MusicProviderType.Spotify => new MusicProviderCapabilities
            {
                SupportsAuthentication = true,
                SupportsSearch = true,
                SupportsMetadata = true,
                SupportsLinks = true,
                SupportsEmbeds = true,
                SupportsQueueControl = true,
                SupportsPlaybackControl = true,
                SupportsDeviceSelection = true,
                SupportsConnectDevices = true
            },
            MusicProviderType.Tidal => new MusicProviderCapabilities
            {
                SupportsAuthentication = true,
                SupportsSearch = true,
                SupportsMetadata = true,
                SupportsLinks = true,
                SupportsEmbeds = true,
                SupportsQueueControl = false,
                SupportsPlaybackControl = false,
                SupportsDeviceSelection = false,
                SupportsConnectDevices = false,
                Limitation =
                    "Die öffentliche TIDAL API unterstützt Katalog und Metadaten. " +
                    "Offizielles Playback ist auf TIDAL SDKs/Embeds beschränkt; " +
                    "TIDAL Connect steht nur Gerätepartnern zur Verfügung."
            },
            MusicProviderType.InternalQueue => new MusicProviderCapabilities
            {
                SupportsInternalQueue = true,
                Limitation =
                    "Titel werden nur lokal vorgemerkt und nicht automatisch gesucht " +
                    "oder abgespielt."
            },
            _ => new MusicProviderCapabilities
            {
                SupportsInternalQueue = false,
                Limitation = "Musikwünsche sind deaktiviert."
            }
        };


    public static bool CanUse(
        MusicProviderType provider,
        Func<MusicProviderCapabilities, bool> capability) =>
        capability(GetCapabilities(provider));


    public static void Require(
        MusicProviderType provider,
        Func<MusicProviderCapabilities, bool> capability,
        string operation)
    {
        var capabilities = GetCapabilities(provider);
        if (capability(capabilities)) return;
        throw new NotSupportedException(
            $"{operation} wird von {provider} nicht offiziell unterstützt. " +
            capabilities.Limitation);
    }
}
