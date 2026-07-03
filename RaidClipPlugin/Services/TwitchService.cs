using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class TwitchService : ITwitchClipClient, IClipChatClient, IGiveawayTwitchClient
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
            user.GetProperty("display_name").GetString() ?? "",
            user.TryGetProperty("profile_image_url", out var profileImage)
                ? profileImage.GetString() ?? "" : "");
    }

    public async Task<TwitchChannelInfo?> GetChannelInfoAsync(
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        var url = "https://api.twitch.tv/helix/channels?broadcaster_id=" +
                  Uri.EscapeDataString(broadcasterId);
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        var channel = data[0];
        return new TwitchChannelInfo(
            channel.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
            channel.TryGetProperty("game_id", out var gameId) ? gameId.GetString() ?? "" : "",
            channel.TryGetProperty("game_name", out var gameName) ? gameName.GetString() ?? "" : "");
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

    public async Task<List<TwitchUser>> GetChattersAsync(
        string broadcasterId,
        string moderatorId,
        CancellationToken cancellationToken)
    {
        var chatters = new Dictionary<string, TwitchUser>(
            StringComparer.Ordinal);
        var cursor = "";

        do
        {
            var url =
                "https://api.twitch.tv/helix/chat/chatters" +
                $"?broadcaster_id={Uri.EscapeDataString(broadcasterId)}" +
                $"&moderator_id={Uri.EscapeDataString(moderatorId)}" +
                "&first=1000" +
                (string.IsNullOrWhiteSpace(cursor)
                    ? ""
                    : $"&after={Uri.EscapeDataString(cursor)}");

            using var response = await _http.GetAsync(
                url, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            foreach (var item in document.RootElement
                         .GetProperty("data").EnumerateArray())
            {
                var id = item.GetProperty("user_id").GetString() ?? "";
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                chatters[id] = new TwitchUser(
                    id,
                    item.GetProperty("user_login").GetString() ?? "",
                    item.GetProperty("user_name").GetString() ?? "");
            }

            cursor = document.RootElement.TryGetProperty(
                         "pagination", out var pagination) &&
                     pagination.TryGetProperty("cursor", out var cursorValue)
                ? cursorValue.GetString() ?? ""
                : "";
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        return chatters.Values.ToList();
    }

    public async Task<IReadOnlyList<TwitchCustomReward>> GetCustomRewardsAsync(
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        var url = "https://api.twitch.tv/helix/channel_points/custom_rewards" +
                  "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) +
                  "&only_manageable_rewards=false";
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => new TwitchCustomReward(
                item.GetProperty("id").GetString() ?? "",
                item.GetProperty("title").GetString() ?? "",
                item.TryGetProperty("is_user_input_required", out var input) &&
                input.GetBoolean(),
                !item.TryGetProperty("is_enabled", out var enabled) ||
                enabled.GetBoolean()))
            .ToArray();
    }

    public async Task UpdateRedemptionStatusAsync(
        string broadcasterId,
        string rewardId,
        string redemptionId,
        bool fulfilled,
        CancellationToken cancellationToken)
    {
        var url = "https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions" +
                  "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) +
                  "&reward_id=" + Uri.EscapeDataString(rewardId) +
                  "&id=" + Uri.EscapeDataString(redemptionId);
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(new
            {
                status = fulfilled ? "FULFILLED" : "CANCELED"
            })
        };
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
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

    public async Task<TwitchCreatedClip> CreateClipAsync(
        TwitchClipRequest request,
        CancellationToken cancellationToken)
    {
        var url = "https://api.twitch.tv/helix/clips" +
                  "?broadcaster_id=" +
                  Uri.EscapeDataString(request.BroadcasterId) +
                  "&has_delay=false";
        using var response = await _http.PostAsync(url, null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0)
            throw new InvalidOperationException(
                "Twitch hat keine Clip-ID zurückgegeben.");
        var item = data[0];
        return new TwitchCreatedClip(
            item.GetProperty("id").GetString() ?? "",
            item.TryGetProperty("edit_url", out var editUrl)
                ? editUrl.GetString() ?? "" : "");
    }

    public async Task<PublishedClip?> GetClipByIdAsync(
        string clipId,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            "https://api.twitch.tv/helix/clips?id=" +
            Uri.EscapeDataString(clipId), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        var item = data[0];
        return new PublishedClip(
            item.GetProperty("id").GetString() ?? "",
            item.GetProperty("url").GetString() ?? "",
            item.TryGetProperty("title", out var title)
                ? title.GetString() ?? "" : "",
            item.TryGetProperty("thumbnail_url", out var thumbnail)
                ? thumbnail.GetString() ?? "" : "",
            item.TryGetProperty("duration", out var duration)
                ? duration.GetDouble() : 0);
    }

    public async Task<TwitchLiveStream?> GetLiveStreamAsync(
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            "https://api.twitch.tv/helix/streams?user_id=" +
            Uri.EscapeDataString(broadcasterId), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        var item = data[0];
        var startedAt = item.TryGetProperty("started_at", out var started) &&
                        DateTimeOffset.TryParse(started.GetString(), out var parsed)
            ? parsed : DateTimeOffset.UtcNow;
        return new TwitchLiveStream(
            item.GetProperty("id").GetString() ?? "",
            item.GetProperty("user_id").GetString() ?? broadcasterId,
            item.GetProperty("user_login").GetString() ?? "",
            item.GetProperty("user_name").GetString() ?? "",
            item.GetProperty("game_id").GetString() ?? "",
            item.GetProperty("game_name").GetString() ?? "",
            startedAt,
            true);
    }

    public async Task<DateTimeOffset?> GetFollowedAtAsync(
        string broadcasterId,
        string userId,
        CancellationToken cancellationToken)
    {
        var url = "https://api.twitch.tv/helix/channels/followers" +
                  "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) +
                  "&user_id=" + Uri.EscapeDataString(userId);
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        return data[0].TryGetProperty("followed_at", out var followedAt) &&
               DateTimeOffset.TryParse(followedAt.GetString(), out var parsed)
            ? parsed : null;
    }

    public async Task<bool> IsFollowerAsync(
        string broadcasterId,
        string userId,
        CancellationToken cancellationToken)
    {
        var url = "https://api.twitch.tv/helix/channels/followers" +
                  "?broadcaster_id=" + Uri.EscapeDataString(broadcasterId) +
                  "&user_id=" + Uri.EscapeDataString(userId);
        using var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("data").GetArrayLength() > 0;
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
    string DisplayName,
    string ProfileImageUrl = "");

public sealed record TwitchChannelInfo(string Title, string GameId, string GameName);
