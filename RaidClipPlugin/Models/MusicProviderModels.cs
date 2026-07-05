namespace RaidClipPlugin.Models;


public enum MusicProviderType
{
    Disabled,
    Spotify,
    Tidal,
    InternalQueue
}


public sealed record MusicProviderCapabilities
{
    public bool SupportsAuthentication { get; init; }
    public bool SupportsSearch { get; init; }
    public bool SupportsMetadata { get; init; }
    public bool SupportsLinks { get; init; }
    public bool SupportsEmbeds { get; init; }
    public bool SupportsInternalQueue { get; init; } = true;
    public bool SupportsQueueControl { get; init; }
    public bool SupportsPlaybackControl { get; init; }
    public bool SupportsDeviceSelection { get; init; }
    public bool SupportsConnectDevices { get; init; }
    public string Limitation { get; init; } = "";
}


public sealed record MusicAuthResult(
    bool Success,
    string AccountName = "",
    string ErrorMessage = "");


public sealed record SongSearchResult(
    MusicProviderType Provider,
    string Id,
    string Name,
    string Artist,
    string Album,
    int DurationMs,
    bool Explicit,
    bool IsPlayable,
    string ExternalUrl,
    string EmbedUrl = "");


public sealed record ProviderSongRequestResult(
    bool Success,
    string Message,
    SongSearchResult? Track = null,
    bool RequiresManualPlayback = false);
