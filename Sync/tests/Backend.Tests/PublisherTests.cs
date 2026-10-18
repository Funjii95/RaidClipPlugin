using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SnapshotDelivery;
using SnapshotProtocol;
using SnapshotPublisher;

namespace Backend.Tests;

public sealed class PublisherFactory(string directory, string key) : WebApplicationFactory<PublisherProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing")
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Publisher:Mode"] = "LocalPrototype", ["Publisher:ChannelId"] = "42",
            ["Publisher:ApiKeyHash"] = Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(key))),
            ["Publisher:StoreDirectory"] = directory
        }));
}

public sealed class PublisherTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "funjii-publisher-tests", Guid.NewGuid().ToString("N"));
    private readonly string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly PublisherFactory factory;
    internal const string Account = """
        {"UserId":"1001","DisplayName":"SYNTHETIC","Points":9007199254740993,"UpdatedAt":"2026-10-18T00:00:00Z",
        "PrivateNote":"local-private","Rpg":{"SchemaVersion":1,"CompletedActions":["private-action"],"Inventory":[]}}
        """;
    internal static SnapshotMessage Message(long revision = 1) => new(1, "42", "1001", revision,
        Guid.NewGuid(), DateTimeOffset.Parse("2026-10-18T00:00:00Z"), SnapshotKind.Upsert, Account);
    public PublisherTests() => factory = new PublisherFactory(Path.Combine(directory, "receiver"), key);
    private async Task<HttpResponseMessage> Post(SnapshotMessage message, string? credential = null)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential ?? key);
        return await client.PostAsJsonAsync("/api/v1/snapshots", message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-key")]
    [InlineData("Bearer not-a-key")]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiJ9.fake.jwt")]
    [InlineData("Bearer AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public async Task Bad_credentials_are_rejected_before_body_parsing(string? authorization)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/snapshots")
            { Content = new StringContent("invalid json containing private data", Encoding.UTF8, "application/json") };
        if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.False(Directory.Exists(Path.Combine(directory, "receiver")));
    }

    [Fact]
    public async Task Channel_is_bound_to_credential_and_never_selected_by_the_body()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Message() with { ChannelId = "43" })).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(directory, "receiver")));
    }

    [Fact]
    public async Task Valid_request_is_projected_and_repeated_request_is_a_duplicate()
    {
        var message = Message();
        var response = await Post(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var ack = (await response.Content.ReadFromJsonAsync<SnapshotAcknowledgement>())!;
        Assert.Equal(message.SnapshotId, ack.SnapshotId);
        Assert.Equal("Applied", ack.Decision);
        var file = await File.ReadAllTextAsync(Path.Combine(directory, "receiver", "42-1001.json"));
        Assert.DoesNotContain("local-private", file);
        Assert.DoesNotContain("private-action", file);
        var repeated = await Post(message);
        Assert.Equal("Duplicate", (await repeated.Content.ReadFromJsonAsync<SnapshotAcknowledgement>())!.Decision);
        Assert.Equal(file, await File.ReadAllTextAsync(Path.Combine(directory, "receiver", "42-1001.json")));
    }

    [Fact]
    public async Task Conflicting_and_late_revisions_have_distinct_acknowledgements()
    {
        var message = Message(2);
        await Post(message);
        var conflict = await Post(message with { AccountJson = Account.Replace("9007199254740993", "7") });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("Conflict", (await conflict.Content.ReadFromJsonAsync<SnapshotAcknowledgement>())!.Decision);
        var stale = await Post(Message());
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Equal("Stale", (await stale.Content.ReadFromJsonAsync<SnapshotAcknowledgement>())!.Decision);
    }

    [Fact]
    public async Task Tombstone_cannot_be_overwritten_without_explicit_restore()
    {
        await Post(Message());
        await Post(Message(2) with { Kind = SnapshotKind.Delete, AccountJson = null });
        var rejected = await Post(Message(3));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("RestoreRequired", (await rejected.Content.ReadFromJsonAsync<SnapshotAcknowledgement>())!.Decision);
        Assert.Equal(HttpStatusCode.OK, (await Post(Message(3) with
            { Kind = SnapshotKind.Restore, ExpectedDeletedRevision = 2 })).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Size_limit_also_applies_without_content_length(bool unknownLength)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using HttpContent content = unknownLength
            ? new UnknownLengthContent(new byte[SnapshotValidation.MaxMessageBytes + 1])
            : new ByteArrayContent(new byte[SnapshotValidation.MaxMessageBytes + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/v1/snapshots", content)).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(directory, "receiver")));
    }

    [Theory]
    [InlineData("{", "application/json", HttpStatusCode.BadRequest)]
    [InlineData("{}", "text/plain", HttpStatusCode.UnsupportedMediaType)]
    public async Task Bad_body_has_no_sensitive_error_response(string body, string type, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await client.PostAsync("/api/v1/snapshots", new StringContent(body, Encoding.UTF8, type));
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("Exception", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Duplicate_identity_fields_are_rejected()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var json = JsonSerializer.Serialize(Message()).Replace("\"ChannelId\":\"42\"", "\"ChannelId\":\"42\",\"channelId\":\"43\"");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/snapshots",
            new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Authenticated_requests_are_rate_limited()
    {
        var message = Message();
        for (var i = 0; i < 60; i++) Assert.Equal(HttpStatusCode.OK, (await Post(message)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(message)).StatusCode);
    }

    [Fact]
    public async Task Sender_dispatches_only_projected_pending_message_and_records_ack()
    {
        var outbox = new LocalOutboxStore(Path.Combine(directory, "sender"), "42");
        var original = Message();
        var state = await outbox.CommitAsync("1001", 0, original.SnapshotId, original.CapturedAt,
            original.Kind, original.AccountJson);
        using var sender = new HttpSnapshotSender(new Uri("http://localhost/api/v1/snapshots"), key,
            factory.Server.CreateHandler(), true);
        var dispatcher = new OutboxDispatcher(outbox, sender);
        var sent = await dispatcher.SendNextAsync("1001");
        Assert.Equal(new DeliveryResult(DeliveryStatus.Delivered, SnapshotDecision.Applied), sent);
        Assert.Empty((await outbox.ReadAsync("1001"))!.Pending);
        Assert.Equal(state.PrivateAccountJson, (await outbox.ReadAsync("1001"))!.PrivateAccountJson);
        Assert.Equal(DeliveryStatus.Empty, (await dispatcher.SendNextAsync("1001")).Status);
    }

    [Fact]
    public async Task Publisher_commit_before_sender_crash_is_repeated_safely()
    {
        var outbox = new LocalOutboxStore(Path.Combine(directory, "sender"), "42");
        var original = Message();
        var state = await outbox.CommitAsync("1001", 0, original.SnapshotId, original.CapturedAt,
            original.Kind, original.AccountJson);
        await Post(state.Pending[0]); // Receiver committed; sender did not persist its ACK.
        using var sender = new HttpSnapshotSender(new Uri("http://localhost/api/v1/snapshots"), key,
            factory.Server.CreateHandler(), true);
        Assert.Equal(new DeliveryResult(DeliveryStatus.Delivered, SnapshotDecision.Duplicate),
            await new OutboxDispatcher(outbox, sender).SendNextAsync("1001"));
        Assert.Empty((await outbox.ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Corrupt_receiver_store_is_retryable_and_not_reported_as_bad_input()
    {
        await Post(Message());
        await File.WriteAllTextAsync(Path.Combine(directory, "receiver", "42-1001.json"), "broken json");
        var response = await Post(Message(2));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("broken", await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        factory.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    internal sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
