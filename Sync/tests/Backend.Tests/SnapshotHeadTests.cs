using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotDelivery;
using SnapshotFirestore;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class SnapshotHeadTests
{
    [Fact]
    public async Task Metadata_uses_publisher_auth_and_scope_without_exposing_profile_or_enabling_writes()
    {
        await using var fixture = await TransactionalFirestoreFixture.StartAsync();
        var sink = new FirestoreSnapshotSink(fixture.Database, "42812664");
        var message = PublisherTests.Message() with { ChannelId = "42812664" };
        await sink.ApplyAsync(message);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var factory = new PublisherFactory("unused-head-test", key);
        using var app = factory.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Publisher:Mode"] = "FirestoreProfilesTest", ["Publisher:ChannelId"] = "42812664",
                ["Publisher:ProjectId"] = "funjii-otter-rpg", ["Publisher:Collection"] = "rpgReadSnapshots",
                ["Publisher:AllowedUserIds:0"] = "1001", ["Publisher:AllowedUserIds:1"] = "1002",
                ["Publisher:AllowedUserIds:2"] = "1003", ["Publisher:AllowedUserIds:3"] = "1004",
                ["Publisher:AllowedUserIds:4"] = "1005", ["Publisher:AllowedUserIds:5"] = "1006"
            }))
            .ConfigureServices(services => { services.RemoveAll<ISnapshotSink>(); services.AddSingleton<ISnapshotSink>(sink); }));
        using var http = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/snapshots/1001/head")).StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var json = await http.GetStringAsync("/api/v1/snapshots/1001/head");
        Assert.DoesNotContain("accountJson", json);
        Assert.DoesNotContain("Points", json);
        Assert.DoesNotContain("private", json);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/v1/snapshots/9999/head")).StatusCode);
        using var reader = new HttpSnapshotHeadReader(new Uri("http://localhost/api/v1/snapshots"), key, "42812664", app.Server.CreateHandler(), true);
        var head = await reader.ReadAsync("1001");
        Assert.Equal(message.SnapshotId, head!.SnapshotId);
        Assert.Equal(SnapshotValidation.Validate(message, "42812664").ContentHash, head.ContentHash);
        Assert.Null(head.AccountJson);
        Assert.Null(await reader.ReadAsync("1002"));
        using var sender = new HttpSnapshotSender(new Uri("http://localhost/api/v1/snapshots"), key, app.Server.CreateHandler(), true);
        Assert.Equal(DeliveryStatus.RetryLater, (await sender.SendAsync(message)).Status);
        Assert.Equal(1, fixture.Server.WriteCount);
    }

    [Theory]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(302, false)]
    [InlineData(429, true)]
    [InlineData(503, true)]
    public async Task Client_classifies_failures_without_reading_error_details(int status, bool retryable)
    {
        using var reader = new HttpSnapshotHeadReader(new Uri("http://localhost/api/v1/snapshots"),
            Convert.ToBase64String(new byte[32]), "42", new Reply((HttpStatusCode)status, "private-error-details"), true);
        var error = await Assert.ThrowsAsync<SnapshotHeadReadException>(() => reader.ReadAsync("1001"));
        Assert.Equal(retryable, error.Retryable);
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"contractVersion\":1,\"contractVersion\":1}")]
    public async Task Invalid_metadata_is_blocked(string body)
    {
        using var reader = new HttpSnapshotHeadReader(new Uri("http://localhost/api/v1/snapshots"),
            Convert.ToBase64String(new byte[32]), "42", new Reply(HttpStatusCode.OK, body), true);
        Assert.False((await Assert.ThrowsAsync<SnapshotHeadReadException>(() => reader.ReadAsync("1001"))).Retryable);
    }

    private sealed class Reply(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
    }
}
