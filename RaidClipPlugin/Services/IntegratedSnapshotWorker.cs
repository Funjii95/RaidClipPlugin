using System.Text.Json;
using SnapshotProtocol;
using SnapshotDelivery;

namespace RaidClipPlugin.Services;

internal sealed class CloudSyncOptions
{
    public bool Enabled {get;set;}
    public bool AutomaticPlayerEnrollment {get;set;}
    public string Endpoint {get;set;}="https://funjii-otter-rpg-publisher-test-134126914046.europe-west3.run.app/api/v1/snapshots";
    public string ChannelId {get;set;}="42812664";
    public string[] AllowedUserIds {get;set;}=[];
    public string ProtectedKeyFile {get;set;}="";
    public void Validate()
    {
        if((Endpoint!="https://funjii-otter-rpg-publisher-test-134126914046.europe-west3.run.app/api/v1/snapshots" && Endpoint!="https://rc1---funjii-otter-rpg-publisher-test-cwzl75ajla-ey.a.run.app/api/v1/snapshots") ||
            ChannelId!="42812664" || AllowedUserIds.Length is <1 or >100 ||
            AllowedUserIds.Distinct().Count()!=AllowedUserIds.Length ||
            AllowedUserIds.Any(id=>id.Length is <1 or >25 || id.Any(c=>c is <'0' or >'9')) ||
            !Path.IsPathFullyQualified(ProtectedKeyFile))throw new InvalidDataException("Invalid explicit test sync configuration.");
    }
}

internal sealed class IntegratedSnapshotWorker
{
    private readonly ViewerPointStore source;
    private readonly LocalOutboxStore outbox;
    private readonly string channel;
    private readonly string[] users;
    private readonly bool automaticPlayerEnrollment;
    private readonly Func<string,string,bool>? eligible;
    private readonly Func<CancellationToken,Task> capabilities;
    private readonly Func<string,CancellationToken,Task<StoredSnapshot?>> read;
    private readonly Func<SnapshotMessage,CancellationToken,Task<DeliveryResult>> send;
    private readonly Func<string,CancellationToken,Task>? purgeActions;
    private readonly SemaphoreSlim wake=new(1,1);
    internal string Status {get;private set;}="Initialisierung";
    internal event Action<string>? StatusChanged;
    internal event Action<bool>? AvailabilityChanged;
    private void Availability(bool value){if(AvailabilityChanged is {} listeners)foreach(Action<bool> listener in listeners.GetInvocationList())try{listener(value);}catch{}}
    internal IntegratedSnapshotWorker(ViewerPointStore source,LocalOutboxStore outbox,string channel,string[] users,
        Func<CancellationToken,Task> capabilities,Func<string,CancellationToken,Task<StoredSnapshot?>> read,
        Func<SnapshotMessage,CancellationToken,Task<DeliveryResult>> send,Func<string,CancellationToken,Task>? purgeActions=null,bool automaticPlayerEnrollment=false,Func<string,string,bool>? eligible=null)
    {this.source=source;this.outbox=outbox;this.channel=channel;this.users=users;this.capabilities=capabilities;this.read=read;this.send=send;this.purgeActions=purgeActions;this.automaticPlayerEnrollment=automaticPlayerEnrollment;this.eligible=eligible;}

