using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SnapshotDelivery;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class DeliveryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "funjii-delivery-tests", Guid.NewGuid().ToString("N"));
    private readonly string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly Uri Endpoint = new("https://publisher.example/api/v1/snapshots");
    private static HttpResponseMessage Ack(SnapshotMessage message, string decision = "Applied") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new SnapshotAcknowledgement(1, message.ChannelId, message.UserId,
            message.Revision, message.SnapshotId, decision))
    };

    [Theory]
    [InlineData(401, DeliveryStatus.Blocked)]
    [InlineData(403, DeliveryStatus.Blocked)]
    [InlineData(409, DeliveryStatus.Blocked)]
    [InlineData(302, DeliveryStatus.Blocked)]
    [InlineData(400, DeliveryStatus.Blocked)]
    [InlineData(413, DeliveryStatus.Blocked)]
    [InlineData(415, DeliveryStatus.Blocked)]
    [InlineData(408, DeliveryStatus.RetryLater)]
    [InlineData(429, DeliveryStatus.RetryLater)]
    [InlineData(500, DeliveryStatus.RetryLater)]
    [InlineData(503, DeliveryStatus.RetryLater)]
    public async Task Non_success_keeps_the_pending_message(int status, DeliveryStatus expected)
    {
        var message = PublisherTests.Message();
        var store = new LocalOutboxStore(directory, "42");
        await store.CommitAsync("1001", 0, message.SnapshotId, message.CapturedAt, message.Kind, message.AccountJson);
        var calls = 0;
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage((HttpStatusCode)status);
            if (status == 302) response.Headers.Location = new Uri("https://untrusted.example/");
            return Task.FromResult(response);
        }), false);
        Assert.Equal(expected, (await new OutboxDispatcher(store, sender).SendNextAsync("1001")).Status);
        Assert.Equal(1, calls); // Redirect is never followed and no in-call busy retries.
        Assert.Equal(message.SnapshotId, Assert.Single((await store.ReadAsync("1001"))!.Pending).SnapshotId);
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("user")]
    [InlineData("revision")]
    [InlineData("snapshot")]
    [InlineData("version")]
    [InlineData("decision")]
    [InlineData("numericDecision")]
    public async Task Ack_must_match_the_exact_pending_message(string changed)
    {
        var message = PublisherTests.Message();
        var ack = new SnapshotAcknowledgement(1, message.ChannelId, message.UserId,
            message.Revision, message.SnapshotId, "Applied");
        ack = changed switch
        {
            "channel" => ack with { ChannelId = "43" }, "user" => ack with { UserId = "1002" },
            "revision" => ack with { Revision = 2 }, "snapshot" => ack with { SnapshotId = Guid.NewGuid() },
            "version" => ack with { ContractVersion = 2 }, "decision" => ack with { Decision = "Conflict" },
            _ => ack with { Decision = "0" }
        };
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ack) })), false);
        Assert.Equal(DeliveryStatus.Blocked, (await sender.SendAsync(message)).Status);
    }

    [Fact]
    public async Task Sender_never_sends_private_account_fields_or_uses_get()
    {
        var message = PublisherTests.Message();
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Endpoint, request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(key, request.Headers.Authorization.Parameter);
            var json = await request.Content!.ReadAsStringAsync(ct);
            Assert.DoesNotContain("local-private", json);
            Assert.DoesNotContain("private-action", json);
            Assert.DoesNotContain("PrivateAccountJson", json);
            return Ack(message);
        }), false);
        Assert.Equal(DeliveryStatus.Delivered, (await sender.SendAsync(message)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ack_size_is_bounded_even_without_content_length(bool unknownLength)
    {
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) =>
        {
            HttpContent content = unknownLength ? new PublisherTests.UnknownLengthContent(new byte[4097])
                : new ByteArrayContent(new byte[4097]);
            content.Headers.ContentType = new("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }), false);
        Assert.Equal(DeliveryStatus.Blocked, (await sender.SendAsync(PublisherTests.Message())).Status);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"contractVersion\":1,\"ContractVersion\":2}")]
    public async Task Invalid_ack_json_fails_closed(string json)
    {
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })), false);
        Assert.Equal(DeliveryStatus.Blocked, (await sender.SendAsync(PublisherTests.Message())).Status);
    }

    [Fact]
    public async Task Network_failure_keeps_the_queue_and_cancellation_is_not_acknowledged()
    {
        var message = PublisherTests.Message();
        var store = new LocalOutboxStore(directory, "42");
        await store.CommitAsync("1001", 0, message.SnapshotId, message.CapturedAt, message.Kind, message.AccountJson);
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) => throw new HttpRequestException("synthetic")), false);
        Assert.Equal(DeliveryStatus.RetryLater, (await new OutboxDispatcher(store, sender).SendNextAsync("1001")).Status);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new OutboxDispatcher(store, sender).SendNextAsync("1001", cancelled.Token));
        Assert.Single((await store.ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Parallel_dispatchers_serialize_across_separate_store_instances()
    {
        var message = PublisherTests.Message();
        var store = new LocalOutboxStore(directory, "42");
        await store.CommitAsync("1001", 0, message.SnapshotId, message.CapturedAt, message.Kind, message.AccountJson);
        var calls = 0;
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Ack(message));
        }), false);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            new OutboxDispatcher(new LocalOutboxStore(directory, "42"), sender).SendNextAsync("1001")));
        Assert.Equal(1, calls);
        Assert.Single(results, r => r.Status == DeliveryStatus.Delivered);
        Assert.Equal(3, results.Count(r => r.Status == DeliveryStatus.Empty));
    }

    [Theory]
    [InlineData("http://publisher.example/api/v1/snapshots")]
    [InlineData("http://127.0.0.1/api/v1/snapshots")]
    [InlineData("https://publisher.example/api/v1/snapshots?key=secret")]
    [InlineData("https://user:password@publisher.example/api/v1/snapshots")]
    [InlineData("https://publisher.example/other")]
    public void Production_sender_rejects_unencrypted_or_ambiguous_destinations(string endpoint)
        => Assert.Throws<ArgumentException>(() => new HttpSnapshotSender(new Uri(endpoint), key));

    [Fact]
    public async Task Worker_retries_a_transient_failure_then_delivers_the_same_message()
    {
        var message = PublisherTests.Message();
        var store = new LocalOutboxStore(directory, "42");
        await store.CommitAsync("1001", 0, message.SnapshotId, message.CapturedAt, message.Kind, message.AccountJson);
        var calls = 0;
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler(async (request, ct) =>
        {
            var actual = await request.Content!.ReadFromJsonAsync<SnapshotMessage>(ct);
            Assert.Equal(message.SnapshotId, actual!.SnapshotId);
            return ++calls == 1 ? new(HttpStatusCode.ServiceUnavailable) : Ack(message);
        }), false);
        using var stop = new CancellationTokenSource();
        var waits = new List<TimeSpan>();
        var worker = new OutboxDeliveryWorker(new OutboxDispatcher(store, sender), (duration, ct) =>
        {
            waits.Add(duration);
            if (duration == TimeSpan.FromSeconds(5)) { stop.Cancel(); return Task.FromCanceled(ct); }
            return Task.CompletedTask;
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunAsync("1001", stop.Token));
        Assert.Equal(2, calls);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }, waits);
        Assert.Empty((await store.ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Worker_backs_off_without_removing_failed_messages()
    {
        var message = PublisherTests.Message();
        var store = new LocalOutboxStore(directory, "42");
        await store.CommitAsync("1001", 0, message.SnapshotId, message.CapturedAt, message.Kind, message.AccountJson);
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))), false);
        using var stop = new CancellationTokenSource();
        var waits = new List<TimeSpan>();
        var worker = new OutboxDeliveryWorker(new OutboxDispatcher(store, sender), (duration, ct) =>
        {
            waits.Add(duration);
            if (waits.Count == 3) { stop.Cancel(); return Task.FromCanceled(ct); }
            return Task.CompletedTask;
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunAsync("1001", stop.Token));
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) }, waits);
        Assert.Single((await store.ReadAsync("1001"))!.Pending);
    }

    [Fact]
    public async Task Worker_stops_on_access_or_contract_failure_instead_of_retrying_forever()
    {
        var message = PublisherTests.Message();
        var store = new LocalOutboxStore(directory, "42");
        await store.CommitAsync("1001", 0, message.SnapshotId, message.CapturedAt, message.Kind, message.AccountJson);
        using var sender = new HttpSnapshotSender(Endpoint, key, new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))), false);
        var waits = 0;
        var worker = new OutboxDeliveryWorker(new OutboxDispatcher(store, sender), (_, _) =>
        {
            waits++;
            return Task.CompletedTask;
        });
        Assert.Equal(DeliveryStatus.Blocked, await worker.RunAsync("1001", CancellationToken.None));
        Assert.Equal(0, waits);
        Assert.Single((await store.ReadAsync("1001"))!.Pending);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
