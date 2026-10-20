using Backend;
using SnapshotProtocol;
namespace Backend.Tests;

public sealed class FirestoreRelayContentionTests
{
    [Fact]public async Task Self_deletion_queue_survives_store_restart_without_needing_a_live_plugin(){
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var settings=new BackendSettings{Twitch=new(){ChannelId="42"},Actions=new(){AllowedUserIds=["1001"],SelfDeletionEnabled=true,PrivacyDeletionEnabled=true}};
        ActionRelay Relay()=>new(new FirestoreRelayStore(fixture.Database,"42","syntheticDeleteQueue"),new RelayClock(),settings);
        var intent=new SnapshotProtocol.SelfDeletionIntent(Guid.NewGuid(),"DELETE_MY_RPG_ACCOUNT");
        var job=await Relay().RequestSelfDeletionAsync("1001",intent,default);
        Assert.Equal(job,await Relay().SelfDeletionStatusAsync("1001",default));
        Assert.Equal(job,Assert.Single(await Relay().PendingSelfDeletionsAsync(default)));
        Assert.False((await Relay().AvailabilityAsync(default)).Available);
    }
    [Fact]
    public async Task Parallel_status_idle_polls_and_receipts_do_not_rewrite_unchanged_documents()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var store=new FirestoreRelayStore(fixture.Database,"42","syntheticActions");
        var clock=new RelayClock();
        var settings=new BackendSettings { Twitch=new(){ChannelId="42"},Actions=new(){AllowedUserIds=["1001"]} };
        var relay=new ActionRelay(store,clock,settings);
        Assert.False((await relay.AvailabilityAsync(default)).Available);
        Assert.Equal(0,fixture.Server.BeginCount);
        Assert.Equal(0,fixture.Server.WriteCount);
        var session=await relay.OpenAsync(default);
        await relay.HeartbeatAsync(new(session.SessionId,1,clock.Now,true,"{}"),default);
        var initial=fixture.Server.WriteCount;
        await Task.WhenAll(Enumerable.Range(0,40).Select(async _=>{
            Assert.True((await relay.AvailabilityAsync(default)).Available);
            Assert.Null(await relay.ClaimAsync(session.SessionId,default));
        }));
        Assert.Equal(initial,fixture.Server.WriteCount);
        var intent=ActionApiFixture.Intent();
        await relay.EnqueueAsync("1001",intent,session.SessionId,default);
        var queuedWrites=fixture.Server.WriteCount;
        await Task.WhenAll(Enumerable.Range(0,20).Select(async _=>Assert.Equal("queued",(await relay.ReadAsync("1001",intent.ActionId,default))!.State)));
        Assert.Equal(queuedWrites,fixture.Server.WriteCount);
        var envelope=await relay.ClaimAsync(session.SessionId,default);
        Assert.NotNull(envelope);
        var done=new RelayComplete(session.SessionId,intent.ActionId,new(true,"applied","synthetic"));
        await relay.CompleteAsync(done,default);
        var completedWrites=fixture.Server.WriteCount;
        await relay.CompleteAsync(done,default);
        Assert.Equal("applied",(await relay.ReadAsync("1001",intent.ActionId,default))!.State);
        Assert.Equal(completedWrites,fixture.Server.WriteCount);
        Assert.Equal("applied",(await relay.EnqueueAsync("1001",intent,session.SessionId,default)).State);
        Assert.Equal(completedWrites,fixture.Server.WriteCount);
    }
    [Fact]
    public async Task Expired_queued_action_is_still_persistently_rejected()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var store=new FirestoreRelayStore(fixture.Database,"42","syntheticActions");
        var clock=new RelayClock();
        var relay=new ActionRelay(store,clock,new(){Twitch=new(){ChannelId="42"},Actions=new(){AllowedUserIds=["1001"]}});
        var session=await relay.OpenAsync(default);
        await relay.HeartbeatAsync(new(session.SessionId,1,clock.Now,true,"{}"),default);
        var intent=ActionApiFixture.Intent();await relay.EnqueueAsync("1001",intent,session.SessionId,default);
        var before=fixture.Server.WriteCount;clock.Now=clock.Now.AddSeconds(20);
        Assert.Equal("rejected",(await relay.ReadAsync("1001",intent.ActionId,default))!.State);
        Assert.Equal(before+2,fixture.Server.WriteCount);
        Assert.False((await relay.AvailabilityAsync(default)).Available);
    }
}
