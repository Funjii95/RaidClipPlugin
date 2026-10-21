using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SnapshotProtocol;

namespace SnapshotDelivery;

public sealed class SnapshotHeadReadException(bool retryable) : Exception("Snapshot metadata request failed.")
{
    public bool Retryable { get; } = retryable;
}

public sealed class HttpSnapshotHeadReader : IDisposable
{
    private readonly Uri endpoint;
    private readonly string channel;
    private readonly HttpClient client;
    public HttpSnapshotHeadReader(Uri endpoint, string credential, string channel)
        : this(endpoint, credential, channel, new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false, CheckCertificateRevocationList = true }, false) { }

    internal HttpSnapshotHeadReader(Uri endpoint, string credential, string channel, HttpMessageHandler handler, bool allowLoopback)
    {
        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != "https" && !(allowLoopback && endpoint.Scheme == "http" && endpoint.IsLoopback)) ||
            endpoint.AbsolutePath != "/api/v1/snapshots" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0 || !Numeric(channel))
            throw new ArgumentException("Explicit trusted snapshot endpoint required.");
        byte[] key;
        try { key = Convert.FromBase64String(credential); }
        catch (FormatException) { throw new ArgumentException("Invalid credential."); }
        if (key.Length is < 32 or > 64 || credential != Convert.ToBase64String(key)) throw new ArgumentException("Invalid credential.");
        this.endpoint = endpoint;
        this.channel = channel;
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
    }

    public async Task<StoredSnapshot?> ReadAsync(string userId, CancellationToken ct = default)
    {
        if (!Numeric(userId)) throw new ArgumentException("Invalid user.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await client.GetAsync(new Uri(endpoint.AbsoluteUri + "/" + userId + "/head"),
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode != HttpStatusCode.OK)
                throw new SnapshotHeadReadException((int)response.StatusCode is 408 or 429 or >= 500);
            if (response.Content.Headers.ContentType?.MediaType != "application/json" || response.Content.Headers.ContentLength > 4096)
                throw new SnapshotHeadReadException(false);
            await using var body = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4097];
            while (true)
            {
                var count = await body.ReadAsync(chunk.AsMemory(0, 4097 - (int)buffer.Length), deadline.Token);
                if (count == 0) break;
                buffer.Write(chunk, 0, count);
                if (buffer.Length > 4096) throw new SnapshotHeadReadException(false);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new SnapshotHeadReadException(false);
            var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            string[] required = ["contractVersion", "channelId", "userId", "revision", "snapshotId", "capturedAt", "deleted", "contentHash"];
            if (names.Length != required.Length || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length ||
                required.Any(name => !names.Contains(name, StringComparer.OrdinalIgnoreCase))) throw new SnapshotHeadReadException(false);
            var head = document.RootElement.Deserialize<SnapshotHead>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            if (head.ContractVersion != 1 || head.ChannelId != channel || head.UserId != userId || head.Revision <= 0 ||
                head.SnapshotId == Guid.Empty || head.CapturedAt == default || head.CapturedAt.Offset != TimeSpan.Zero ||
                head.ContentHash is not { Length: 64 } || head.ContentHash.Any(c => !Uri.IsHexDigit(c)))
                throw new SnapshotHeadReadException(false);
            return new(head.ChannelId, head.UserId, head.Revision, head.SnapshotId, head.CapturedAt, head.Deleted, null, head.ContentHash);
        }
        catch (HttpRequestException) { throw new SnapshotHeadReadException(true); }
        catch (IOException) { throw new SnapshotHeadReadException(true); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new SnapshotHeadReadException(true); }
        catch (JsonException) { throw new SnapshotHeadReadException(false); }
    }

    public async Task RequireFullProjectionAsync(IReadOnlyCollection<string> allowedUsers, CancellationToken ct=default, bool automaticPlayerEnrollment=false)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response=await client.GetAsync(new Uri(endpoint.AbsoluteUri+"/capabilities"),HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(response.StatusCode!=HttpStatusCode.OK)throw new SnapshotHeadReadException((int)response.StatusCode is 408 or 429 or >=500);
            await using var input=await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer=new MemoryStream();var chunk=new byte[4097];
            while(true){var n=await input.ReadAsync(chunk.AsMemory(0,4097-(int)buffer.Length),deadline.Token);if(n==0)break;buffer.Write(chunk,0,n);if(buffer.Length>4096)throw new SnapshotHeadReadException(false);}
            using var document=JsonDocument.Parse(buffer.ToArray());var root=document.RootElement;
            var names=root.EnumerateObject().Select(p=>p.Name).ToArray();
            if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Length ||
                (root.GetProperty("rpgProjectionVersion").GetInt32()!=SnapshotValidation.RpgProjectionVersion &&
                 (!root.TryGetProperty("supportedRpgProjectionVersions",out var versions) || versions.ValueKind!=JsonValueKind.Array ||
                  !versions.EnumerateArray().Any(v=>v.ValueKind==JsonValueKind.Number && v.TryGetInt32(out var version) && version==SnapshotValidation.RpgProjectionVersion))) ||
                root.GetProperty("channelId").GetString()!=channel ||
                root.GetProperty("maxMessageBytes").GetInt32()<SnapshotValidation.MaxMessageBytes)
                throw new SnapshotHeadReadException(false);
            var users=root.GetProperty("allowedUserIds").EnumerateArray().Select(v=>v.GetString()).ToHashSet(StringComparer.Ordinal);
            if(automaticPlayerEnrollment && (!root.TryGetProperty("automaticPlayerEnrollment",out var auto) || auto.ValueKind!=JsonValueKind.True))throw new SnapshotHeadReadException(false);
            if(!automaticPlayerEnrollment && allowedUsers.Any(id=>!users.Contains(id)))throw new SnapshotHeadReadException(false);
        }
        catch(HttpRequestException){throw new SnapshotHeadReadException(true);}
        catch(IOException){throw new SnapshotHeadReadException(true);}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new SnapshotHeadReadException(true);}
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException){throw new SnapshotHeadReadException(false);}
    }

    private static bool Numeric(string? value) => value is { Length: > 0 and <= 25 } && value.All(c => c is >= '0' and <= '9');
    public void Dispose() => client.Dispose();
}
