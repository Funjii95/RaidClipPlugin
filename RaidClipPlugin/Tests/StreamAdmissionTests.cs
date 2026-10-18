using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class StreamAdmissionTests
{
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.Parse("2026-10-18T12:00:00Z");
        internal long Tick=1;
        public override DateTimeOffset GetUtcNow()=>Now;
        public override long GetTimestamp()=>Tick;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        internal void Advance(TimeSpan time){Now+=time;Tick+=time.Ticks;}
    }
    [Fact]
    public void Connected_alone_is_not_live_and_confirmation_expires()
    {
        var clock=new Clock();var gate=new StreamAdmission(clock);var epoch=gate.Begin(()=>true);
        Assert.False(gate.CanPlay(epoch));gate.Observe(gate.StartCheck(epoch),null);Assert.False(gate.CanPlay(epoch));
        gate.Observe(gate.StartCheck(epoch),"stream-1");Assert.True(gate.CanPlay(epoch));
        clock.Advance(TimeSpan.FromSeconds(21));Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Offline_and_old_out_of_order_signals_cannot_reenable_play()
    {
        var clock=new Clock();var gate=new StreamAdmission(clock);var epoch=gate.Begin(()=>true);
        var old=gate.StartCheck(epoch);var current=gate.StartCheck(epoch);
        gate.Observe(current,null);gate.Observe(old,"old-stream");Assert.False(gate.CanPlay(epoch));
        var delayed=gate.StartCheck(epoch);clock.Advance(TimeSpan.FromSeconds(21));gate.Observe(delayed,"stream");Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Stale_sessions_and_changed_broadcasts_require_new_session()
    {
        var gate=new StreamAdmission();var old=gate.Begin(()=>true);var stale=gate.StartCheck(old);
        gate.End();var epoch=gate.Begin(()=>true);gate.Observe(stale,"stream");Assert.False(gate.CanPlay(epoch));
        gate.Observe(gate.StartCheck(epoch),"stream-1");Assert.True(gate.CanPlay(epoch));
        gate.Observe(gate.StartCheck(epoch),"stream-2");Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Not_ready_and_old_messages_are_rejected()
    {
        var clock=new Clock();var ready=false;var gate=new StreamAdmission(clock);var epoch=gate.Begin(()=>ready);
        gate.Observe(gate.StartCheck(epoch),"stream");Assert.False(gate.CanPlay(epoch));ready=true;
        Assert.False(gate.CanPlay(epoch,clock.Now.AddMinutes(-1)));Assert.True(gate.CanPlay(epoch,clock.Now));
        ready=false;Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Old_execution_context_cannot_commit_in_a_new_ready_session()
    {
        var gate=new StreamAdmission();var old=gate.Begin(()=>true);
        gate.Observe(gate.StartCheck(old),"stream");
        using var scope=gate.Enter(old);
        gate.End();var epoch=gate.Begin(()=>true);gate.Observe(gate.StartCheck(epoch),"stream");
        Assert.Throws<GameplayUnavailableException>(()=>gate.Commit(()=>throw new Exception("Must not execute")));
    }
    [Fact]
    public async Task Denied_save_rolls_back_mutable_legacy_accounts_and_does_not_leak_later()
    {
        var directory=Path.Combine(Path.GetTempPath(),"otter-gate",Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(directory);await store.SetPointsAsync("1001","Synthetic",10,0,default);
            var gate=new StreamAdmission();store.Admission=gate;var epoch=gate.Begin(()=>true);
            await Assert.ThrowsAsync<GameplayUnavailableException>(()=>store.AddPointsAsync("1001","Synthetic",100,0,default));
            Assert.Equal(10,await store.GetPointsAsync("1001",default));
            gate.Observe(gate.StartCheck(epoch),"stream");
            await store.AddPointsAsync("1001","Synthetic",1,0,default);
            Assert.Equal(11,await new ViewerPointStore(directory).GetPointsAsync("1001",default));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }

    [Fact]
    public async Task Already_reserved_stake_can_be_reversed_after_disconnect_without_allowing_new_play()
    {
        var directory=Path.Combine(Path.GetTempPath(),"otter-refund",Guid.NewGuid().ToString("N"));
        try {
            var store=new ViewerPointStore(directory);await store.SetPointsAsync("1001","Synthetic",100,0,default);
            var gate=new StreamAdmission();store.Admission=gate;var epoch=gate.Begin(()=>true);
            gate.Observe(gate.StartCheck(epoch),"stream");
            Assert.True((await store.ReserveDuelStakeAsync("1001","Synthetic",10,0,0,default)).Success);
            gate.End();await store.RefundDuelStakeAsync("1001","Synthetic",10,0,default);
            Assert.Equal(100,await store.GetPointsAsync("1001",default));
            await Assert.ThrowsAsync<GameplayUnavailableException>(()=>store.AddPointsAsync("1001","Synthetic",1,0,default));
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task Queued_store_operation_keeps_its_original_session()
    {
        var directory=Path.Combine(Path.GetTempPath(),"otter-queued",Guid.NewGuid().ToString("N"));
        try {
            var store=new ViewerPointStore(directory);await store.SetPointsAsync("1001","Synthetic",10,0,default);
            var gate=new StreamAdmission();store.Admission=gate;var epoch=gate.Begin(()=>true);gate.Observe(gate.StartCheck(epoch),"stream");
            var mutex=(SemaphoreSlim)typeof(ViewerPointStore).GetField("_lock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(store)!;
            await mutex.WaitAsync();var queued=store.AddPointsAsync("1001","Synthetic",99,0,default);
            gate.End();epoch=gate.Begin(()=>true);gate.Observe(gate.StartCheck(epoch),"stream");mutex.Release();
            await Assert.ThrowsAsync<GameplayUnavailableException>(()=>queued);
            Assert.Equal(10,await store.GetPointsAsync("1001",default));
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
