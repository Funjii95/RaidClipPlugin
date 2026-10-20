using SnapshotProtocol;
namespace RaidClipPlugin.Services;

// Runs independently of gameplay. Uses the existing stopped-plugin maintenance
// fence and privacy/outbox pipeline; it never cancels a duel or books a refund.
internal sealed class SelfDeletionWorker(ViewerPointStore source,IReadOnlySet<string> users,
    Func<CancellationToken,Task<SelfDeletionJob[]>> fetch,Func<SelfDeletionJob,CancellationToken,Task> acknowledge,Func<string,bool>? enrolled=null)
{
    private readonly SemaphoreSlim gate=new(1,1);
    internal async Task TickAsync(CancellationToken ct,bool allowLocalErasure=true){
        await gate.WaitAsync(ct);try{
            foreach(var job in await fetch(ct)){
                var local=await source.ReadPrivacyRequestAsync(job.UserId,ct);
                if(job.Completed||job.RequestId==Guid.Empty||!(users.Contains(job.UserId)||enrolled?.Invoke(job.UserId)==true||local is not null))throw new InvalidDataException("Invalid deletion scope.");
                if(local?.CloudErased==true){await acknowledge(job,ct);continue;}
                if(!allowLocalErasure||source.Admission is {} admission&&!admission.CanMaintain)continue;
                // EnterMaintenance inside this method checks again atomically if
                // gameplay started after the observation above.
                try{await source.RequestPrivacyDeletionAsync(job.UserId,job.RequestId,ct);}
                catch(InvalidOperationException)when(source.Admission is {} a&&!a.CanMaintain){continue;}
                local=await source.ReadPrivacyRequestAsync(job.UserId,ct);
                if(local?.CloudErased==true)await acknowledge(job,ct);
            }
        }finally{gate.Release();}
    }
}
