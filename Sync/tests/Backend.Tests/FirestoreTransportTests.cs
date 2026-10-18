using System.Collections.Concurrent;
using System.Net;
using Backend;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests;

// Real Firestore SDK + protobuf/gRPC over local HTTP/2; synthetic read-only server, no cloud credentials.
public sealed class FirestoreTransportTests
{
    private const string Account = """
        {"UserId":"1001","DisplayName":"Synthetic Otter","Points":9007199254740993,
        "UpdatedAt":"2026-10-17T00:00:00Z","Rpg":{"SchemaVersion":1,
        "Inventory":[{"InstanceId":"synthetic-item","Rarity":2,"Attributes":{"Luck":0.25}}],
        "CompletedActions":["never-publish-this"]}}
        """;

    [Fact]
    public async Task Adapter_reads_existing_and_missing_documents_over_real_grpc()
    {
        await using var fixture = await LocalFirestore.StartAsync();
        fixture.Server.AccountJson = Account;
        var result = await fixture.Reader.GetAsync("1001", default);
        Assert.NotNull(result);
        Assert.Equal(9007199254740993L, result.Points);
        Assert.Equal("plugin-snapshot", result.Source);
        Assert.Equal("synthetic-item", result.Rpg.Inventory[0].GetProperty("InstanceId").GetString());
        Assert.DoesNotContain("never-publish-this", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Null(await fixture.Reader.GetAsync("9999", default));
        await fixture.Reader.CheckAsync(default); // Missing readiness document is valid.
        Assert.All(fixture.Server.Requests, path => Assert.StartsWith(
            "projects/demo-funjii-local/databases/(default)/documents/rpgReadSnapshots/", path));
        Assert.Equal(3, fixture.Server.Requests.Count);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("identity")]
    [InlineData("schema")]
    [InlineData("missing-field")]
    public async Task Adapter_rejects_incompatible_firestore_documents(string scenario)
    {
        await using var fixture = await LocalFirestore.StartAsync();
        fixture.Server.AccountJson = scenario switch
        {
            "malformed" => "{invalid",
            "identity" => Account.Replace("1001", "1002"),
            "schema" => Account.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2"),
            _ => null
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Reader.GetAsync("1001", default));
    }

    [Fact]
    public async Task Invalid_ids_never_reach_transport()
    {
        await using var fixture = await LocalFirestore.StartAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.GetAsync("../private", default));
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        await using var fixture = await LocalFirestore.StartAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.GetAsync("1001", cancellation.Token));
    }

    [Fact]
    public async Task Firestore_permission_failure_is_not_an_empty_account()
    {
        await using var fixture = await LocalFirestore.StartAsync();
        fixture.Server.DenyAccess = true;
        var read = await Assert.ThrowsAsync<RpcException>(() => fixture.Reader.GetAsync("1001", default));
        Assert.Equal(StatusCode.PermissionDenied, read.StatusCode);
        var readiness = await Assert.ThrowsAsync<RpcException>(() => fixture.Reader.CheckAsync(default));
        Assert.Equal(StatusCode.PermissionDenied, readiness.StatusCode);
    }

    private sealed class LocalFirestore(WebApplication app, GrpcChannel channel,
        SyntheticFirestore server, FirestorePlayerReader reader) : IAsyncDisposable
    {
        public SyntheticFirestore Server => server;
        public FirestorePlayerReader Reader => reader;
        public static async Task<LocalFirestore> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0,
                endpoint => endpoint.Protocols = HttpProtocols.Http2));
            builder.Services.AddGrpc();
            var server = new SyntheticFirestore();
            builder.Services.AddSingleton(server);
            var app = builder.Build();
            app.MapGrpcService<SyntheticFirestore>();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            var channel = GrpcChannel.ForAddress(address);
            var client = new FirestoreClientBuilder { CallInvoker = channel.CreateCallInvoker() }.Build();
            var db = FirestoreDb.Create("demo-funjii-local", client);
            return new(app, channel, server, new FirestorePlayerReader(db, "rpgReadSnapshots"));
        }
        public async ValueTask DisposeAsync()
        {
            channel.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    public sealed class SyntheticFirestore : Firestore.FirestoreBase
    {
        public string? AccountJson { get; set; }
        public bool DenyAccess { get; set; }
        public ConcurrentQueue<string> Requests { get; } = new();
        public override async Task BatchGetDocuments(BatchGetDocumentsRequest request,
            IServerStreamWriter<BatchGetDocumentsResponse> responseStream, ServerCallContext context)
        {
            if (DenyAccess) throw new RpcException(new Status(StatusCode.PermissionDenied, "Synthetic denial"));
            Assert.Equal("projects/demo-funjii-local/databases/(default)", request.Database);
            foreach (var name in request.Documents)
            {
                Requests.Enqueue(name);
                var time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);
                var response = new BatchGetDocumentsResponse { ReadTime = time };
                if (name.EndsWith("/1001", StringComparison.Ordinal))
                {
                    response.Found = new Document { Name = name, CreateTime = time, UpdateTime = time };
                    if (AccountJson is not null)
                        response.Found.Fields.Add("accountJson", new Google.Cloud.Firestore.V1.Value { StringValue = AccountJson });
                }
                else response.Missing = name;
                await responseStream.WriteAsync(response);
            }
        }
    }
}
