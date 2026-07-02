using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public sealed class AuthenticationService
{
    private const string DeviceUrl = "https://id.twitch.tv/oauth2/device";
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";
    private const string ValidateUrl = "https://id.twitch.tv/oauth2/validate";
    private const string RequiredScopeValue =
        "user:read:chat user:write:chat moderator:manage:shoutouts " +
        "moderator:manage:banned_users moderator:manage:chat_messages " +
        "moderator:read:chatters moderator:read:followers " +
        "channel:read:subscriptions channel:manage:redemptions";
    private static readonly string[] RequiredScopes =
        RequiredScopeValue.Split(' ');
    private readonly TwitchConfig _config;
    private readonly HttpClient _http = new();
    private readonly string _tokenPath;
    private readonly string _legacyTokenPath;

    public AuthenticationService(AppConfig config)
    {
        _config = config.Twitch;
        _http.Timeout = TimeSpan.FromSeconds(30);

        var directory = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RaidClipPlugin");
        Directory.CreateDirectory(directory);
        _tokenPath = Path.Combine(directory, "twitch-token.dat");
        _legacyTokenPath = Path.Combine(directory, "twitch-token.json");
    }

    public async Task<TwitchSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        var saved = await LoadTokenAsync(cancellationToken);
        if (saved is not null)
        {
            var valid = await ValidateAsync(saved.AccessToken, cancellationToken);
            if (valid is not null)
                return valid with { RefreshToken = saved.RefreshToken };

            if (!string.IsNullOrWhiteSpace(saved.RefreshToken))
            {
                var refreshed = await RefreshAsync(saved.RefreshToken, cancellationToken);
                if (refreshed is not null)
                    return refreshed;
            }
        }
        return await SignInAsync(cancellationToken);
    }

    private async Task<TwitchSession> SignInAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(DeviceUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["scopes"] = RequiredScopeValue
            }), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Twitch-Anmeldung konnte nicht gestartet werden ({(int)response.StatusCode}): {body}");

        var device = JsonSerializer.Deserialize<DeviceCodeResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Twitch hat keinen Gerätecode geliefert.");

        Console.WriteLine();
        Console.WriteLine("Twitch-Anmeldung erforderlich");
        Console.WriteLine($"1. Öffne: {device.VerificationUri}");
        Console.WriteLine($"2. Gib diesen Code ein: {device.UserCode}");
        Console.WriteLine();
        try { Process.Start(new ProcessStartInfo(device.VerificationUri) { UseShellExecute = true }); }
        catch { }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn);
        var interval = Math.Max(device.Interval, 1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
            using var tokenResponse = await _http.PostAsync(TokenUrl,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _config.ClientId,
                    ["device_code"] = device.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["scopes"] = RequiredScopeValue
                }), cancellationToken);
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            if (tokenResponse.IsSuccessStatusCode)
            {
                var token = JsonSerializer.Deserialize<TokenResponse>(tokenBody, JsonOptions)
                    ?? throw new InvalidOperationException("Twitch hat kein Token geliefert.");
                await SaveTokenAsync(token, cancellationToken);
                var session = await ValidateAsync(token.AccessToken, cancellationToken)
                    ?? throw new InvalidOperationException("Das neue Twitch-Token ist ungültig.");
                return session with { RefreshToken = token.RefreshToken };
            }

            var error = TryReadError(tokenBody);
            if (error == "authorization_pending") continue;
            if (error == "slow_down") { interval += 5; continue; }
            if (error is "expired_token" or "access_denied") break;
            throw new InvalidOperationException($"Twitch-Anmeldung fehlgeschlagen: {tokenBody}");
        }
        throw new TimeoutException("Die Twitch-Anmeldung wurde nicht rechtzeitig abgeschlossen.");
    }

    private async Task<TwitchSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _config.ClientId
        };

        if (!string.IsNullOrWhiteSpace(_config.ClientSecret))
        {
            fields["client_secret"] = _config.ClientSecret;
        }

        using var response = await _http.PostAsync(
            TokenUrl,
            new FormUrlEncodedContent(fields),
            cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
        if (token is null) return null;
        await SaveTokenAsync(token, cancellationToken);
        var session = await ValidateAsync(token.AccessToken, cancellationToken);
        return session is null ? null : session with { RefreshToken = token.RefreshToken };
    }

    private async Task<TwitchSession?> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ValidateUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var validation = await response.Content.ReadFromJsonAsync<ValidationResponse>(JsonOptions, cancellationToken);
        if (validation is null ||
            validation.ClientId != _config.ClientId ||
            validation.Scopes is null ||
            RequiredScopes.Except(
                validation.Scopes,
                StringComparer.Ordinal).Any())
        {
            return null;
        }

        return new TwitchSession(
            accessToken,
            "",
            validation.UserId,
            validation.Login);
    }

    private async Task<TokenResponse?> LoadTokenAsync(
        CancellationToken cancellationToken)
    {
        if (File.Exists(_tokenPath))
        {
            try
            {
                var protectedData = await File.ReadAllBytesAsync(
                    _tokenPath,
                    cancellationToken);
                return WindowsProtectedStore.UnprotectJson<TokenResponse>(
                    protectedData);
            }
            catch
            {
                return null;
            }
        }

        if (!File.Exists(_legacyTokenPath))
        {
            return null;
        }

        try
        {
            TokenResponse? legacyToken;
            await using (var stream = File.OpenRead(_legacyTokenPath))
            {
                legacyToken = await JsonSerializer.DeserializeAsync<TokenResponse>(
                    stream,
                    JsonOptions,
                    cancellationToken);
            }

            if (legacyToken is null)
            {
                return null;
            }

            await SaveTokenAsync(legacyToken, cancellationToken);
            File.Delete(_legacyTokenPath);
            Console.WriteLine(
                "🔒 Vorhandenes Twitch-Token wurde verschlüsselt übernommen.");
            return legacyToken;
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveTokenAsync(
        TokenResponse token,
        CancellationToken cancellationToken)
    {
        var protectedData = WindowsProtectedStore.ProtectJson(token);
        var temporaryPath = _tokenPath + ".tmp";

        await File.WriteAllBytesAsync(
            temporaryPath,
            protectedData,
            cancellationToken);
        File.Move(temporaryPath, _tokenPath, overwrite: true);
    }

    private static string? TryReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() : null;
        }
        catch { return null; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private sealed record DeviceCodeResponse(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] string VerificationUri,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval);
    private sealed record ValidationResponse(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("scopes")] string[]? Scopes);
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);
}

public sealed record TwitchSession(string AccessToken, string RefreshToken, string UserId, string Login);