    private void Signal(){try{wake.Release();}catch(SemaphoreFullException){}}
    private void Report(string status)
    {
        if(Status==status)return;Status=status;
        if(StatusChanged is {} listeners)foreach(Action<string> listener in listeners.GetInvocationList())try{listener(status);}catch{}
    }
    internal async Task RunAsync(CancellationToken ct)
    {
        source.Persisted+=Signal;
        var failures=0;
        try
        {
            while(true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await SynchronizeOnceAsync(ct);
                    Availability(true);failures=0;Report("Cloud synchronisiert (vollständiges RPG)");
                    await wake.WaitAsync(TimeSpan.FromSeconds(30),ct);
                }
                catch(Exception error) when(error is SnapshotHeadReadException {Retryable:true} or RetrySyncException)
                {
                    Availability(false);failures=Math.Min(6,failures+1);Report("Cloud offline – lokaler Spielstand und Warteschlange bleiben erhalten");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(60,1<<failures)),ct);
                }
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){Report("Cloud-Sender beendet; ausstehende Daten bleiben gespeichert");}
        catch{Report("Cloud-Sender angehalten: Vertrag, Zugang oder Datenkonflikt prüfen; lokale Speicherung bleibt aktiv");}
        finally{Availability(false);source.Persisted-=Signal;}
    }

    // Reads the committed legacy file, never live mutable objects or cloud-owned player state.
    internal async Task SynchronizeOnceAsync(CancellationToken ct=default)
    {
        await capabilities(ct); // Old servers must fail closed before any incomplete projection is sent.
        var bytes=await source.ReadCommittedFileAsync(ct);
        if(bytes is null)return;
        using var document=JsonDocument.Parse(bytes);
        if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Invalid legacy save.");
        var privacy=await source.PrivacyRequestsAsync(ct);
        // Independent account heads can be checked concurrently. Every account
        // retains its own dispatch lease, durable order and revision validation.
        var selected=users.ToHashSet(StringComparer.Ordinal);
        if(automaticPlayerEnrollment)foreach(var entry in document.RootElement.EnumerateObject()){
            if(entry.Name.Length is <1 or >25 || entry.Name.Any(c=>c is <'0' or >'9'))continue;
            var name=entry.Value.TryGetProperty("DisplayName",out var display)?display.GetString()??"":"";
            if(eligible?.Invoke(entry.Name,name)==false)continue;
            if(entry.Value.TryGetProperty("Points",out var points)&&points.TryGetInt64(out var balance)&&balance>0 || await outbox.ReadAsync(entry.Name,ct) is not null)selected.Add(entry.Name);
        }
        await Parallel.ForEachAsync(selected.Concat(privacy.Select(p=>p.UserId)).Distinct(),
            new ParallelOptions {MaxDegreeOfParallelism=2,CancellationToken=ct},
            (id,_)=>new ValueTask(SynchronizeUserAsync(id)));
        async Task SynchronizeUserAsync(string id)
        {
            await using var dispatch=await outbox.AcquireDispatchLeaseAsync(id,ct);
            if(await source.ReadPrivacyRequestAsync(id,ct) is {} deletion){
                if(deletion.CloudErased)return;
                if(!deletion.LocalErased || purgeActions is null)throw new RetrySyncException();
                try{
                    await purgeActions(id,ct);
                    var privacyHead=await read(id,ct);
                    if(privacyHead is not null && !privacyHead.Deleted){
                        await outbox.QueuePrivacyDeletionAsync(privacyHead,deletion.OperationId,ct);
                        await DrainAsync(id,ct);
                        if((await read(id,ct))?.Deleted!=true)throw new RetrySyncException();
                    }
                    await outbox.ForgetPrivacyPayloadAsync(id,ct);
                    await source.MarkPrivacyCloudErasedAsync(id,ct);
                }catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch{throw new RetrySyncException();}
                return;
            }
            var state=await outbox.ReadAsync(id,ct);
            // Deliver the durable prefix before reserving a newer revision. All messages are full states.
            if(state is not null)await DrainAsync(id,ct);
            state=await outbox.ReadAsync(id,ct);
            if(!document.RootElement.TryGetProperty(id,out var account))
            {
                if(state is not null)throw new InvalidDataException("Account disappeared; explicit deletion/restore required.");
                return;
            }
            var candidate=new SnapshotMessage(1,channel,id,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,account.GetRawText());
            var clean=SnapshotValidation.Validate(candidate,channel).AccountJson!;
            var head=await read(id,ct);
            if(head is null){
                if(!automaticPlayerEnrollment || state is not null)throw new InvalidDataException("Cloud account requires explicit initial provisioning.");
                await outbox.CommitAsync(id,0,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,clean,ct:ct);
                await DrainAsync(id,ct);return;
            }
            if(head.Deleted){if(automaticPlayerEnrollment)return;throw new InvalidDataException("Explicit restore required.");}
            if(state is null)
            {
                // Bootstrap only if the existing cloud content hash proves exact equality.
                var baseline=candidate with {Revision=head.Revision,SnapshotId=head.SnapshotId,CapturedAt=head.CapturedAt,AccountJson=clean};
                await outbox.InitializeBaselineAsync(baseline,head.ContentHash,ct);
                return;
            }
            var last=SnapshotValidation.Validate(state.LastMessage,channel);
            if(head.ChannelId!=channel || head.UserId!=id || head.Revision!=state.Revision ||
                head.SnapshotId!=state.CommitId || head.ContentHash!=last.ContentHash)
                throw new InvalidDataException("Cloud and local revision diverged; automatic rebase forbidden.");
            if(state.PrivateAccountJson==clean)return;
            await outbox.CommitAsync(id,state.Revision,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,clean,ct:ct);
            await DrainAsync(id,ct);
        }
    }

    private async Task DrainAsync(string id,CancellationToken ct)
    {
        while((await outbox.ReadAsync(id,ct))?.Pending.FirstOrDefault() is {} message)
        {
            var head=await read(id,ct);
            if(head is null){
                if(!automaticPlayerEnrollment || message.Revision!=1 || message.Kind!=SnapshotKind.Upsert)throw new InvalidDataException("Cloud profile missing.");
            }
            var clean=SnapshotValidation.Validate(message,channel);
            var duplicate=head is not null && head.Revision==message.Revision && head.SnapshotId==message.SnapshotId && head.ContentHash==clean.ContentHash;
            if(head is not null && (head.ChannelId!=channel || head.UserId!=id || head.Deleted && !duplicate || (!duplicate && head.Revision!=message.Revision-1)))
                throw new InvalidDataException("Stale or conflicting snapshot; no new revision or gameplay replay.");
            var result=await send(message,ct);
            if(result.Status==DeliveryStatus.RetryLater)throw new RetrySyncException();
            if(result.Status!=DeliveryStatus.Delivered || result.Decision is not (SnapshotDecision.Applied or SnapshotDecision.Duplicate))
                throw new InvalidDataException("Unaccepted acknowledgement; queue retained.");
            await outbox.AcknowledgeAsync(id,message.SnapshotId,message.Revision,result.Decision.Value,ct);
        }
    }
    private sealed class RetrySyncException : Exception {}
}
