using SnapshotProtocol;
namespace Backend;

public sealed partial class ActionRelay
{
    private void RequireSelfDeletion(){if(!settings.Actions.SelfDeletionEnabled||!settings.Actions.PrivacyDeletionEnabled)throw new RelayDenied(503,"self_deletion_disabled");}
    public async Task<SelfDeletionJob?> SelfDeletionStatusAsync(string user,CancellationToken ct){Scope(user);RequireSelfDeletion();return (await store.ReadChannelAsync(ct)).DeletionRequests.GetValueOrDefault(user);}
    public Task<SelfDeletionJob> RequestSelfDeletionAsync(string user,SelfDeletionIntent intent,CancellationToken ct){
        Scope(user);RequireSelfDeletion();
        if(intent.RequestId==Guid.Empty||intent.Confirmation!="DELETE_MY_RPG_ACCOUNT")throw new RelayDenied(400,"confirmation_required");
        return store.TransactionAsync<SelfDeletionJob>(null,false,(s,_)=>{
            if(s.DeletionRequests.TryGetValue(user,out var existing))return new(existing);
            if(s.DeletionRequests.Count>=100)throw new RelayDenied(429,"deletion_queue_full");
            var job=new SelfDeletionJob(user,intent.RequestId,Now);s.DeletionRequests.Add(user,job);return new(job);
        },ct);
    }
    public async Task<SelfDeletionJob[]> PendingSelfDeletionsAsync(CancellationToken ct){RequireSelfDeletion();return (await store.ReadChannelAsync(ct)).DeletionRequests.Values.Where(j=>!j.Completed&&(settings.Actions.AutomaticPlayerEnrollment||settings.Actions.AllowedUserIds.Contains(j.UserId))).Take(100).ToArray();}
    public Task<SelfDeletionJob> CompleteSelfDeletionAsync(string user,Guid requestId,CancellationToken ct){
        Scope(user);RequireSelfDeletion();return store.TransactionAsync<SelfDeletionJob>(null,false,(s,_)=>{
            if(!s.DeletionRequests.TryGetValue(user,out var job)||job.RequestId!=requestId)throw new RelayDenied(409,"deletion_request_mismatch");
            if(!s.PrivacyBlockedUsers.Contains(user))throw new RelayDenied(409,"cloud_purge_not_confirmed");
            job=job with{Completed=true};s.DeletionRequests[user]=job;return new(job);
        },ct);
    }
}
