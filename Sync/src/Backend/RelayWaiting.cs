using SnapshotProtocol;
namespace Backend;
public sealed partial class MemoryRelayStore
{
 private TaskCompletionSource changed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private void Pulse(){var previous=changed;changed=new(TaskCreationOptions.RunContinuationsAsynchronously);previous.TrySetResult();}
 public Task WaitForOutcomeAsync(Guid id,TimeSpan duration,CancellationToken ct)=>WaitAsync(()=>!actions.TryGetValue(id,out var a)||a.State is not ("queued" or "executing"),duration,ct);
 public Task WaitForWorkAsync(Guid session,TimeSpan duration,CancellationToken ct)=>WaitAsync(()=>channel.SessionId!=session||channel.Queue.Count>0,duration,ct);
 private async Task WaitAsync(Func<bool> ready,TimeSpan duration,CancellationToken ct){
  var timer=System.Diagnostics.Stopwatch.StartNew();
  while(timer.Elapsed<duration){Task signal;await gate.WaitAsync(ct);try{if(ready())return;signal=changed.Task;}finally{gate.Release();}
   var remaining=duration-timer.Elapsed;if(remaining<=TimeSpan.Zero)return;try{await signal.WaitAsync(remaining,ct);}catch(TimeoutException){return;}
  }
 }
}
public sealed partial class ActionRelay
{
 private readonly SemaphoreSlim waitSlots=new(64,64);
 public async Task<ActionReceipt?> ReadWaitingAsync(string user,Guid id,CancellationToken ct){
  var receipt=await ReadAsync(user,id,ct);if(receipt?.State is not ("queued" or "executing")||!await waitSlots.WaitAsync(0,ct))return receipt;
  try{await store.WaitForOutcomeAsync(id,TimeSpan.FromSeconds(4),ct);return await ReadAsync(user,id,ct);}finally{waitSlots.Release();}
 }
 public async Task<RelayClaim> ClaimWaitingAsync(Guid session,CancellationToken ct){
  var action=await ClaimAsync(session,ct);if(action is not null||!await waitSlots.WaitAsync(0,ct))return new(action,0);
  try{var elapsed=System.Diagnostics.Stopwatch.StartNew();await store.WaitForWorkAsync(session,TimeSpan.FromSeconds(2),ct);var waited=(int)Math.Min(2000,elapsed.ElapsedMilliseconds);return new(await ClaimAsync(session,ct),waited);}finally{waitSlots.Release();}
 }
}
