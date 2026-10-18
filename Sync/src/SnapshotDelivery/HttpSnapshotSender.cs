using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SnapshotProtocol;

namespace SnapshotDelivery;

public enum DeliveryStatus { Empty, Delivered, RetryLater, Blocked }
public sealed record DeliveryResult(DeliveryStatus Status, SnapshotDecision? Decision = null);

public sealed class HttpSnapshotSender : IDisposable
{
    private readonly Uri endpoint;
    private readonly string credential;
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpSnapshotSender(Uri endpoint, string credential) : this(endpoint, credential,
        new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, CheckCertificateRevocationList = true }, false) { }

    // Only the test assembly may use an in-process transport or unencrypted loopback.
    internal HttpSnapshotSender(Uri endpoint, string credential, HttpMessageHandler handler, bool allowLoopback)
    {
        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != "https" &&
            !(allowLoopback && endpoint.Scheme == "http" && endpoint.IsLoopback)) ||
            endpoint.AbsolutePath != "/api/v1/snapshots" || endpoint.Query.Length > 0 ||
            endpoint.Fragment.Length > 0 || endpoint.UserInfo.Length > 0)
            throw new ArgumentException("Explicit HTTPS snapshot endpoint required.");
        byte[] key;
        try { key = Convert.FromBase64String(credential); }
        catch (FormatException) { throw new ArgumentException("Invalid publisher credential."); }
        if (key.Length is < 32 or > 64 || credential != Convert.ToBase64String(key))
            throw new ArgumentException("Invalid publisher credential.");
        this.endpoint = endpoint;
        this.credential = credential;
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<DeliveryResult> SendAsync(SnapshotMessage message, CancellationToken ct = default)
    {
        var validated = SnapshotValidation.Validate(message, message.ChannelId);
        var clean = message with { AccountJson = validated.AccountJson };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(clean);
        if (bytes.Length > SnapshotValidation.MaxMessageBytes) throw new InvalidDataException("Message too large.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500)
                return new(DeliveryStatus.RetryLater);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > 4096 ||
                response.Content.Headers.ContentType?.MediaType != "application/json")
                return new(DeliveryStatus.Blocked);
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var body = new MemoryStream();
            var chunk = new byte[1024];
            while (true)
            {
                var count = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, 4097 - (int)body.Length)), deadline.Token);
                if (count == 0) break;
                body.Write(chunk, 0, count);
                if (body.Length > 4096) return new(DeliveryStatus.Blocked);
            }
            using var document = JsonDocument.Parse(body.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new(DeliveryStatus.Blocked);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!names.Add(property.Name)) return new(DeliveryStatus.Blocked);
            var ack = document.RootElement.Deserialize<SnapshotAcknowledgement>(JsonOptions);
            if (ack is null || ack.ContractVersion != 1 || ack.ChannelId != message.ChannelId ||
                ack.UserId != message.UserId || ack.Revision != message.Revision || ack.SnapshotId != message.SnapshotId ||
                ack.Decision is not ("Applied" or "Duplicate" or "Stale") ||
                !Enum.TryParse<SnapshotDecision>(ack.Decision, out var decision))
                return new(DeliveryStatus.Blocked);
            return new(DeliveryStatus.Delivered, decision);
        }
        catch (JsonException) { return new(DeliveryStatus.Blocked); }
        catch (HttpRequestException) { return new(DeliveryStatus.RetryLater); }
        catch (IOException) { return new(DeliveryStatus.RetryLater); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(DeliveryStatus.RetryLater); }
    }

    public void Dispose() => client.Dispose();
}
