using Google.Cloud.Firestore;
using System.Text.Json;
namespace Backend;

public sealed class FirestoreRelayStore(FirestoreDb database,string channelId,string collection) : IRelayStore
{
    public Task<T> TransactionAsync<T>(Guid? id,bool head,Func<RelayChannel,RelayAction?,RelayMutation<T>> operation,CancellationToken ct)
    {
        var control=database.Collection(collection).Document(channelId);
        return database.RunTransactionAsync(async transaction=>{
            var controlSnapshot=await transaction.GetSnapshotAsync(control,ct);
            var state=controlSnapshot.Exists ? JsonSerializer.Deserialize<RelayChannel>(controlSnapshot.GetValue<string>("stateJson"))! : new();
            if(head)id=state.Queue.FirstOrDefault() is var first && first!=Guid.Empty ? first : null;
            DocumentReference? actionReference=id is {} key ? control.Collection("actions").Document(key.ToString("N")) : null;
            RelayAction? action=null;
            if(actionReference is not null){var snapshot=await transaction.GetSnapshotAsync(actionReference,ct);if(snapshot.Exists)action=JsonSerializer.Deserialize<RelayAction>(snapshot.GetValue<string>("actionJson"));}
            var result=operation(state,action);
            transaction.Set(control,new Dictionary<string,object>{{"stateJson",JsonSerializer.Serialize(state)},{"channelId",channelId},{"version",1L}});
            if(result.WriteAction && result.Action is {} changed)
                transaction.Set(control.Collection("actions").Document(changed.Envelope.Intent.ActionId.ToString("N")),new Dictionary<string,object>{{"actionJson",JsonSerializer.Serialize(changed)},{"userId",changed.Envelope.UserId},{"channelId",channelId},{"version",1L}});
            return result.Value;
        },TransactionOptions.ForMaxAttempts(5),ct);
    }
}
