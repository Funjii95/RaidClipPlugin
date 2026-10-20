using Backend;
using SnapshotProtocol;
namespace Backend.Tests;

public sealed class AutomaticEnrollmentTests
{
    private static RpgIntent Intent()=>new(Guid.NewGuid(),"status",System.Text.Json.JsonSerializer.SerializeToElement(new{}));
    [Fact]
    public async Task Only_published_profiles_can_enqueue_and_zero_balance_remains_admitted()
    {
        var clock=new RelayClock();var settings=new BackendSettings{Twitch=new(){ChannelId="42"},Actions=new(){AutomaticPlayerEnrollment=true}};
        var relay=new ActionRelay(new MemoryRelayStore(),clock,settings,new PublishedReader());
        var session=await relay.OpenAsync(default);
        await relay.HeartbeatAsync(new(session.SessionId,1,clock.Now,true,"{}"),default);
        var accepted=await relay.EnqueueAsync("1001",Intent(),session.SessionId,default);
        Assert.Equal("queued",accepted.State);
        var denied=await Assert.ThrowsAsync<RelayDenied>(()=>relay.EnqueueAsync("1002",Intent(),session.SessionId,default));
        Assert.Equal(403,denied.Status);Assert.Equal("profile_not_enrolled",denied.Code);
        await Assert.ThrowsAsync<RelayDenied>(()=>relay.EnqueueAsync("../1001",Intent(),session.SessionId,default));
    }
    [Fact]
    public async Task Automatic_mode_still_requires_fresh_plugin_session()
    {
        var clock=new RelayClock();var settings=new BackendSettings{Twitch=new(){ChannelId="42"},Actions=new(){AutomaticPlayerEnrollment=true}};
        var relay=new ActionRelay(new MemoryRelayStore(),clock,settings,new PublishedReader());
        var session=await relay.OpenAsync(default);
        await relay.HeartbeatAsync(new(session.SessionId,1,clock.Now,true,"{}"),default);
        clock.Now=clock.Now.AddSeconds(16);
        var result=await relay.EnqueueAsync("1001",Intent(),session.SessionId,default);
        Assert.Equal("rejected",result.State);
    }
    private sealed class PublishedReader:IPlayerReader
    {
        public Task CheckAsync(CancellationToken ct)=>Task.CompletedTask;
        public Task<PlayerView?> GetAsync(string user,CancellationToken ct)=>Task.FromResult<PlayerView?>(user=="1001"?new("1001","SYNTHETIC",0,new(),DateTimeOffset.UtcNow,"firestore"):null);
    }
}

public sealed class AutomaticProfilePublisherTests
{
    [Fact]
    public async Task Initial_profile_is_transactional_idempotent_and_cannot_jump_or_restore()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var sink=new SnapshotFirestore.FirestoreSnapshotSink(fixture.Database,"42",sequentialUpdatesOnly:true,allowSequentialDeletes:true,allowInitialProfiles:true);
        Assert.Equal(SnapshotDecision.Conflict,await sink.ApplyAsync(PublisherTests.Message(2)));
        var first=PublisherTests.Message();
        Assert.Equal(SnapshotDecision.Applied,await sink.ApplyAsync(first));
        Assert.Equal(SnapshotDecision.Duplicate,await sink.ApplyAsync(first));
        Assert.Equal(SnapshotDecision.Conflict,await sink.ApplyAsync(PublisherTests.Message(3)));
        Assert.Equal(SnapshotDecision.Applied,await sink.ApplyAsync(PublisherTests.Message(2) with{Kind=SnapshotKind.Delete,AccountJson=null}));
        Assert.Equal(SnapshotDecision.RestoreRequired,await sink.ApplyAsync(PublisherTests.Message(3)));
        Assert.Equal(2,fixture.Server.WriteCount);
    }

    [Fact]
    public async Task Published_profile_from_another_channel_is_rejected_by_reader()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        await fixture.Sink.ApplyAsync(PublisherTests.Message());
        var reader=new FirestorePlayerReader(fixture.Database,"rpgReadSnapshots","999");
        await Assert.ThrowsAsync<InvalidDataException>(()=>reader.GetAsync("1001",default));
    }
}
