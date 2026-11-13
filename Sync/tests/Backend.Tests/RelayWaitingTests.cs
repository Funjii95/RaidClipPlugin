using Backend;
using SnapshotProtocol;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
namespace Backend.Tests;
public sealed class RelayWaitingTests
{
 private static BackendSettings Settings()=>new(){Twitch=new(){ChannelId="42"},Actions=new(){AllowedUserIds=["1001","1002"],PrivacyDeletionEnabled=true}};
 private static async Task<(ActionRelay Relay,MemoryRelayStore Store,Guid Session)> Ready(){var store=new MemoryRelayStore();var clock=new RelayClock();var relay=new ActionRelay(store,clock,Settings());var session=(await relay.OpenAsync(default)).SessionId;await relay.HeartbeatAsync(new(session,1,clock.Now,true,"{}"),default);return(relay,store,session);}
 [Fact] public async Task Work_wait_wakes_on_enqueue_without_a_poll_interval_and_keeps_full_execution_lease(){
  var (relay,store,session)=await Ready();var clock=new RelayClock();var other=new ActionRelay(store,clock,Settings());var elapsed=Stopwatch.StartNew();var waiting=relay.ClaimWaitingAsync(session,default);await Task.Delay(30);var intent=ActionApiFixture.Intent();await other.EnqueueAsync("1001",intent,session,default);var claim=await waiting.WaitAsync(TimeSpan.FromSeconds(1));Assert.Equal(intent.ActionId,claim.Envelope!.Intent.ActionId);Assert.InRange(claim.WaitedMilliseconds,1,1999);Assert.Equal(TimeSpan.FromSeconds(8),claim.Envelope.ExecuteBefore-claim.Envelope.ObservedAt);Assert.True(elapsed.Elapsed<TimeSpan.FromSeconds(1));
 }
 [Fact] public async Task Outcome_wait_returns_on_completion_and_never_runs_gameplay_again(){
  var (relay,_,session)=await Ready();var intent=ActionApiFixture.Intent();await relay.EnqueueAsync("1001",intent,session,default);await relay.ClaimAsync(session,default);var waiting=relay.ReadWaitingAsync("1001",intent.ActionId,default);await Task.Delay(30);Assert.False(waiting.IsCompleted);var outcome=new ActionOutcome(true,"applied","once");await relay.CompleteAsync(new(session,intent.ActionId,outcome),default);var read=await waiting.WaitAsync(TimeSpan.FromSeconds(1));Assert.Equal(outcome,read!.Outcome);Assert.Equal("applied",read.State);Assert.Equal(read,await relay.EnqueueAsync("1001",intent,session,default));
 }
 [Fact] public async Task Missing_and_foreign_receipts_return_immediately_without_exposing_data(){
  var (relay,_,session)=await Ready();var intent=ActionApiFixture.Intent();await relay.EnqueueAsync("1001",intent,session,default);Assert.Null(await relay.ReadWaitingAsync("1002",intent.ActionId,default).WaitAsync(TimeSpan.FromMilliseconds(200)));Assert.Null(await relay.ReadWaitingAsync("1001",Guid.NewGuid(),default).WaitAsync(TimeSpan.FromMilliseconds(200)));
 }
 [Fact] public async Task Cancellation_does_not_cancel_or_replay_the_pending_action(){
  var (relay,_,session)=await Ready();var intent=ActionApiFixture.Intent();await relay.EnqueueAsync("1001",intent,session,default);using var stop=new CancellationTokenSource(30);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>relay.ReadWaitingAsync("1001",intent.ActionId,stop.Token));Assert.Equal("queued",(await relay.ReadAsync("1001",intent.ActionId,default))!.State);
 }
 [Fact] public async Task Privacy_erasure_during_wait_never_reveals_a_saved_outcome(){
  var (relay,_,session)=await Ready();var intent=ActionApiFixture.Intent();await relay.EnqueueAsync("1001",intent,session,default);var waiting=relay.ReadWaitingAsync("1001",intent.ActionId,default);await Task.Delay(20);await relay.PurgeUserAsync("1001",default);Assert.Null(await waiting.WaitAsync(TimeSpan.FromSeconds(1)));
 }
 [Fact] public async Task Idle_wait_is_bounded_and_does_not_modify_the_queue(){
  var (relay,_,session)=await Ready();var timer=Stopwatch.StartNew();var claim=await relay.ClaimWaitingAsync(session,default);Assert.Null(claim.Envelope);Assert.InRange(timer.Elapsed.TotalSeconds,1.8,5);Assert.InRange(claim.WaitedMilliseconds,1800,2000);Assert.Null(await relay.ClaimAsync(session,default));
 }
 [Fact] public async Task Waiting_routes_keep_viewer_and_plugin_authorization_separate(){
  await using var api=new ActionApiFixture();var session=await api.ReadyAsync();Assert.Equal(HttpStatusCode.Unauthorized,(await api.Viewer.PostAsync("/api/v1/plugin/actions/claim/"+session+"/wait",null)).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await api.Plugin.GetAsync("/api/v1/play/actions/"+Guid.NewGuid()+"/wait")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await api.Viewer.GetAsync("/api/v1/play/actions/"+Guid.NewGuid()+"/wait")).StatusCode);
 }
}
