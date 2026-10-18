using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SnapshotDelivery;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class FullProjectionTests
{
    [Fact]
    public async Task Full_projection_capability_is_authenticated_and_binds_channel_and_scope()
    {
        using var factory=new PublisherFactory("unused-capability-test",Convert.ToBase64String(new byte[32]));
        using var app=factory.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,config)=>
            config.AddInMemoryCollection(new Dictionary<string,string?> { ["Publisher:AllowedUserIds:0"]="1001" })));
        using var http=app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync("/api/v1/snapshots/capabilities")).StatusCode);
        using var reader=new HttpSnapshotHeadReader(new Uri("http://localhost/api/v1/snapshots"),Convert.ToBase64String(new byte[32]),"42",app.Server.CreateHandler(),true);
        await reader.RequireFullProjectionAsync(["1001"]);
        Assert.False((await Assert.ThrowsAsync<SnapshotHeadReadException>(()=>reader.RequireFullProjectionAsync(["9999"]))).Retryable);
    }
    [Fact]
    public async Task Old_server_cannot_silently_strip_new_rpg_fields()
    {
        using var reader=new HttpSnapshotHeadReader(new Uri("http://localhost/api/v1/snapshots"),Convert.ToBase64String(new byte[32]),"42",new OldServer(),true);
        Assert.False((await Assert.ThrowsAsync<SnapshotHeadReadException>(()=>reader.RequireFullProjectionAsync(["1001"]))).Retryable);
    }
    [Fact]
    public async Task Actual_firestore_sdk_preserves_new_item_buff_and_decimal_health_fields()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var account=PublisherTests.Account.Replace("\"Inventory\":[]", """
            "Health":{"SushiRegenRemainder":0.25},"Inventory":[{"InstanceId":"item","ItemId":"synthetic","IsLocked":true,"IsFavorite":true,"SetId":"set","Effects":[],"Collector":{"FishingPearls":1.5},"Generation":null}],"Buffs":[{"RecipeId":"fighter","EncounterBased":true,"Category":0,"ActivatedAt":"2026-10-18T00:00:00Z","ExpiresAt":null,"LastBossId":"boss","BossLifeleech":2.25}]
            """);
        await fixture.Sink.ApplyAsync(PublisherTests.Message() with {AccountJson=account});
        var player=(await fixture.Reader.GetAsync("1001",default))!;
        Assert.Equal(0.25m,player.Rpg.Health!.SushiRegenRemainder);
        Assert.True(player.Rpg.Inventory[0].GetProperty("IsFavorite").GetBoolean());
        Assert.Equal("set",player.Rpg.Inventory[0].GetProperty("SetId").GetString());
        Assert.Equal(2.25m,player.Rpg.Buffs[0].GetProperty("BossLifeleech").GetDecimal());
    }
    private sealed class OldServer : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
