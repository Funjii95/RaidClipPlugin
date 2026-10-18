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
    public void Connected_ready_plugin_allows_play_without_any_Twitch_live_confirmation()
    {
        var gate=new StreamAdmission();var epoch=gate.Begin(()=>true);
        Assert.True(gate.CanPlay(epoch));gate.End();Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Fresh_plugin_heartbeat_is_required_and_recovery_uses_current_connection()
    {
        var clock=new Clock();var last=clock.Now;var connected=true;
        var gate=new StreamAdmission(clock);var epoch=gate.Begin(()=>connected && clock.Now-last<=StreamAdmission.MaximumAge);
        Assert.True(gate.CanPlay(epoch));clock.Advance(TimeSpan.FromSeconds(21));Assert.False(gate.CanPlay(epoch));
        last=clock.Now;Assert.True(gate.CanPlay(epoch));connected=false;Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Old_sessions_cannot_authorize_a_new_plugin_connection()
    {
        var gate=new StreamAdmission();var old=gate.Begin(()=>true);
        gate.End();var epoch=gate.Begin(()=>true);Assert.False(gate.CanPlay(old));Assert.True(gate.CanPlay(epoch));
    }
    [Fact]
    public void Not_ready_and_old_messages_are_rejected()
    {
        var clock=new Clock();var ready=false;var gate=new StreamAdmission(clock);var epoch=gate.Begin(()=>ready);
        Assert.False(gate.CanPlay(epoch));ready=true;
        Assert.False(gate.CanPlay(epoch,clock.Now.AddMinutes(-1)));Assert.True(gate.CanPlay(epoch,clock.Now));
        ready=false;Assert.False(gate.CanPlay(epoch));
    }
    [Fact]
    public void Old_execution_context_cannot_commit_in_a_new_ready_session()
    {
        var gate=new StreamAdmission();var old=gate.Begin(()=>true);

        using var scope=gate.Enter(old);
        gate.End();var epoch=gate.Begin(()=>true);
        Assert.Throws<GameplayUnavailableException>(()=>gate.Commit(()=>throw new Exception("Must not execute")));
    }
    [Fact]
    public async Task Denied_save_rolls_back_mutable_legacy_accounts_and_does_not_leak_later()
    {
        var directory=Path.Combine(Path.GetTempPath(),"otter-gate",Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(directory);await store.SetPointsAsync("1001","Synthetic",10,0,default);
            var gate=new StreamAdmission();store.Admission=gate;var ready=false;var epoch=gate.Begin(()=>ready);
            await Assert.ThrowsAsync<GameplayUnavailableException>(()=>store.AddPointsAsync("1001","Synthetic",100,0,default));
            Assert.Equal(10,await store.GetPointsAsync("1001",default));
            ready=true;
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
            var gate=new StreamAdmission();store.Admission=gate;var epoch=gate.Begin(()=>true);
            var mutex=(SemaphoreSlim)typeof(ViewerPointStore).GetField("_lock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(store)!;
            await mutex.WaitAsync();var queued=store.AddPointsAsync("1001","Synthetic",99,0,default);
            gate.End();epoch=gate.Begin(()=>true);mutex.Release();
            await Assert.ThrowsAsync<GameplayUnavailableException>(()=>queued);
            Assert.Equal(10,await store.GetPointsAsync("1001",default));
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
