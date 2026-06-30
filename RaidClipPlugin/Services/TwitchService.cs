using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace RaidClipPlugin.Services;

public class TwitchService
{
    private readonly HttpClient _httpClient = new();

    public TwitchService(string clientId, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<string?> GetUserIdAsync(string login)
    {
        var response = await _httpClient.GetAsync(
            $"https://api.twitch.tv/helix/users?login={login}");

        Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync();

        Console.WriteLine(json);

        if (!response.IsSuccessStatusCode)
            return null;

        var obj = JObject.Parse(json);

        return obj["data"]?.First?["id"]?.ToString();
    }
    public HttpClient HttpClient => _httpClient;
}
