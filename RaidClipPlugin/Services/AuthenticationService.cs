using System.Text.Json;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public class AuthenticationService
{
    private readonly AppConfig _config;
    private readonly HttpClient _http = new();

    public AuthenticationService(AppConfig config)
    {
        _config = config;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var values = new Dictionary<string, string>
        {
            { "client_id", _config.Twitch.ClientId },
            { "client_secret", _config.Twitch.ClientSecret },
            { "grant_type", "client_credentials" }
        };

        var response = await _http.PostAsync(
            "https://id.twitch.tv/oauth2/token",
            new FormUrlEncodedContent(values));

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
                  .GetProperty("access_token")
                  .GetString()!;
    }
}