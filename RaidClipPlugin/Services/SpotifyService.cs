using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class SpotifyService : IDisposable
{
    public const string RequiredScopes =
        "user-read-playback-state user-modify-playback-state " +
        "user-read-currently-playing user-read-private";
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string ApiUrl = "https://api.spotify.com/v1";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly string _tokenPath;
    private MusicRequestConfig _config;
    private SpotifyToken? _token;

    public bool IsConnected => _token is not null;
    public string AccountName { get; private set; } = "";

    public SpotifyService(MusicRequestConfig config)
    {
        _config = config;
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin");
        Directory.CreateDirectory(directory);
        _tokenPath = Path.Combine(directory, "spotify-token.dat");
        LoadToken();
    }

    public void UpdateConfig(MusicRequestConfig config)
    {
        if (_token is not null && !_config.SpotifyClientId.Equals(
                config.SpotifyClientId, StringComparison.Ordinal))
        {
            _token = null;
            AccountName = "";
            if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
        }
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.SpotifyClientId))
            throw new InvalidOperationException("Bitte eine Spotify Client-ID eingeben.");

        var redirect = new Uri(_config.RedirectUri);
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUrl = AuthorizeUrl +
            $"?client_id={Uri.EscapeDataString(_config.SpotifyClientId)}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirect.AbsoluteUri)}" +
            $"&scope={Uri.EscapeDataString(RequiredScopes)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            "&code_challenge_method=S256" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect.AbsoluteUri);
        listener.Start();
        Process.Start(new ProcessStartInfo(authorizationUrl)
        {
            UseShellExecute = true
        });

        using var registration = cancellationToken.Register(listener.Stop);
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync();
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        var returnedState = context.Request.QueryString["state"];
        var code = context.Request.QueryString["code"];
        var error = context.Request.QueryString["error"];
        var responseHtml = string.IsNullOrWhiteSpace(error)
            ? "<h2>Spotify wurde verbunden.</h2><p>Dieses Fenster kann geschlossen werden.</p>"
            : "<h2>Spotify-Anmeldung abgebrochen.</h2>";
        var responseBytes = Encoding.UTF8.GetBytes(
            "<!doctype html><meta charset=utf-8><body style='font-family:sans-serif;background:#111;color:#eee;padding:40px'>" +
            responseHtml + "</body>");
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = responseBytes.Length;
        await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
        context.Response.Close();

        if (!string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException("Spotify-Anmeldung wurde abgelehnt.");
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(returnedState ?? ""),
                Encoding.UTF8.GetBytes(state)))
            throw new InvalidOperationException("Spotify OAuth-State ist ungültig.");
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Spotify hat keinen Anmeldecode geliefert.");

        using var tokenResponse = await _http.PostAsync(TokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _config.SpotifyClientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirect.AbsoluteUri,
                ["code_verifier"] = verifier
            }), cancellationToken);
        await EnsureSuccessAsync(tokenResponse, "Spotify-Anmeldung", cancellationToken);
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Spotify hat kein Token geliefert.");
        _token = new SpotifyToken(token.AccessToken, token.RefreshToken ?? "",
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn)),
            token.Scope ?? RequiredScopes, _config.SpotifyClientId);
        SaveToken();
        AccountName = await GetAccountNameAsync(cancellationToken);
        Console.WriteLine("Spotify-Verbindung erfolgreich hergestellt.");
    }

    public void Disconnect()
    {
        _token = null;
        AccountName = "";
        if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
        Console.WriteLine("Spotify-Konto wurde getrennt.");
    }

    public async Task<string> GetAccountNameAsync(CancellationToken cancellationToken)
    {
        using var document = await SendJsonAsync(
            () => new HttpRequestMessage(HttpMethod.Get, ApiUrl + "/me"),
            cancellationToken);
        var root = document.RootElement;
        AccountName = root.TryGetProperty("display_name", out var display)
            ? display.GetString() ?? "Spotify-Nutzer"
            : root.GetProperty("id").GetString() ?? "Spotify-Nutzer";
        return AccountName;
    }

    public async Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(
        CancellationToken cancellationToken)
    {
        using var document = await SendJsonAsync(
            () => new HttpRequestMessage(HttpMethod.Get, ApiUrl + "/me/player/devices"),
            cancellationToken);
        return document.RootElement.GetProperty("devices").EnumerateArray()
            .Select(item => new SpotifyDevice(
                item.GetProperty("id").GetString() ?? "",
                item.GetProperty("name").GetString() ?? "Unbekannt",
                item.GetProperty("type").GetString() ?? "Unbekannt",
                item.GetProperty("is_active").GetBoolean(),
                item.GetProperty("is_restricted").GetBoolean()))
            .Where(device => !string.IsNullOrWhiteSpace(device.Id) &&
                             !device.IsRestricted)
            .ToArray();
    }

    public Task<SpotifyTrack?> SearchTrackAsync(
        string query, CancellationToken cancellationToken) =>
        GetTrackFromJsonAsync(
            () => new HttpRequestMessage(HttpMethod.Get,
                ApiUrl + "/search?type=track&limit=1&market=from_token&q=" +
                Uri.EscapeDataString(query)),
            root => root.GetProperty("tracks").GetProperty("items") is var items &&
                    items.GetArrayLength() > 0 ? items[0] : (JsonElement?)null,
            cancellationToken);

    public Task<SpotifyTrack?> GetTrackAsync(
        string trackId, CancellationToken cancellationToken) =>
        GetTrackFromJsonAsync(
            () => new HttpRequestMessage(HttpMethod.Get,
                ApiUrl + "/tracks/" + Uri.EscapeDataString(trackId) +
                "?market=from_token"),
            root => root,
            cancellationToken);

    public async Task<SpotifyTrack?> GetCurrentTrackAsync(
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get,
                ApiUrl + "/me/player/currently-playing"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        await EnsureSuccessAsync(response, "Aktueller Spotify-Track", cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("item", out var item) &&
               item.ValueKind == JsonValueKind.Object
            ? ParseTrack(item)
            : null;
    }

    public async Task AddToQueueAsync(
        SpotifyTrack track, string? deviceId,
        CancellationToken cancellationToken)
    {
        var url = ApiUrl + "/me/player/queue?uri=" +
                  Uri.EscapeDataString(track.Uri) +
                  (string.IsNullOrWhiteSpace(deviceId) ? "" :
                      "&device_id=" + Uri.EscapeDataString(deviceId));
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, url), cancellationToken);
        await EnsureSuccessAsync(response, "Spotify-Warteschlange", cancellationToken);
    }

    public async Task PlayAsync(
        SpotifyTrack track, string? deviceId,
        CancellationToken cancellationToken)
    {
        var url = ApiUrl + "/me/player/play" +
                  (string.IsNullOrWhiteSpace(deviceId) ? "" :
                      "?device_id=" + Uri.EscapeDataString(deviceId));
        using var response = await SendAsync(() => new HttpRequestMessage(
            HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new { uris = new[] { track.Uri } })
        }, cancellationToken);
        await EnsureSuccessAsync(response, "Spotify-Wiedergabe", cancellationToken);
    }

    public async Task TransferPlaybackAsync(
        string deviceId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(
            HttpMethod.Put, ApiUrl + "/me/player")
        {
            Content = JsonContent.Create(new
            {
                device_ids = new[] { deviceId }, play = false
            })
        }, cancellationToken);
        await EnsureSuccessAsync(response, "Spotify-Gerätewechsel", cancellationToken);
    }

    public Task SkipAsync(CancellationToken cancellationToken) =>
        SendNoContentAsync(HttpMethod.Post, "/me/player/next", cancellationToken);
    public Task PauseAsync(CancellationToken cancellationToken) =>
        SendNoContentAsync(HttpMethod.Put, "/me/player/pause", cancellationToken);
    public Task ResumeAsync(CancellationToken cancellationToken) =>
        SendNoContentAsync(HttpMethod.Put, "/me/player/play", cancellationToken);

    private async Task SendNoContentAsync(
        HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(method, ApiUrl + path), cancellationToken);
        await EnsureSuccessAsync(response, "Spotify-Player", cancellationToken);
    }

    private async Task<SpotifyTrack?> GetTrackFromJsonAsync(
        Func<HttpRequestMessage> requestFactory,
        Func<JsonElement, JsonElement?> select,
        CancellationToken cancellationToken)
    {
        using var document = await SendJsonAsync(requestFactory, cancellationToken);
        var selected = select(document.RootElement);
        return selected is null ? null : ParseTrack(selected.Value);
    }

    private static SpotifyTrack ParseTrack(JsonElement item)
    {
        var artists = item.GetProperty("artists").EnumerateArray()
            .Select(artist => artist.GetProperty("name").GetString() ?? "")
            .Where(name => name.Length > 0);
        var externalUrl = item.TryGetProperty("external_urls", out var urls) &&
                          urls.TryGetProperty("spotify", out var spotifyUrl)
            ? spotifyUrl.GetString() ?? "" : "";
        var isPlayable = (!item.TryGetProperty("is_playable", out var playable) ||
                          playable.GetBoolean()) &&
                         !(item.TryGetProperty("restrictions", out var restrictions) &&
                           restrictions.ValueKind == JsonValueKind.Object);
        return new SpotifyTrack(
            item.GetProperty("id").GetString() ?? "",
            item.GetProperty("uri").GetString() ?? "",
            item.GetProperty("name").GetString() ?? "",
            string.Join(", ", artists),
            item.GetProperty("duration_ms").GetInt32(),
            item.GetProperty("explicit").GetBoolean(),
            isPlayable,
            externalUrl,
            item.TryGetProperty("is_local", out var local) && local.GetBoolean(),
            item.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "");
    }

    private async Task<JsonDocument> SendJsonAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(requestFactory, cancellationToken);
        await EnsureSuccessAsync(response, "Spotify API", cancellationToken);
        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var token = await GetAccessTokenAsync(attempt > 0, cancellationToken);
            using var request = requestFactory();
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                response.Dispose();
                continue;
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                response.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(
                    Math.Clamp(delay.TotalSeconds, 1, 10)), cancellationToken);
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Spotify API ist vorübergehend nicht erreichbar.");
    }

    private async Task<string> GetAccessTokenAsync(
        bool forceRefresh, CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is null)
                throw new InvalidOperationException("Spotify ist nicht verbunden.");
            if (!forceRefresh &&
                _token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return _token.AccessToken;
            if (string.IsNullOrWhiteSpace(_token.RefreshToken))
                throw new InvalidOperationException(
                    "Spotify-Token ist abgelaufen. Bitte Spotify erneut verbinden.");

            using var response = await _http.PostAsync(TokenUrl,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _config.SpotifyClientId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _token.RefreshToken
                }), cancellationToken);
            await EnsureSuccessAsync(response, "Spotify-Token-Aktualisierung",
                cancellationToken);
            var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException(
                    "Spotify hat kein aktualisiertes Token geliefert.");
            _token = new SpotifyToken(refreshed.AccessToken,
                string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                    ? _token.RefreshToken : refreshed.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, refreshed.ExpiresIn)),
                refreshed.Scope ?? _token.Scope, _config.SpotifyClientId);
            SaveToken();
            Console.WriteLine("Spotify-Zugriffstoken wurde aktualisiert.");
            return _token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var safeMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Spotify-Anmeldung ist abgelaufen.",
            HttpStatusCode.Forbidden =>
                "Spotify verweigert die Player-Steuerung. Premium, Berechtigungen und Gerät prüfen.",
            HttpStatusCode.NotFound => "Spotify-Gerät oder Titel wurde nicht gefunden.",
            HttpStatusCode.TooManyRequests => "Spotify-Ratenlimit wurde erreicht.",
            _ => $"{operation} fehlgeschlagen ({(int)response.StatusCode})."
        };
        throw new SpotifyApiException(safeMessage, response.StatusCode,
            response.StatusCode is HttpStatusCode.TooManyRequests or
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable,
            body.Length > 0 && body.Length < 500 ? body : "");
    }

    private void LoadToken()
    {
        if (!File.Exists(_tokenPath)) return;
        try
        {
            _token = WindowsProtectedStore.UnprotectJson<SpotifyToken>(
                File.ReadAllBytes(_tokenPath));
            if (_token is not null && !_token.ClientId.Equals(
                    _config.SpotifyClientId, StringComparison.Ordinal))
                _token = null;
        }
        catch
        {
            _token = null;
        }
    }

    private void SaveToken()
    {
        if (_token is null) return;
        var temporary = _tokenPath + ".tmp";
        File.WriteAllBytes(temporary, WindowsProtectedStore.ProtectJson(_token));
        File.Move(temporary, _tokenPath, true);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose()
    {
        _http.Dispose();
        _tokenLock.Dispose();
    }

    private sealed record SpotifyToken(
        string AccessToken, string RefreshToken,
        DateTimeOffset ExpiresAt, string Scope, string ClientId);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}

public sealed class SpotifyApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public bool IsTemporary { get; }
    public string SafeDetails { get; }

    public SpotifyApiException(
        string message, HttpStatusCode statusCode,
        bool isTemporary, string safeDetails) : base(message)
    {
        StatusCode = statusCode;
        IsTemporary = isTemporary;
        SafeDetails = safeDetails;
    }
}
