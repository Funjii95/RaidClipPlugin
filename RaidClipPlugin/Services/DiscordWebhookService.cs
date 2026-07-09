using System.Net;
using System.Net.Http.Json;

namespace RaidClipPlugin.Services;

public sealed class DiscordWebhookService : IDisposable
{
    private readonly HttpClient _http = new();

    public async Task<string> SendClipAsync(
        string webhookUrl,
        object payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new InvalidOperationException("Discord Webhook URL fehlt.");

        var url = webhookUrl.Contains("wait=true", StringComparison.OrdinalIgnoreCase)
            ? webhookUrl
            : webhookUrl + (webhookUrl.Contains('?') ? "&wait=true" : "?wait=true");

        using var response = await _http.PostAsJsonAsync(
            url, payload, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ??
                             TimeSpan.FromSeconds(3);
            await Task.Delay(retryAfter, cancellationToken);
            using var retry = await _http.PostAsJsonAsync(
                url, payload, cancellationToken);
            await EnsureSuccessAsync(retry, cancellationToken);
            return await TryReadMessageIdAsync(retry, cancellationToken);
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await TryReadMessageIdAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Discord Webhook meldet {(int)response.StatusCode} {response.StatusCode}: {body}");
    }

    private static async Task<string> TryReadMessageIdAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("id", out var id)
                ? id.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    public void Dispose() => _http.Dispose();
}
