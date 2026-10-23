using System.Net;
using System.Net.Http.Json;
using Backend;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class AdminActionTests
{
    [InlineData("adminHealAll","{}")]
    [InlineData("adminMaterialGrant","{\"targetUserId\":\"1001\",\"materialId\":\"reinforcement_core\",\"count\":\"1\"}")]
    [InlineData("adminBalance","{\"path\":\"reset:all\",\"value\":\"0\"}")]
    [Theory][InlineData("adminCatalog","{}")][InlineData("adminStart","{\"event\":\"boss\",\"stake\":\"0\"}")][InlineData("adminGrant","{\"itemId\":\"tide_king_helmet\",\"targetUserId\":\"1001\",\"count\":\"1\",\"roll\":\"random\",\"stats\":\"{}\"}")]
    public async Task OrdinaryVerifiedViewerCannotQueueAdminActions(string kind,string arguments)
    {
        await using var f=new ActionApiFixture();await f.ReadyAsync();
        var response=await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent(kind,arguments));
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.Contains("admin_required",await response.Content.ReadAsStringAsync());
    }
    [Fact] public async Task OnlyConfiguredChannelOwnerCanQueueAndRetryAdminRequest()
    {
        var settings=new BackendSettings{Twitch=new(){ChannelId="42"},Actions=new(){AllowedUserIds=["42","1001"]}};
        var relay=new ActionRelay(new MemoryRelayStore(),TimeProvider.System,settings);
        var session=await relay.OpenAsync(default);
        await relay.HeartbeatAsync(new(session.SessionId,1,DateTimeOffset.UtcNow,true,"{}"),default);
        var intent=ActionApiFixture.Intent("adminStart","{\"event\":\"mimic\",\"stake\":\"0\"}");
        var first=await relay.EnqueueAsync("42",intent,session.SessionId,default);
        Assert.Equal(first,await relay.EnqueueAsync("42",intent,session.SessionId,default));
        var denied=await Assert.ThrowsAsync<RelayDenied>(()=>relay.EnqueueAsync("1001",intent,session.SessionId,default));Assert.Equal(403,denied.Status);
        Assert.Equal("42",(await relay.ClaimAsync(session.SessionId,default))!.UserId);
    }
    [Theory][InlineData("arbitrary-command")][InlineData("boss;reset")]
    public void AdminEventsAreAnExplicitAllowlist(string name)=>Assert.Throws<ArgumentException>(()=>RpgIntents.Canonical(ActionApiFixture.Intent("adminStart",System.Text.Json.JsonSerializer.Serialize(new{ @event=name,stake="0"}))));
}
