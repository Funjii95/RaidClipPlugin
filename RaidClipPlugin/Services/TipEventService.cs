using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class TipEventService : IDisposable
{
    private const string StreamElementsUrl = "wss://astro.streamelements.com/";
    private readonly TipProviderConfig _config;
    private readonly HttpClient _http = new();
    private HttpListener? _listener;
    public event Func<ChatAlertEvent, Task>? TipReceived;
    public event Action<string>? StatusChanged;

    public TipEventService(TipProviderConfig config) => _config = config;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        if (_config.StreamElements.Enabled)
            tasks.Add(RunStreamElementsAsync(cancellationToken));
        if (_config.Streamlabs.Enabled)
            tasks.Add(RunStreamlabsAsync(cancellationToken));
        if (_config.KoFi.Enabled || _config.TipeeeStream.Enabled)
            tasks.Add(RunWebhookListenerAsync(cancellationToken));
        if (tasks.Count == 0)
        {
            StatusChanged?.Invoke("Kein Tip-Anbieter aktiviert");
            return;
        }
        await Task.WhenAll(tasks);
    }

    private async Task RunStreamElementsAsync(CancellationToken cancellationToken)
    {
        var settings = _config.StreamElements;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri(StreamElementsUrl), cancellationToken);
                _ = await ReceiveAsync(socket, cancellationToken);
                var request = JsonSerializer.Serialize(new
                {
                    type = "subscribe",
                    nonce = Guid.NewGuid().ToString("N"),
                    data = new
                    {
                        topic = "channel.tips",
                        room = settings.ChannelId.Trim(),
                        token = settings.Token.Trim(),
                        token_type = settings.TokenType.Trim().ToLowerInvariant()
                    }
                });
                await socket.SendAsync(Encoding.UTF8.GetBytes(request),
                    WebSocketMessageType.Text, true, cancellationToken);
                StatusChanged?.Invoke("StreamElements verbunden");
                while (socket.State == WebSocketState.Open)
                {
                    var json = await ReceiveAsync(socket, cancellationToken);
                    if (json is null) break;
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;
                    if (GetString(root, "topic") != "channel.tips" ||
                        !root.TryGetProperty("data", out var data) ||
                        GetString(data, "status") != "success" ||
                        GetString(data, "approved") == "denied" ||
                        !data.TryGetProperty("donation", out var donation))
                        continue;
                    var user = donation.TryGetProperty("user", out var userData)
                        ? GetString(userData, "username")
                        : "Anonym";
                    await EmitAsync(new ChatAlertEvent(
                        ChatAlertKind.Tip,
                        string.IsNullOrWhiteSpace(user) ? "Anonym" : user,
                        GetDecimal(donation, "amount"),
                        GetString(donation, "currency"),
                        GetString(donation, "message"),
                        Provider: "StreamElements"));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("StreamElements: " + exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task RunStreamlabsAsync(CancellationToken cancellationToken)
    {
        var settings = _config.Streamlabs;
        long newestId = 0;
        var initialized = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://streamlabs.com/api/v2.0/donations?limit=20&verified=1");
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", settings.AccessToken.Trim());
                using var response = await _http.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));
                var root = document.RootElement;
                var donations = root.TryGetProperty("data", out var data) ? data : root;
                if (donations.ValueKind == JsonValueKind.Array)
                {
                    var current = new List<(long Id, ChatAlertEvent Alert)>();
                    foreach (var donation in donations.EnumerateArray())
                    {
                        var id = GetLong(donation, "donation_id");
                        if (id == 0) id = GetLong(donation, "id");
                        current.Add((id, new ChatAlertEvent(
                            ChatAlertKind.Tip,
                            GetString(donation, "name"),
                            GetDecimal(donation, "amount"),
                            GetString(donation, "currency"),
                            GetString(donation, "message"),
                            Provider: "Streamlabs")));
                    }
                    if (initialized)
                        foreach (var item in current.Where(item => item.Id > newestId)
                                     .OrderBy(item => item.Id))
                            await EmitAsync(item.Alert);
                    if (current.Count > 0) newestId = Math.Max(newestId, current.Max(x => x.Id));
                    initialized = true;
                }
                StatusChanged?.Invoke("Streamlabs verbunden");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("Streamlabs: " + exception.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(
                Math.Clamp(settings.PollIntervalSeconds, 10, 300)), cancellationToken);
        }
    }

    private async Task RunWebhookListenerAsync(CancellationToken cancellationToken)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://127.0.0.1:17892/");
        _listener.Start();
        StatusChanged?.Invoke("Tip-Webhooks lokal auf Port 17892 aktiv");
        using var registration = cancellationToken.Register(() => _listener.Stop());
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception) when (cancellationToken.IsCancellationRequested) { break; }
            _ = HandleWebhookAsync(context, cancellationToken);
        }
    }

    private async Task HandleWebhookAsync(
        HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath.Trim('/') ?? "";
            var provider = path.Equals(_config.KoFi.Path, StringComparison.OrdinalIgnoreCase)
                ? (Name: "Ko-fi", Settings: _config.KoFi)
                : path.Equals(_config.TipeeeStream.Path, StringComparison.OrdinalIgnoreCase)
                    ? (Name: "TipeeeStream", Settings: _config.TipeeeStream)
                    : default;
            if (provider.Settings is null || !provider.Settings.Enabled)
            {
                context.Response.StatusCode = 404;
                return;
            }
            using var reader = new StreamReader(context.Request.InputStream,
                context.Request.ContentEncoding ?? Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);
            if (provider.Name == "Ko-fi" && body.StartsWith("data=", StringComparison.Ordinal))
                body = WebUtility.UrlDecode(body[5..]);
            using var document = JsonDocument.Parse(body);
            var data = document.RootElement;
            if (data.TryGetProperty("data", out var nested) && nested.ValueKind == JsonValueKind.Object)
                data = nested;
            var suppliedToken = provider.Name == "Ko-fi"
                ? GetString(data, "verification_token")
                : context.Request.Headers["X-RaidClip-Token"] ?? "";
            if (!string.IsNullOrWhiteSpace(provider.Settings.VerificationToken) &&
                !string.Equals(suppliedToken, provider.Settings.VerificationToken,
                    StringComparison.Ordinal))
            {
                context.Response.StatusCode = 401;
                return;
            }
            await EmitAsync(new ChatAlertEvent(
                ChatAlertKind.Tip,
                FirstString(data, "from_name", "username", "name", "displayName"),
                FirstDecimal(data, "amount", "amount_received", "value"),
                FirstString(data, "currency", "currency_code"),
                FirstString(data, "message", "comment"),
                Provider: provider.Name));
            context.Response.StatusCode = 204;
        }
        catch
        {
            context.Response.StatusCode = 400;
        }
        finally { context.Response.Close(); }
    }

    private Task EmitAsync(ChatAlertEvent alert) =>
        TipReceived is { } handler ? handler(alert) : Task.CompletedTask;

    private static async Task<string?> ReceiveAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string FirstString(JsonElement data, params string[] names) =>
        names.Select(name => GetString(data, name))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Anonym";

    private static decimal FirstDecimal(JsonElement data, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetDecimal(data, name);
            if (value != 0) return value;
        }
        return 0;
    }

    private static string GetString(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) ? value.ToString() : "";

    private static decimal GetDecimal(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) &&
        decimal.TryParse(value.ToString(), NumberStyles.Number,
            CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static long GetLong(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) &&
        long.TryParse(value.ToString(), out var result) ? result : 0;

    public void Dispose()
    {
        _listener?.Close();
        _http.Dispose();
    }
}
