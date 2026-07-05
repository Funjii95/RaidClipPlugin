using RaidClipPlugin.Models;


namespace RaidClipPlugin.Services;


public interface IMusicProvider
{
    MusicProviderType ProviderType { get; }
    string ProviderName { get; }
    MusicProviderCapabilities Capabilities { get; }

    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken);
    Task<MusicAuthResult> AuthenticateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SongSearchResult>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken);
    Task<ProviderSongRequestResult> AddSongRequestAsync(
        SongSearchResult track,
        CancellationToken cancellationToken);
}
