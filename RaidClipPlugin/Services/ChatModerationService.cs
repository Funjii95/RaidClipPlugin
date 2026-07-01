using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ChatModerationService : IDisposable
{
    private const string EventSubUrl = "wss://eventsub.wss.twitch.tv/ws";
    private const string SubscriptionsUrl = "https://api.twitch.tv/helix/eventsub/subscriptions";
    private const string BansUrl = "https://api.twitch.tv/helix/moderation/bans";
    private const string DeleteMessageUrl = "https://api.twitch.tv/helix/moderation/chat";

    private readonly string _clientId;
    private readonly string _accessToken;
    private readonly string _broadcasterId;
    private readonly string _moderatorId;
    private readonly HttpClient _http = new();
    private bool _disposed;

    public event Func<ChatMessage, Task>? MessageReceived;
    public event Action? Activated;

    public ChatModerationService(
        string clientId,
        string accessToken,
        string broadcasterId,
        string moderatorId)
    {
        _clientId = clientId;
        _accessToken = accessToken;
        _broadcasterId = broadcasterId;
        _moderatorId = moderatorId;
        _http.DefaultRequestHeaders.Add("Client-Id", clientId);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var url = EventSubUrl;
        var subscribe = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var reconnectUrl = await ConnectAndListenAsync(
                    url,
                    subscribe,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(reconnectUrl))
                {
                    url = reconnectUrl;
                    subscribe = false;
                    continue;
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Chat-Moderation wurde unterbrochen: " + exception.Message);
            }

            url = EventSubUrl;
            subscribe = true;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public Task TimeoutUserAsync(
        string userId,
        int durationSeconds,
        string reason,
        CancellationToken cancellationToken)
    {
        var duration = Math.Clamp(durationSeconds, 1, 1_209_600);
        return ModerateUserAsync(userId, duration, reason, cancellationToken);
    }

    public Task BanUserAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken) =>
        ModerateUserAsync(userId, null, reason, cancellationToken);

    public async Task DeleteMessageAsync(
        string messageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Die Nachrichten-ID fehlt.", nameof(messageId));
        }

        var url =
            $"{DeleteMessageUrl}?broadcaster_id={Uri.EscapeDataString(_broadcasterId)}" +
            $"&moderator_id={Uri.EscapeDataString(_moderatorId)}" +
            $"&message_id={Uri.EscapeDataString(messageId)}";

        using var response = await _http.DeleteAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task ModerateUserAsync(
        string userId,
        int? durationSeconds,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Die Nutzer-ID fehlt.", nameof(userId));
        }

        var url =
            $"{BansUrl}?broadcaster_id={Uri.EscapeDataString(_broadcasterId)}" +
            $"&moderator_id={Uri.EscapeDataString(_moderatorId)}";

        object data = durationSeconds is null
            ? new
            {
                user_id = userId,
                reason = LimitReason(reason)
            }
            : new
            {
                user_id = userId,
                duration = durationSeconds.Value,
                reason = LimitReason(reason)
            };

        using var response = await _http.PostAsJsonAsync(
            url,
            new { data },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<string?> ConnectAndListenAsync(
        string url,
        bool subscribe,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(url), cancellationToken);

        while (socket.State == WebSocketState.Open)
        {
            var json = await ReceiveTextAsync(socket, cancellationToken);
            if (json is null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root
                .GetProperty("metadata")
                .GetProperty("message_type")
                .GetString();

            switch (type)
            {
                case "session_welcome":
                    var sessionId = root
                        .GetProperty("payload")
                        .GetProperty("session")
                        .GetProperty("id")
                        .GetString();

                    if (subscribe && !string.IsNullOrWhiteSpace(sessionId))
                    {
                        await CreateSubscriptionAsync(
                            sessionId,
                            cancellationToken);
                    }

                    Console.WriteLine("Chat-Moderation ist aktiv.");
                    Activated?.Invoke();
                    break;

                case "notification":
                    HandleNotification(root);
                    break;

                case "session_reconnect":
                    return root
                        .GetProperty("payload")
                        .GetProperty("session")
                        .GetProperty("reconnect_url")
                        .GetString();

                case "revocation":
                    Console.WriteLine(
                        "Twitch hat die Chat-Subscription widerrufen.");
                    break;
            }
        }

        return null;
    }

    private async Task CreateSubscriptionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            SubscriptionsUrl);
        request.Headers.Add("Client-Id", _clientId);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = JsonContent.Create(new
        {
            type = "channel.chat.message",
            version = "1",
            condition = new
            {
                broadcaster_user_id = _broadcasterId,
                user_id = _moderatorId
            },
            transport = new
            {
                method = "websocket",
                session_id = sessionId
            }
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private void HandleNotification(JsonElement root)
    {
        var payload = root.GetProperty("payload");
        var subscriptionType = payload
            .GetProperty("subscription")
            .GetProperty("type")
            .GetString();

        if (subscriptionType != "channel.chat.message")
        {
            return;
        }

        var data = payload.GetProperty("event");
        var badgeIds = data.TryGetProperty("badges", out var badges)
            ? badges.EnumerateArray()
                .Select(badge => badge.GetProperty("set_id").GetString() ?? "")
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var message = new ChatMessage
        {
            Id = data.GetProperty("message_id").GetString() ?? "",
            UserId = data.GetProperty("chatter_user_id").GetString() ?? "",
            UserLogin = data.GetProperty("chatter_user_login").GetString() ?? "",
            UserName = data.GetProperty("chatter_user_name").GetString() ?? "",
            Text = data.GetProperty("message").GetProperty("text").GetString() ?? "",
            ReceivedAt = DateTimeOffset.Now,
            IsModerator = badgeIds.Contains("moderator") || badgeIds.Contains("staff"),
            IsVip = badgeIds.Contains("vip"),
            IsBroadcaster = badgeIds.Contains("broadcaster")
        };

        if (MessageReceived is { } handler)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await handler(message);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "Chatnachricht konnte nicht verarbeitet werden: " +
                        exception.Message);
                }
            });
        }
    }

    private static string LimitReason(string reason)
    {
        var text = string.IsNullOrWhiteSpace(reason)
            ? "Moderation über RaidClipPlugin"
            : reason.Trim();
        return text.Length <= 500 ? text : text[..500];
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
            $"Twitch Moderation meldet {(int)response.StatusCode} " +
            $"{response.StatusCode}: {body}");
    }

    private static async Task<string?> ReceiveTextAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _http.Dispose();
        _disposed = true;
    }
}
