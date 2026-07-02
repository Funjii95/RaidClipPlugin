using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class MinigameEventService : IDisposable
{
    private const string EventSubUrl =
        "wss://eventsub.wss.twitch.tv/ws";
    private const string SubscriptionsUrl =
        "https://api.twitch.tv/helix/eventsub/subscriptions";
    private readonly string _clientId;
    private readonly string _accessToken;
    private readonly string _broadcasterId;
    private readonly string _moderatorId;
    private readonly MinigameConfig _config;
    private readonly string _musicRewardId;
    private readonly HttpClient _http = new();

    public event Func<MinigamePassiveEvent, Task>? EventReceived;
    public event Func<MusicRequestRedemption, Task>? MusicRedemptionReceived;
    public event Action? Activated;

    public MinigameEventService(
        string clientId, string accessToken, string broadcasterId,
        string moderatorId, MinigameConfig config,
        string musicRewardId = "")
    {
        _clientId = clientId;
        _accessToken = accessToken;
        _broadcasterId = broadcasterId;
        _moderatorId = moderatorId;
        _config = config;
        _musicRewardId = musicRewardId;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Minigame-EventSub unterbrochen: " + exception.Message);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ConnectAndListenAsync(
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(EventSubUrl), cancellationToken);

        while (socket.State == WebSocketState.Open)
        {
            var json = await ReceiveTextAsync(socket, cancellationToken);
            if (json is null) return;
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.GetProperty("metadata")
                .GetProperty("message_type").GetString();

            if (type == "session_welcome")
            {
                var sessionId = root.GetProperty("payload")
                    .GetProperty("session").GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    await CreateSubscriptionsAsync(
                        sessionId, cancellationToken);
                }
                Activated?.Invoke();
                Console.WriteLine("Gemeinsame Minigame-/Musikwunsch-Ereignisse sind aktiv.");
            }
            else if (type == "notification")
            {
                await HandleNotificationAsync(root);
            }
            else if (type == "revocation")
            {
                Console.WriteLine(
                    "Eine passive Minigame-Subscription wurde widerrufen.");
            }
        }
    }

    private async Task CreateSubscriptionsAsync(
        string sessionId, CancellationToken cancellationToken)
    {
        if (_config.FollowPointsEnabled)
        {
            await TryCreateSubscriptionAsync(
                "channel.follow", "2",
                new
                {
                    broadcaster_user_id = _broadcasterId,
                    moderator_user_id = _moderatorId
                },
                sessionId, cancellationToken);
        }

        if (_config.SubPointsEnabled)
        {
            await TryCreateSubscriptionAsync(
                "channel.subscribe", "1",
                new { broadcaster_user_id = _broadcasterId },
                sessionId, cancellationToken);
        }

        if (_config.ChannelRewardPointsEnabled ||
            !string.IsNullOrWhiteSpace(_musicRewardId))
        {
            object condition = _config.ChannelRewardPointsEnabled
                ? new { broadcaster_user_id = _broadcasterId }
                : new { broadcaster_user_id = _broadcasterId, reward_id = _musicRewardId };
            await TryCreateSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.add", "1",
                condition, sessionId, cancellationToken);
        }
    }

    private async Task TryCreateSubscriptionAsync(
        string type, string version, object condition, string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await CreateSubscriptionAsync(
                type, version, condition, sessionId, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Passive Punktequelle {type} ist nicht verfügbar: " +
                exception.Message);
        }
    }

    private async Task CreateSubscriptionAsync(
        string type, string version, object condition, string sessionId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, SubscriptionsUrl);
        request.Headers.Add("Client-Id", _clientId);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = JsonContent.Create(new
        {
            type,
            version,
            condition,
            transport = new { method = "websocket", session_id = sessionId }
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"{type}-Subscription fehlgeschlagen " +
            $"({(int)response.StatusCode}): {body}");
    }

    private async Task HandleNotificationAsync(JsonElement root)
    {
        var payload = root.GetProperty("payload");
        var type = payload.GetProperty("subscription")
            .GetProperty("type").GetString();
        var data = payload.GetProperty("event");
        var kind = type switch
        {
            "channel.follow" => MinigamePassiveEventKind.Follow,
            "channel.subscribe" => MinigamePassiveEventKind.Subscription,
            "channel.channel_points_custom_reward_redemption.add" =>
                MinigamePassiveEventKind.ChannelReward,
            _ => (MinigamePassiveEventKind?)null
        };
        var userId = data.TryGetProperty("user_id", out var id)
            ? id.GetString() ?? "" : "";
        var displayName = data.TryGetProperty("user_name", out var name)
            ? name.GetString() ?? userId : userId;
        if (kind is not null &&
            (kind != MinigamePassiveEventKind.ChannelReward ||
             _config.ChannelRewardPointsEnabled) &&
            EventReceived is { } handler)
        {
            await handler(new MinigamePassiveEvent(
                kind.Value, userId, displayName));
        }

        if (type == "channel.channel_points_custom_reward_redemption.add" &&
            !string.IsNullOrWhiteSpace(_musicRewardId) &&
            data.TryGetProperty("reward", out var reward) &&
            string.Equals(reward.GetProperty("id").GetString(),
                _musicRewardId, StringComparison.Ordinal) &&
            MusicRedemptionReceived is { } musicHandler)
        {
            await musicHandler(new MusicRequestRedemption(
                data.GetProperty("id").GetString() ?? "",
                reward.GetProperty("id").GetString() ?? "",
                reward.GetProperty("title").GetString() ?? "",
                userId,
                data.TryGetProperty("user_login", out var login)
                    ? login.GetString() ?? "" : "",
                displayName,
                data.TryGetProperty("user_input", out var input)
                    ? input.GetString() ?? "" : "",
                data.GetProperty("redeemed_at").GetDateTimeOffset(),
                data.GetProperty("status").GetString() ?? "unknown"));
        }
    }

    private static async Task<string?> ReceiveTextAsync(
        ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Dispose() => _http.Dispose();
}
