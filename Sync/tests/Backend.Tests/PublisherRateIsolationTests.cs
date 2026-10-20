using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotFirestore;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class PublisherRateIsolationTests
{
    [Fact]
    public async Task Metadata_polling_does_not_spend_the_snapshot_write_budget()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var sink=new FirestoreSnapshotSink(fixture.Database,"42812664");
        var first=PublisherTests.Message() with {ChannelId="42812664"};
        await sink.ApplyAsync(first);
        using var factory=new PublisherFactory("unused-rate-isolation",ActionApiFixture.Key);
        using var app=factory.WithWebHostBuilder(b=>b
            .ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Publisher:Mode"]="FirestoreProfilesTest",["Publisher:ChannelId"]="42812664",
                ["Publisher:ProjectId"]="funjii-otter-rpg",["Publisher:Collection"]="rpgReadSnapshots",
                ["Publisher:WritesEnabled"]="true",["Publisher:AllowedUserIds:0"]="1001"
            }))
            .ConfigureServices(s=>{s.RemoveAll<ISnapshotSink>();s.AddSingleton<ISnapshotSink>(sink);}));
        using var http=app.CreateClient();
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ActionApiFixture.Key);
        for(var i=0;i<60;i++)
            Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/v1/snapshots/1001/head")).StatusCode);
        var write=await http.PostAsJsonAsync("/api/v1/snapshots",first with {Revision=2,SnapshotId=Guid.NewGuid()});
        Assert.True(write.IsSuccessStatusCode,$"Snapshot write was blocked: {write.StatusCode}");
        Assert.Equal(2,(await sink.ReadCurrentAsync("1001"))!.Revision);
        for(var i=60;i<600;i++)
            Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/v1/snapshots/1001/head")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await http.GetAsync("/api/v1/snapshots/1001/head")).StatusCode);
        var later=await http.PostAsJsonAsync("/api/v1/snapshots",first with {Revision=3,SnapshotId=Guid.NewGuid()});
        Assert.True(later.IsSuccessStatusCode);
        Assert.Equal(3,(await sink.ReadCurrentAsync("1001"))!.Revision);
    }
}

