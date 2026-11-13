using Google.Cloud.Firestore;
using System.Text.Json;
namespace Backend;

public sealed class FirestoreRelayStore(FirestoreDb database,string channelId,string collection) : IRelayStore
{
    private readonly SemaphoreSlim transactions = new(1,1);
    public Task WaitForOutcomeAsync(Guid id,TimeSpan duration,CancellationToken ct)=>WaitAsync(database.Collection(collection).Document(channelId).Collection("actions").Document(id.ToString("N")),snapshot=>!snapshot.Exists||snapshot.ContainsField("privacyErased")||JsonSerializer.Deserialize<RelayAction>(snapshot.GetValue<string>("actionJson"))!.State is not ("queued" or "executing"),duration,ct);
    public Task WaitForWorkAsync(Guid session,TimeSpan duration,CancellationToken ct)=>WaitAsync(database.Collection(collection).Document(channelId),snapshot=>!snapshot.Exists||JsonSerializer.Deserialize<RelayChannel>(snapshot.GetValue<string>("stateJson")) is {} state&&(state.SessionId!=session||state.Queue.Count>0),duration,ct);
    private static async Task WaitAsync(DocumentReference reference,Func<DocumentSnapshot,bool> ready,TimeSpan duration,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(duration);
        var signal=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener=reference.Listen(snapshot=>{if(ready(snapshot))signal.TrySetResult();},deadline.Token);
        try{var finished=await Task.WhenAny(signal.Task,listener.ListenerTask);await finished;}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested&&deadline.IsCancellationRequested){}
        finally{deadline.Cancel();try{await listener.StopAsync();}catch(OperationCanceledException)when(deadline.IsCancellationRequested){}}
        ct.ThrowIfCancellationRequested();
    }
    public async Task<T> ObserveAsync<T>(Guid? id,Func<RelayChannel,RelayAction?,T> observation,CancellationToken ct)
    {
        RelayAction? action=null;
        if(id is {} key){
            var snapshot=await database.Collection(collection).Document(channelId).Collection("actions").Document(key.ToString("N")).GetSnapshotAsync(ct);
            if(snapshot.Exists){if(snapshot.ContainsField("privacyErased"))throw new RelayDenied(410,"privacy_erased");action=JsonSerializer.Deserialize<RelayAction>(snapshot.GetValue<string>("actionJson"));}
        }
        // Observe the privacy fence after reading the receipt. A deletion that has
        // already blocked the account must never expose an earlier saved result.
        return observation(await ReadChannelAsync(ct),action);
    }
    public async Task<RelayChannel> ReadChannelAsync(CancellationToken ct)
    {
        var snapshot=await database.Collection(collection).Document(channelId).GetSnapshotAsync(ct);
        return snapshot.Exists?JsonSerializer.Deserialize<RelayChannel>(snapshot.GetValue<string>("stateJson"))!:new();
    }
    public async Task PurgeUserActionsAsync(string user,CancellationToken ct)
    {
        await TransactionAsync<bool>(null,false,(s,_)=>{s.PrivacyBlockedUsers.Add(user);s.ExpiresAt=default;s.Queue.Clear();return new(true);},ct);
        var actions=database.Collection(collection).Document(channelId).Collection("actions");
        while(true){
            var matches=await actions.WhereEqualTo("userId",user).Limit(200).GetSnapshotAsync(ct);
            if(matches.Count==0)return;
            var batch=database.StartBatch();
            foreach(var document in matches.Documents)
                // Keep only a spent ActionId. No result/profile/parameters/user fields remain.
                batch.Set(document.Reference,new Dictionary<string,object>{{"privacyErased",true},{"version",1L}});
            await batch.CommitAsync(ct);
        }
    }
    public async Task<T> TransactionAsync<T>(Guid? id,bool head,Func<RelayChannel,RelayAction?,RelayMutation<T>> operation,CancellationToken ct)
    {
        await transactions.WaitAsync(ct);
        try {
        var control=database.Collection(collection).Document(channelId);
        return await database.RunTransactionAsync(async transaction=>{
            var controlSnapshot=await transaction.GetSnapshotAsync(control,ct);
            var state=controlSnapshot.Exists ? JsonSerializer.Deserialize<RelayChannel>(controlSnapshot.GetValue<string>("stateJson"))! : new();
            var originalState=JsonSerializer.Serialize(state);
            if(head)id=state.Queue.FirstOrDefault() is var first && first!=Guid.Empty ? first : null;
            DocumentReference? actionReference=id is {} key ? control.Collection("actions").Document(key.ToString("N")) : null;
            RelayAction? action=null;
            if(actionReference is not null){var snapshot=await transaction.GetSnapshotAsync(actionReference,ct);if(snapshot.Exists){if(snapshot.ContainsField("privacyErased"))throw new RelayDenied(410,"privacy_erased");action=JsonSerializer.Deserialize<RelayAction>(snapshot.GetValue<string>("actionJson"));}}
            var originalAction=action is null?null:JsonSerializer.Serialize(action);
            var result=operation(state,action);
            var updatedState=JsonSerializer.Serialize(state);
            if(updatedState!=originalState)
                transaction.Set(control,new Dictionary<string,object>{{"stateJson",updatedState},{"channelId",channelId},{"version",1L}});
            if(result.WriteAction && result.Action is {} changed && JsonSerializer.Serialize(changed)!=originalAction)
                transaction.Set(control.Collection("actions").Document(changed.Envelope.Intent.ActionId.ToString("N")),new Dictionary<string,object>{{"actionJson",JsonSerializer.Serialize(changed)},{"userId",changed.Envelope.UserId},{"channelId",channelId},{"version",1L}});
            return result.Value;
        },TransactionOptions.ForMaxAttempts(5),ct);
        } finally { transactions.Release(); }
    }
}
