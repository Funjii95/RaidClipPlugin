using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Backend;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SnapshotProtocol;

namespace Backend.Tests;
public sealed class RelayClock : TimeProvider
{
    public DateTimeOffset Now=DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow()=>Now;
}
public sealed class FakeLive(RelayClock clock) : ITwitchLiveProbe
{
    public bool Online=false;
    public int Calls;
    public string Stream="synthetic-stream";
    public Task<LiveConfirmation?> CheckAsync(CancellationToken ct){Calls++;return Task.FromResult(Online?new LiveConfirmation(Stream,clock.Now):null);}
}
public sealed class ActionApiFixture : IAsyncDisposable
{
    public static readonly byte[] KeyBytes=System.Text.Encoding.UTF8.GetBytes("synthetic-action-key-at-least-32-bytes");
    public static string Key=>Convert.ToBase64String(KeyBytes);
    public readonly RelayClock Clock=new();
    public readonly FakeLive Live;
    private readonly ApiFactory original=new();
    public readonly WebApplicationFactory<Program> Factory;
    public readonly HttpClient Viewer;
    public readonly HttpClient Plugin;
    public ActionApiFixture(IRelayStore? store=null,IPlayerReader? players=null,bool privacy=false,bool selfDeletion=false)
    {
        Live=new(Clock);
        Factory=original.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,cfg)=>cfg.AddInMemoryCollection(new Dictionary<string,string?>{
            ["Actions:Enabled"]="true",["Actions:Mode"]="Synthetic",["Actions:AllowedUserIds:0"]="1001",["Actions:AllowedUserIds:1"]="1002",
            ["Actions:PluginKeyHash"]=Convert.ToHexString(SHA256.HashData(KeyBytes)),["Actions:PrivacyDeletionEnabled"]=privacy.ToString(),["Actions:SelfDeletionEnabled"]=selfDeletion.ToString()
        })).ConfigureServices(services=>{
            services.RemoveAll<TimeProvider>();services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<ITwitchLiveProbe>();services.AddSingleton<ITwitchLiveProbe>(Live);
            services.RemoveAll<IRelayStore>();services.AddSingleton<IRelayStore>(store??new MemoryRelayStore());
            if(players is not null){services.RemoveAll<IPlayerReader>();services.AddSingleton(players);}
        }));
        Viewer=Factory.CreateClient();Viewer.DefaultRequestHeaders.Authorization=new("Bearer",Token());
        Plugin=Factory.CreateClient();Plugin.DefaultRequestHeaders.Authorization=new("Bearer",Key);
    }
    public static string Token(string user="1001",string channel="42",string? secret=null,bool expired=false)=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims:[new Claim("channel_id",channel),new Claim("user_id",user),new Claim("role","viewer"),new Claim("opaque_user_id","Usynthetic"),new Claim("is_unlinked","false")],
        expires:DateTime.UtcNow.AddMinutes(expired?-5:5),signingCredentials:new(new SymmetricSecurityKey(Convert.FromBase64String(secret??ApiFactory.Secret)),SecurityAlgorithms.HmacSha256)));
    public static RpgIntent Intent(string kind="buyPerk",string arguments="{\"perkId\":\"damage\"}",Guid? id=null)=>new(id??Guid.NewGuid(),kind,JsonDocument.Parse(arguments).RootElement.Clone());
    public async Task<Guid> ReadyAsync(){var opened=await Plugin.PostAsync("/api/v1/plugin/actions/session",null);opened.EnsureSuccessStatusCode();var session=(await opened.Content.ReadFromJsonAsync<RelaySession>())!.SessionId;await BeatAsync(session,1);Viewer.DefaultRequestHeaders.Remove("X-Rpg-Session");Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",session.ToString());return session;}
    public async Task<HttpResponseMessage> BeatAsync(Guid id,long seq,bool ready=true)=>await Plugin.PostAsJsonAsync("/api/v1/plugin/actions/heartbeat",new RelayHeartbeat(id,seq,Clock.Now,ready,"{}"));
    public async ValueTask DisposeAsync(){Viewer.Dispose();Plugin.Dispose();await Factory.DisposeAsync();await original.DisposeAsync();}
}
public sealed class ActionRelayTests
{
    [Fact] public async Task Valid_action_id_is_durable_and_claimed_once_even_after_concurrent_requests()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();var intent=ActionApiFixture.Intent();
        var replies=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)));
        Assert.All(replies,r=>Assert.Equal(HttpStatusCode.Accepted,r.StatusCode));
        var claim=await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null);var e=(await claim.Content.ReadFromJsonAsync<ActionEnvelope>())!;
        Assert.Equal("1001",e.UserId);Assert.Equal(intent.ActionId,e.Intent.ActionId);
        var result=new RelayComplete(session,intent.ActionId,new(true,"applied",""));
        (await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",result)).EnsureSuccessStatusCode();
        (await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",result)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent,(await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).StatusCode);
        Assert.Equal("applied",(await f.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/"+intent.ActionId))!.State);
    }
    [Theory][InlineData("signature")][InlineData("channel")][InlineData("expired")][InlineData("identity")]
    public async Task Invalid_JWT_is_rejected(string reason)
    {
        await using var f=new ActionApiFixture();await f.ReadyAsync();
        f.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token(reason=="identity"?"":"1001",reason=="channel"?"99":"42",reason=="signature"?Convert.ToBase64String(new byte[32]):null,reason=="expired"));
        var response=await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent());Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }
    [Fact]public async Task Browser_identity_override_and_duplicate_fields_are_rejected()
    {
        await using var f=new ActionApiFixture();await f.ReadyAsync();
        foreach(var extra in new[]{"\"userId\":\"1002\",","\"kind\":\"fish\","}){
            using var content=new StringContent("{"+extra+"\"actionId\":\""+Guid.NewGuid()+"\",\"kind\":\"buyPerk\",\"arguments\":{\"perkId\":\"damage\"}}",System.Text.Encoding.UTF8,"application/json");
            Assert.Equal(HttpStatusCode.BadRequest,(await f.Viewer.PostAsync("/api/v1/play/actions",content)).StatusCode);
        }
    }
    [Fact]public async Task Twitch_offline_allows_play_but_disconnect_and_expired_plugin_heartbeat_block()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();f.Live.Online=false;
        Assert.Equal(HttpStatusCode.Accepted,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
        Assert.Equal(0,f.Live.Calls);
        f.Live.Online=true;f.Clock.Now=f.Clock.Now.AddSeconds(16);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.BeatAsync(session,2)).StatusCode);
        session=await f.ReadyAsync();await f.Plugin.DeleteAsync("/api/v1/plugin/actions/session/"+session);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).StatusCode);
    }
    [Fact]public async Task Rpg_not_ready_heartbeat_immediately_revokes_actions()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();
        (await f.BeatAsync(session,2,false)).EnsureSuccessStatusCode();
        Assert.False((await f.Viewer.GetFromJsonAsync<RelayAvailability>("/api/v1/play/state"))!.Available);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
    }
    [Fact]public async Task Stale_heartbeat_sequence_time_and_old_session_cannot_reopen()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await f.BeatAsync(session,1)).StatusCode);
        var stale=new RelayHeartbeat(session,2,f.Clock.Now.AddSeconds(-10),true,"{}");
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/heartbeat",stale)).StatusCode);
        await f.Plugin.DeleteAsync("/api/v1/plugin/actions/session/"+session);await f.ReadyAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await f.BeatAsync(session,999)).StatusCode);
    }
    [Fact]public async Task Other_user_cannot_read_reuse_or_claim_actions()
    {
        await using var f=new ActionApiFixture();await f.ReadyAsync();var action=ActionApiFixture.Intent();await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",action);
        f.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token("1002"));
        Assert.Equal(HttpStatusCode.NotFound,(await f.Viewer.GetAsync("/api/v1/play/actions/"+action.ActionId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",action)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Viewer.PostAsync("/api/v1/plugin/actions/session",null)).StatusCode);
    }
    [Fact]public async Task Claimed_timeout_is_unknown_and_stream_change_never_invalidates_plugin_session()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();var action=ActionApiFixture.Intent();await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",action);
        await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null);f.Clock.Now=f.Clock.Now.AddSeconds(9);
        Assert.Equal("unknown",(await f.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/"+action.ActionId))!.State);
        Assert.Equal(HttpStatusCode.NoContent,(await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).StatusCode);
        f.Live.Stream="new-stream";
        Assert.Equal(HttpStatusCode.Accepted,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
    }

    [Fact]public async Task Delayed_browser_request_from_previous_session_is_never_bound_to_new_session()
    {
        await using var f=new ActionApiFixture();var old=await f.ReadyAsync();
        await f.Plugin.DeleteAsync("/api/v1/plugin/actions/session/"+old);await f.ReadyAsync();
        f.Viewer.DefaultRequestHeaders.Remove("X-Rpg-Session");f.Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",old.ToString());
        Assert.Equal(HttpStatusCode.Conflict,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
    }
    [Fact]public async Task Opening_plugin_connection_without_ready_heartbeat_cannot_authorize_play()
    {
        await using var f=new ActionApiFixture();var open=(await (await f.Plugin.PostAsync("/api/v1/plugin/actions/session",null)).Content.ReadFromJsonAsync<RelaySession>())!;
        f.Viewer.DefaultRequestHeaders.Add("X-Rpg-Session",open.SessionId.ToString());
        Assert.False((await f.Viewer.GetFromJsonAsync<RelayAvailability>("/api/v1/play/state"))!.Available);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
    }

    [Fact]public async Task Claim_execution_window_is_capped_by_the_existing_plugin_lease()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();
        (await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).EnsureSuccessStatusCode();
        f.Clock.Now=f.Clock.Now.AddSeconds(14);
        var envelope=(await (await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).Content.ReadFromJsonAsync<ActionEnvelope>())!;
        Assert.Equal(f.Clock.Now,envelope.ObservedAt);Assert.True(envelope.ExecuteBefore-envelope.ObservedAt<=TimeSpan.FromSeconds(1));
    }

    [Fact]public async Task Refused_old_intent_is_terminal_even_after_new_session_and_retry()
    {
        await using var f=new ActionApiFixture();var old=await f.ReadyAsync();var intent=ActionApiFixture.Intent();
        await f.Plugin.DeleteAsync("/api/v1/plugin/actions/session/"+old);
        var refused=await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent);
        Assert.Equal(HttpStatusCode.Conflict,refused.StatusCode);Assert.Equal("rejected",(await refused.Content.ReadFromJsonAsync<ActionReceipt>())!.State);
        var current=await f.ReadyAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+current,null)).StatusCode);
    }
    [Fact]public async Task Firestore_queue_survives_new_store_instance_and_retries_transaction_conflicts()
    {
        await using var db=await TransactionalFirestoreFixture.StartAsync();
        var first=new FirestoreRelayStore(db.Database,"42","rpgActionChannelsTest");
        var id=Guid.NewGuid();
        db.Server.AbortNextCommit();
        await first.TransactionAsync<bool>(id,false,(s,_)=>{s.SessionId=Guid.NewGuid();s.Queue.Add(id);return new(true,new(){Envelope=new("42","1001",s.SessionId,"stream",default,ActionApiFixture.Intent(id:id),"hash")},true);},default);
        var second=new FirestoreRelayStore(db.Database,"42","rpgActionChannelsTest");
        var action=await second.TransactionAsync<RelayAction?>(null,true,(s,a)=>new(a),default);
        Assert.Equal(id,action!.Envelope.Intent.ActionId);Assert.True(db.Server.BeginCount>=3);
    }
}
