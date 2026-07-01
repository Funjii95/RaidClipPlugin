using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class EventSubService
{
    private const string DefaultUrl = "wss://eventsub.wss.twitch.tv/ws";
    private const string SubscriptionsUrl = "https://api.twitch.tv/helix/eventsub/subscriptions";
    private readonly string _clientId;
    private readonly string _accessToken;
    private readonly string _broadcasterId;
    private readonly HttpClient _http = new();
    public event Func<RaidEvent, Task>? RaidReceived;
    public event Action? Activated;

    public EventSubService(string clientId, string accessToken, string broadcasterId)
    {
        _clientId = clientId;
        _accessToken = accessToken;
        _broadcasterId = broadcasterId;
    }

    public async Task TestConnectionAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(DefaultUrl), timeout.Token);
        var json = await ReceiveTextAsync(socket, timeout.Token)
            ?? throw new InvalidOperationException(
                "Twitch EventSub hat keine Begrüßung gesendet.");

        using var document = JsonDocument.Parse(json);
        var messageType = document.RootElement
            .GetProperty("metadata")
            .GetProperty("message_type")
            .GetString();

        if (messageType != "session_welcome")
        {
            throw new InvalidOperationException(
                "Unerwartete EventSub-Antwort: " + messageType);
        }

        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Test abgeschlossen",
                CancellationToken.None);
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var url = DefaultUrl;
        var subscribe = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var reconnect = await ConnectAndListenAsync(url, subscribe, cancellationToken);
                if (!string.IsNullOrWhiteSpace(reconnect))
                {
                    url = reconnect;
                    subscribe = false;
                    continue;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Console.WriteLine($"EventSub-Verbindung unterbrochen: {exception.Message}");
            }
            url = DefaultUrl;
            subscribe = true;
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private async Task<string?> ConnectAndListenAsync(string url, bool subscribe,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(url), cancellationToken);
        while (socket.State == WebSocketState.Open)
        {
            var json = await ReceiveTextAsync(socket, cancellationToken);
            if (json is null) return null;
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.GetProperty("metadata").GetProperty("message_type").GetString();
            switch (type)
            {
                case "session_welcome":
                    var sessionId = root.GetProperty("payload").GetProperty("session").GetProperty("id").GetString();
                    if (subscribe && !string.IsNullOrWhiteSpace(sessionId))
                        await CreateSubscriptionAsync(sessionId, cancellationToken);
                    Console.WriteLine("Raid-Erkennung ist aktiv.");
                    Activated?.Invoke();
                    break;
                case "notification":
                    await HandleNotificationAsync(root);
                    break;
                case "session_reconnect":
                    return root.GetProperty("payload").GetProperty("session")
                        .GetProperty("reconnect_url").GetString();
                case "revocation":
                    Console.WriteLine("Twitch hat die Raid-Subscription widerrufen.");
                    break;
            }
        }
        return null;
    }

    private async Task CreateSubscriptionAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SubscriptionsUrl);
        request.Headers.Add("Client-Id", _clientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = JsonContent.Create(new
        {
            type = "channel.raid",
            version = "1",
            condition = new { to_broadcaster_user_id = _broadcasterId },
            transport = new { method = "websocket", session_id = sessionId }
        });
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Raid-Subscription fehlgeschlagen ({(int)response.StatusCode}): {body}");
    }

    private async Task HandleNotificationAsync(JsonElement root)
    {
        var payload = root.GetProperty("payload");
        if (payload.GetProperty("subscription").GetProperty("type").GetString() != "channel.raid") return;
        var data = payload.GetProperty("event");
        var raid = new RaidEvent
        {
            FromBroadcasterId = data.GetProperty("from_broadcaster_user_id").GetString() ?? "",
            FromBroadcasterLogin = data.GetProperty("from_broadcaster_user_login").GetString() ?? "",
            FromBroadcasterName = data.GetProperty("from_broadcaster_user_name").GetString() ?? "",
            Viewers = data.GetProperty("viewers").GetInt32()
        };
        if (RaidReceived is { } handler) _ = handler(raid);
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
