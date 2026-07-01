using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class TwitchService
{
    private readonly HttpClient _http = new();

    public TwitchService(string clientId, string accessToken)
    {
        _http.DefaultRequestHeaders.Add("Client-Id", clientId);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<TwitchUser?> GetUserAsync(
        string login,
        CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(login)
            ? "https://api.twitch.tv/helix/users"
            : $"https://api.twitch.tv/helix/users?login={Uri.EscapeDataString(login)}";

        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        var data = document.RootElement.GetProperty("data");

        if (data.GetArrayLength() == 0)
        {
            return null;
        }

        var user = data[0];

        return new TwitchUser(
            user.GetProperty("id").GetString() ?? "",
            user.GetProperty("login").GetString() ?? "",
            user.GetProperty("display_name").GetString() ?? "");
    }

    public async Task<List<Clip>> GetClipsAsync(
        string broadcasterId,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        var escapedBroadcasterId = Uri.EscapeDataString(broadcasterId);
        var startedAt = Uri.EscapeDataString(
            DateTimeOffset.UtcNow
                .AddDays(-Math.Max(1, lookbackDays))
                .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        var endedAt = Uri.EscapeDataString(
            DateTimeOffset.UtcNow
                .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));

        var periodUrl =
            "https://api.twitch.tv/helix/clips" +
            $"?broadcaster_id={escapedBroadcasterId}" +
            $"&started_at={startedAt}" +
            $"&ended_at={endedAt}" +
            "&first=100";

        var clips = await FetchClipsAsync(periodUrl, cancellationToken);

        if (clips.Count > 0)
        {
            return clips;
        }

        Console.WriteLine(
            $"ℹ️ Keine Clips der letzten {lookbackDays} Tage gefunden. " +
            "Suche jetzt die Top-Clips des Kanals ohne Zeitbegrenzung …");

        var allTimeUrl =
            "https://api.twitch.tv/helix/clips" +
            $"?broadcaster_id={escapedBroadcasterId}" +
            "&first=100";

        return await FetchClipsAsync(allTimeUrl, cancellationToken);
    }

    private async Task<List<Clip>> FetchClipsAsync(
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        var clips = new List<Clip>();
        var data = document.RootElement.GetProperty("data");

        foreach (var item in data.EnumerateArray())
        {
            clips.Add(new Clip
            {
                Id = item.GetProperty("id").GetString() ?? "",
                Url = item.GetProperty("url").GetString() ?? "",
                EmbedUrl = item.TryGetProperty("embed_url", out var embedUrl)
                    ? embedUrl.GetString() ?? ""
                    : "",
                Title = item.GetProperty("title").GetString() ?? "",
                DurationSeconds = item.TryGetProperty("duration", out var duration)
                    ? duration.GetDouble()
                    : 0,
                ThumbnailUrl = item.GetProperty("thumbnail_url").GetString() ?? ""
            });
        }

        return clips;
    }

    public async Task<Clip?> GetRandomClipAsync(
        string broadcasterId,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        var clips = await GetClipsAsync(
            broadcasterId,
            lookbackDays,
            cancellationToken);

        if (clips.Count == 0)
        {
            return null;
        }

        return clips[Random.Shared.Next(clips.Count)];
    }

    public async Task SendChatMessageAsync(
        string broadcasterId,
        string senderId,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var text = message.Length > 500
            ? message[..500]
            : message;

        using var response = await _http.PostAsJsonAsync(
            "https://api.twitch.tv/helix/chat/messages",
            new
            {
                broadcaster_id = broadcasterId,
                sender_id = senderId,
                message = text
            },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var result = document.RootElement.GetProperty("data")[0];

        if (!result.GetProperty("is_sent").GetBoolean())
        {
            var reason = result.TryGetProperty("drop_reason", out var dropReason) &&
                         dropReason.ValueKind == JsonValueKind.Object
                ? dropReason.GetProperty("message").GetString()
                : "Twitch hat die Nachricht verworfen.";
            throw new InvalidOperationException(reason);
        }
    }

    public async Task SendShoutoutAsync(
        string fromBroadcasterId,
        string toBroadcasterId,
        string moderatorId,
        CancellationToken cancellationToken)
    {
        var url =
            "https://api.twitch.tv/helix/chat/shoutouts" +
            $"?from_broadcaster_id={Uri.EscapeDataString(fromBroadcasterId)}" +
            $"&to_broadcaster_id={Uri.EscapeDataString(toBroadcasterId)}" +
            $"&moderator_id={Uri.EscapeDataString(moderatorId)}";

        using var response = await _http.PostAsync(
            url,
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new HttpRequestException(
            $"Twitch API meldet {(int)response.StatusCode} " +
            $"{response.StatusCode}: {body}");
    }
}

public sealed record TwitchUser(
    string Id,
    string Login,
    string DisplayName);
