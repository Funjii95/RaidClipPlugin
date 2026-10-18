using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotProtocol;
using SnapshotPublisher;

namespace Backend.Tests;

public sealed class PublisherConfigurationTests
{
    private static PublisherSettings Settings() => new()
    {
        Mode = "FirestoreTest", ChannelId = "42", ApiKeyHash = new string('A', 64),
        ProjectId = "funjii-otter-rpg", DatabaseId = "(default)",
        Collection = "rpgSnapshotPipelineTest", AllowedUserIds = ["1001"]
    };
    [Theory]
    [InlineData("project")]
    [InlineData("database")]
    [InlineData("collection")]
    [InlineData("channel")]
    [InlineData("user")]
    [InlineData("missingUser")]
    [InlineData("mode")]
    public void Initial_cloud_mode_cannot_target_real_profile_scope(string scenario)
    {
        var settings = Settings();
        switch (scenario)
        {
            case "project": settings.ProjectId = "other-project"; break;
            case "database": settings.DatabaseId = "other-database"; break;
            case "collection": settings.Collection = "rpgReadSnapshots"; break;
            case "channel": settings.ChannelId = "42812664"; break;
            case "user": settings.AllowedUserIds = ["9999"]; break;
            case "missingUser": settings.AllowedUserIds = []; break;
            case "mode": settings.Mode = "Production"; break;
        }
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }
    [Fact]
    public void Valid_cloud_settings_start_with_writes_disabled()
    {
        var settings = Settings();
        settings.Validate();
        Assert.False(settings.WritesEnabled);
    }

    [Theory]
    [InlineData(false, false, HttpStatusCode.ServiceUnavailable, 0)]
    [InlineData(true, false, HttpStatusCode.OK, 1)]
    [InlineData(true, true, HttpStatusCode.Forbidden, 0)]
    public async Task Cloud_write_gate_and_test_user_allowlist_are_enforced(bool enabled, bool otherUser,
        HttpStatusCode expected, int writes)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var factory = new PublisherFactory("unused-local-directory", key);
        var sink = new CountingSink();
        using var configured = factory.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Publisher:Mode"] = "FirestoreTest", ["Publisher:ProjectId"] = "funjii-otter-rpg",
                ["Publisher:DatabaseId"] = "(default)", ["Publisher:Collection"] = "rpgSnapshotPipelineTest",
                ["Publisher:AllowedUserIds:0"] = "1001", ["Publisher:WritesEnabled"] = enabled.ToString()
            })).ConfigureServices(services =>
            {
                services.RemoveAll<ISnapshotSink>();
                services.AddSingleton<ISnapshotSink>(sink);
            }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var message = PublisherTests.Message();
        if (otherUser) message = message with { UserId = "1002", AccountJson = message.AccountJson!.Replace("1001", "1002") };
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/v1/snapshots", message)).StatusCode);
        Assert.Equal(writes, sink.Count);
    }
    private sealed class CountingSink : ISnapshotSink
    {
        public int Count { get; private set; }
        public Task<SnapshotDecision> ApplyAsync(SnapshotMessage message, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(SnapshotDecision.Applied);
        }
    }
}
