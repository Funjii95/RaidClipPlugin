using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotDelivery;
using SnapshotFirestore;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class AutomaticPublisherApiTests
{
    [Fact]
    public async Task Enrollment_requires_plugin_credential_advertises_capability_and_accepts_new_numeric_user()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var sink=new FirestoreSnapshotSink(fixture.Database,"42812664",sequentialUpdatesOnly:true,allowInitialProfiles:true);
        var key=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var factory=new PublisherFactory("unused-automatic",key);
        using var app=factory.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{
            ["Publisher:Mode"]="FirestoreProfilesTest",["Publisher:ChannelId"]="42812664",
            ["Publisher:ProjectId"]="funjii-otter-rpg",["Publisher:Collection"]="rpgReadSnapshots",
            ["Publisher:AllowedUserIds:0"]="9999",["Publisher:AutomaticPlayerEnrollment"]="true",
            ["Publisher:WritesEnabled"]="true"
        })).ConfigureServices(s=>{s.RemoveAll<ISnapshotSink>();s.AddSingleton<ISnapshotSink>(sink);}));
        using var http=app.CreateClient();
        var message=PublisherTests.Message() with{ChannelId="42812664"};
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.PostAsJsonAsync("/api/v1/snapshots",message)).StatusCode);
        Assert.Equal(0,fixture.Server.WriteCount);
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",key);
        using var reader=new HttpSnapshotHeadReader(new Uri("http://localhost/api/v1/snapshots"),key,"42812664",app.Server.CreateHandler(),true);
        await reader.RequireFullProjectionAsync([],automaticPlayerEnrollment:true);
        Assert.Null(await reader.ReadAsync("1001"));
        Assert.Equal(HttpStatusCode.OK,(await http.PostAsJsonAsync("/api/v1/snapshots",message)).StatusCode);
        Assert.NotNull(await reader.ReadAsync("1001"));
        Assert.Equal(HttpStatusCode.OK,(await http.PostAsJsonAsync("/api/v1/snapshots",message)).StatusCode);
        Assert.Equal(1,fixture.Server.WriteCount);
        Assert.Equal(HttpStatusCode.Forbidden,(await http.PostAsJsonAsync("/api/v1/snapshots",message with{ChannelId="43"})).StatusCode);
        Assert.Equal(1,fixture.Server.WriteCount);
    }
}
