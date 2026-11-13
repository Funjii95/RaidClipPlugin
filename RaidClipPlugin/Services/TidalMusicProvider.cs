using System.Globalization;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Credential-free, manual TIDAL link/Embed trial. Never controls Spotify or fulfils rewards.</summary>
public sealed class TidalMusicProvider : IMusicProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _file;
    public TidalMusicProvider(string? directory = null) => _file = Path.Combine(
        directory ?? Path.Combine(TestDataPaths.LocalAppData, "RaidClipPlugin"), "tidal-test-queue.json");
    public MusicProviderType ProviderType => MusicProviderType.Tidal;
    public string ProviderName => "TIDAL (Test)";
    // These are the implemented trial capabilities, not the theoretical API capabilities.
    public MusicProviderCapabilities Capabilities => new()
    {
        SupportsLinks = true, SupportsEmbeds = true, SupportsInternalQueue = true,
        Limitation = "Nur TIDAL-Track-Links und manuelle Wiedergabe im Original-Embed. Keine Textsuche, Desktop-Fernsteuerung oder automatische Wiedergabebestätigung."
    };
    public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    public static bool IsTrustedNavigation(string input) => Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        (uri.IdnHost.Equals("tidal.com", StringComparison.OrdinalIgnoreCase) || uri.IdnHost.EndsWith(".tidal.com", StringComparison.OrdinalIgnoreCase));
    public Task<MusicAuthResult> AuthenticateAsync(CancellationToken cancellationToken) => Task.FromResult(
        new MusicAuthResult(false, ErrorMessage: "Dieser Test besitzt keinen API-Login. Eine Anmeldung erfolgt ausschließlich im offiziellen TIDAL-Player; ein dortiger Login wird vom Plugin nicht als API-Anmeldung gewertet."));

    public static bool TryResolveTrack(string? input, out SongSearchResult track)
    {
        track = null!;
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        if (host is not ("tidal.com" or "www.tidal.com" or "listen.tidal.com" or "embed.tidal.com")) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        string? id = host == "embed.tidal.com"
            ? parts.Length == 2 && parts[0] == "tracks" ? parts[1] : null
            : parts.Length == 2 && parts[0] == "track" ? parts[1]
            : parts.Length == 3 && parts[0] == "browse" && parts[1] == "track" ? parts[2] : null;
        if (id is null || id.Length > 20 || id.Any(c => c is < '0' or > '9') ||
            !ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number == 0) return false;
        id = number.ToString(CultureInfo.InvariantCulture);
        track = new(MusicProviderType.Tidal, id, "TIDAL-Link " + id + " (ungeprüft)", "", "", 0,
            false, false, "https://tidal.com/track/" + id, "https://embed.tidal.com/tracks/" + id);
        return true;
    }

    public Task<IReadOnlyList<SongSearchResult>> SearchTracksAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveTrack(query, out var track)) throw new ArgumentException(
            "Bitte einen vollständigen HTTPS-TIDAL-Track-Link einfügen. Textsuche benötigt eine eigene Entwickler-App; Kurzlinks, Alben und Playlists werden im Test nicht aufgelöst.");
        return Task.FromResult<IReadOnlyList<SongSearchResult>>([track]);
    }

    public async Task<ProviderSongRequestResult> AddSongRequestAsync(SongSearchResult track, CancellationToken cancellationToken)
    {
        // Revalidate rather than trusting URLs or provider flags supplied by a caller or saved file.
        if (track.Provider != MusicProviderType.Tidal || !TryResolveTrack(track.ExternalUrl, out var canonical) || canonical.Id != track.Id)
            return new(false, "Kein gültiger TIDAL-Track-Link.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadUnsafeAsync(cancellationToken);
            if (entries.Any(e => e.Id == canonical.Id)) return new(false, "Dieser TIDAL-Link ist bereits lokal vorgemerkt.");
            if (entries.Count >= 100) return new(false, "Die TIDAL-Testliste ist voll (maximal 100 Links).");
            entries.Add(canonical);
            await WriteUnsafeAsync(entries, cancellationToken);
            return new(true, "Lokal vorgemerkt – nicht in die TIDAL-App eingereiht und nicht abgespielt.", canonical, true);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<SongSearchResult>> GetEntriesAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await ReadUnsafeAsync(ct); } finally { _gate.Release(); }
    }
    public async Task RemoveAsync(string id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { var entries = await ReadUnsafeAsync(ct); entries.RemoveAll(e => e.Id == id); await WriteUnsafeAsync(entries, ct); }
        finally { _gate.Release(); }
    }
    private async Task<List<SongSearchResult>> ReadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(_file)) return [];
        if (new FileInfo(_file).Length > 1_000_000) throw new InvalidDataException("Die TIDAL-Testliste ist zu groß; vorhandene Datei bleibt erhalten.");
        var entries = JsonSerializer.Deserialize<List<SongSearchResult>>(await File.ReadAllTextAsync(_file, ct));
        if (entries is null || entries.Count > 100 || entries.Any(e => e is null || e.Provider != MusicProviderType.Tidal ||
            !TryResolveTrack(e.ExternalUrl, out var verified) || verified.Id != e.Id))
            throw new InvalidDataException("Die TIDAL-Testliste ist beschädigt; vorhandene Datei bleibt erhalten.");
        return entries.Select(e => { TryResolveTrack(e.ExternalUrl, out var canonical); return canonical; }).ToList();
    }
    private async Task WriteUnsafeAsync(List<SongSearchResult> entries, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temporary = _file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(entries), ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, _file, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
