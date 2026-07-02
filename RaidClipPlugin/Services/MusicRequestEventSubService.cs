using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class MusicRequestEventSubService : IDisposable
{
    private const string DefaultUrl = "wss://eventsub.wss.twitch.tv/ws";
    private const string SubscriptionsUrl =
        "https://api.twitch.tv/helix/eventsub/subscriptions";
    private readonly string _clientId;
    private readonly string _accessToken;
    private readonly string _broadcasterId;
    private readonly string _rewardId;
    private readonly HttpClient _http = new();
    private bool _subscriptionActive;

    public event Func<MusicRequestRedemption, Task>? RedemptionReceived;
    public event Action? Activated;

    public MusicRequestEventSubService(
        string clientId, string accessToken,
        string broadcasterId, string rewardId)
    {
        _clientId = clientId;
        _accessToken = accessToken;
        _broadcasterId = broadcasterId;
        _rewardId = rewardId;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var url = DefaultUrl;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                url = await ConnectAndListenAsync(url, cancellationToken) ??
                      DefaultUrl;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "Musikwunsch-EventSub unterbrochen: " + exception.Message);
                _subscriptionActive = false;
                url = DefaultUrl;
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task<string?> ConnectAndListenAsync(
        string url, CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(url), cancellationToken);
        while (socket.State == WebSocketState.Open)
        {
            var json = await ReceiveTextAsync(socket, cancellationToken);
            if (json is null)
            {
                _subscriptionActive = false;
                return null;
            }
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.GetProperty("metadata")
                .GetProperty("message_type").GetString();
            if (type == "session_welcome")
            {
                var sessionId = root.GetProperty("payload")
                    .GetProperty("session").GetProperty("id").GetString();
                if (string.IsNullOrWhiteSpace(sessionId))
                    throw new InvalidOperationException(
                        "Twitch hat keine EventSub-Session-ID geliefert.");
                if (!_subscriptionActive)
                {
                    await CreateSubscriptionAsync(sessionId, cancellationToken);
                    _subscriptionActive = true;
                }
                Activated?.Invoke();
                Console.WriteLine("Musikwunsch-EventSub ist aktiv.");
            }
            else if (type == "session_reconnect")
            {
                return root.GetProperty("payload").GetProperty("session")
                    .GetProperty("reconnect_url").GetString();
            }
            else if (type == "notification")
            {
                await HandleNotificationAsync(root);
            }
            else if (type == "revocation")
            {
                Console.WriteLine(
                    "Musikwunsch-EventSub-Subscription wurde widerrufen.");
            }
        }
        _subscriptionActive = false;
        return null;
    }

    private async Task CreateSubscriptionAsync(
        string sessionId, CancellationToken cancellationToken)
    {
        object condition = string.IsNullOrWhiteSpace(_rewardId)
            ? new { broadcaster_user_id = _broadcasterId }
            : new { broadcaster_user_id = _broadcasterId, reward_id = _rewardId };
        using var request = new HttpRequestMessage(
            HttpMethod.Post, SubscriptionsUrl);
        request.Headers.Add("Client-Id", _clientId);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = JsonContent.Create(new
        {
            type = "channel.channel_points_custom_reward_redemption.add",
            version = "1",
            condition,
            transport = new { method = "websocket", session_id = sessionId }
        });
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Musikwunsch-Subscription fehlgeschlagen ({(int)response.StatusCode}): {body}");
    }

    private async Task HandleNotificationAsync(JsonElement root)
    {
        var payload = root.GetProperty("payload");
        if (payload.GetProperty("subscription").GetProperty("type")
                .GetString() !=
            "channel.channel_points_custom_reward_redemption.add") return;
        var data = payload.GetProperty("event");
        var reward = data.GetProperty("reward");
        var redemption = new MusicRequestRedemption(
            data.GetProperty("id").GetString() ?? "",
            reward.GetProperty("id").GetString() ?? "",
            reward.GetProperty("title").GetString() ?? "",
            data.GetProperty("user_id").GetString() ?? "",
            data.GetProperty("user_login").GetString() ?? "",
            data.GetProperty("user_name").GetString() ?? "",
            data.GetProperty("user_input").GetString() ?? "",
            data.GetProperty("redeemed_at").GetDateTimeOffset(),
            data.GetProperty("status").GetString() ?? "unknown");
        if (RedemptionReceived is { } handler) await handler(redemption);
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
