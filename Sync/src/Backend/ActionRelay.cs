using System.Text.Json;
using SnapshotProtocol;
namespace Backend;

public sealed class RelayChannel
{
    public Guid SessionId {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
    public long Sequence {get;set;}
    // Legacy wire/storage field retained for compatibility; not an authorization input.
    public string StreamId {get;set;}="";
    public string CatalogJson {get;set;}="{}";
    public List<Guid> Queue {get;set;}=[];
    public HashSet<string> PrivacyBlockedUsers {get;set;}=[];
    public Dictionary<string,SelfDeletionJob> DeletionRequests {get;set;}=[];
}
public sealed class RelayAction
{
    public required ActionEnvelope Envelope {get;set;}
    public string State {get;set;}="queued";
    public ActionOutcome? Outcome {get;set;}
    public DateTimeOffset QueuedUntil {get;set;}
}
public sealed record RelayMutation<T>(T Value, RelayAction? Action = null, bool WriteAction = false);
public interface IRelayStore
{
    Task<RelayChannel> ReadChannelAsync(CancellationToken ct);
    Task PurgeUserActionsAsync(string user,CancellationToken ct);
    Task<T> TransactionAsync<T>(Guid? id, bool head, Func<RelayChannel,RelayAction?,RelayMutation<T>> operation,CancellationToken ct);
}
// Test host only. Production configuration cannot select this store.
public sealed class MemoryRelayStore : IRelayStore
{
    private readonly SemaphoreSlim gate=new(1,1);
    private RelayChannel channel=new();
    private readonly Dictionary<Guid,RelayAction> actions=[];
    public async Task<RelayChannel> ReadChannelAsync(CancellationToken ct){await gate.WaitAsync(ct);try{return JsonSerializer.Deserialize<RelayChannel>(JsonSerializer.Serialize(channel))!;}finally{gate.Release();}}
    public async Task PurgeUserActionsAsync(string user,CancellationToken ct){
        await gate.WaitAsync(ct);try{channel.PrivacyBlockedUsers.Add(user);channel.ExpiresAt=default;
            foreach(var id in actions.Where(p=>p.Value.Envelope.UserId==user).Select(p=>p.Key).ToArray()){actions.Remove(id);channel.Queue.Remove(id);}
        }finally{gate.Release();}
    }
    public async Task<T> TransactionAsync<T>(Guid? id,bool head,Func<RelayChannel,RelayAction?,RelayMutation<T>> operation,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try {
            var state=JsonSerializer.Deserialize<RelayChannel>(JsonSerializer.Serialize(channel))!;
            if(head)id=state.Queue.FirstOrDefault() is var first && first!=Guid.Empty ? first : null;
            var action=id is {} key && actions.TryGetValue(key,out var found) ? JsonSerializer.Deserialize<RelayAction>(JsonSerializer.Serialize(found)) : null;
            var mutation=operation(state,action);
            channel=state;
            if(mutation.WriteAction && mutation.Action is {} changed)actions[changed.Envelope.Intent.ActionId]=changed;
            return mutation.Value;
        } finally {gate.Release();}
    }
}
public sealed class RelayDenied(int status,string code) : Exception(code)
{
    public int Status {get;}=status;
    public string Code {get;}=code;
}
// Optional informational probe only; never registered or consulted by ActionRelay.
public sealed record LiveConfirmation(string StreamId, DateTimeOffset CheckedAt);
public interface ITwitchLiveProbe {Task<LiveConfirmation?> CheckAsync(CancellationToken ct);}

public sealed partial class ActionRelay(IRelayStore store,TimeProvider clock,BackendSettings settings,IPlayerReader? players=null)
{
    private DateTimeOffset Now=>clock.GetUtcNow();
    private bool Ready(RelayChannel s)=>s.SessionId!=Guid.Empty && s.ExpiresAt>Now && s.Sequence>0;
    private static ActionReceipt Receipt(RelayAction a)=>new(a.Envelope.Intent.ActionId,a.State,a.Outcome);
    private void Scope(string user){if(!Ids.IsNumeric(user) || !settings.Actions.AutomaticPlayerEnrollment && !settings.Actions.AllowedUserIds.Contains(user))throw new RelayDenied(403,"test_user_not_allowed");}
    public async Task PurgeUserAsync(string user,CancellationToken ct){Scope(user);if(!settings.Actions.PrivacyDeletionEnabled)throw new RelayDenied(503,"privacy_not_enabled");await store.PurgeUserActionsAsync(user,ct);}
    public async Task<RelaySession> OpenAsync(CancellationToken ct)
    {
        return await store.TransactionAsync<RelaySession>(null,false,(s,_)=>{
            if(s.SessionId!=Guid.Empty && s.ExpiresAt>Now)throw new RelayDenied(409,"plugin_already_connected");
            s.SessionId=Guid.NewGuid();s.Sequence=0;s.ExpiresAt=Now.AddSeconds(10);s.StreamId="";s.Queue.Clear();
            // Open alone is not gameplay-ready. First authenticated heartbeat must succeed.
            s.CatalogJson="{}";
            return new(new(s.SessionId,s.ExpiresAt));
        },ct);
    }
    public async Task<RelaySession> HeartbeatAsync(RelayHeartbeat beat,CancellationToken ct)
    {
        if(beat.CatalogJson.Length>48000 || Math.Abs((Now-beat.SentAt).TotalSeconds)>5)throw new RelayDenied(400,"stale_heartbeat");
        using(var doc=JsonDocument.Parse(beat.CatalogJson))if(doc.RootElement.ValueKind!=JsonValueKind.Object)throw new RelayDenied(400,"catalog_invalid");
        return await store.TransactionAsync<RelaySession>(null,false,(s,_)=>{
            if(s.SessionId!=beat.SessionId || s.ExpiresAt<=Now || beat.Sequence<=s.Sequence || Math.Abs((Now-beat.SentAt).TotalSeconds)>5)throw new RelayDenied(409,"stale_session");
            s.Sequence=beat.Sequence;
            if(!beat.Ready){s.ExpiresAt=Now;return new(new(s.SessionId,s.ExpiresAt));}
            s.ExpiresAt=Now.AddSeconds(15);s.CatalogJson=beat.CatalogJson;
            return new(new(s.SessionId,s.ExpiresAt));
        },ct);
    }
    public Task<bool> CloseAsync(Guid session,CancellationToken ct)=>store.TransactionAsync<bool>(null,false,(s,_)=>{
        if(s.SessionId==session)s.ExpiresAt=Now;return new(true);
    },ct);
    public async Task<RelayAvailability> AvailabilityAsync(CancellationToken ct)
    {
        var s=await store.ReadChannelAsync(ct);
        return new(Ready(s),Ready(s)?"ready":"plugin_not_ready",s.CatalogJson,Ready(s)?s.SessionId:null,settings.Actions.SelfDeletionEnabled&&settings.Actions.PrivacyDeletionEnabled);
    }
    public async Task<ActionReceipt> EnqueueAsync(string user,RpgIntent intent,Guid expectedSession,CancellationToken ct)
    {
        Scope(user);

        // This feature is intentionally chat-only; never queue browser requests.
        if(intent.Kind=="openShells")throw new RelayDenied(403,"chat_only");
        if(settings.Actions.AutomaticPlayerEnrollment && (players is null || await players.GetAsync(user,ct) is null))throw new RelayDenied(403,"profile_not_enrolled");
        var hash=RpgIntents.Hash(intent);
        return await store.TransactionAsync<ActionReceipt>(intent.ActionId,false,(s,a)=>{
            if(RpgIntents.IsAdmin(intent.Kind)&&user!=settings.Twitch.ChannelId&&!(Ready(s)&&AdminPermissions.IsModerator(s.CatalogJson,user)&&AdminPermissions.ModeratorMay(intent)))throw new RelayDenied(403,"admin_required");
            if(s.PrivacyBlockedUsers.Contains(user)||s.DeletionRequests.ContainsKey(user))throw new RelayDenied(403,"privacy_blocked");
            if(a is not null){if(a.Envelope.UserId!=user || a.Envelope.IntentHash!=hash)throw new RelayDenied(409,"action_id_conflict");return new(Receipt(a));}
            if(expectedSession!=s.SessionId || !Ready(s) || s.Sequence<=0){
                // Persist a terminal refusal: a delayed duplicate can never become a later play.
                a=new(){Envelope=new(settings.Twitch.ChannelId,user,expectedSession,s.StreamId,default,intent,hash),QueuedUntil=Now,State="rejected",Outcome=new(false,"not_ready","Aktion nicht ausgeführt: Plugin oder Sitzung nicht bereit.")};
                return new(Receipt(a),a,true);
            }
            if(s.Queue.Count>=128)throw new RelayDenied(429,"queue_full");
            a=new(){Envelope=new(settings.Twitch.ChannelId,user,s.SessionId,s.StreamId,default,intent,hash),QueuedUntil=Now.AddSeconds(30)};
            s.Queue.Add(intent.ActionId);return new(Receipt(a),a,true);
        },ct);
    }
    public async Task<ActionReceipt?> ReadAsync(string user,Guid id,CancellationToken ct)
    {
        Scope(user);
        return await store.TransactionAsync<ActionReceipt?>(id,false,(s,a)=>{
            if(a is null || a.Envelope.UserId!=user)return new(null);
            Expire(s,a);return new(Receipt(a),a,true);
        },ct);
    }
    private void Expire(RelayChannel s,RelayAction a)
    {
        if(a.State=="queued" && (a.QueuedUntil<=Now || a.Envelope.SessionId!=s.SessionId || s.ExpiresAt<=Now))
        {a.State="rejected";a.Outcome=new(false,"expired","Aktion wurde nicht ausgeführt.");s.Queue.Remove(a.Envelope.Intent.ActionId);}
        if(a.State=="executing" && a.Envelope.ExecuteBefore<=Now)
        {a.State="unknown";a.Outcome=new(null,"outcome_unknown","Ergebnis unklar. Keine neue Aktion zum Wiederholen anlegen.");s.Queue.Remove(a.Envelope.Intent.ActionId);}
    }
    public async Task<ActionEnvelope?> ClaimAsync(Guid session,CancellationToken ct)
    {
        return await store.TransactionAsync<ActionEnvelope?>(null,true,(s,a)=>{
            if(s.SessionId!=session || !Ready(s) || s.Sequence<=0)throw new RelayDenied(409,"plugin_disconnected");
            if(a is null)return new(null);
            if(s.DeletionRequests.ContainsKey(a.Envelope.UserId)&&a.State=="queued"){
                s.Queue.Remove(a.Envelope.Intent.ActionId);a.State="rejected";a.Outcome=new(false,"privacy_pending","Datenlöschung beantragt.");return new(null,a,true);
            }
            if(s.PrivacyBlockedUsers.Contains(a.Envelope.UserId)){s.Queue.Remove(a.Envelope.Intent.ActionId);return new(null);}
            Expire(s,a);
            if(a.State is not ("queued" or "executing")){s.Queue.Remove(a.Envelope.Intent.ActionId);return new(null,a,true);}
            a.State="executing";
            if(a.Envelope.ExecuteBefore==default)a.Envelope=a.Envelope with{ExecuteBefore=Now.AddSeconds(8)<s.ExpiresAt?Now.AddSeconds(8):s.ExpiresAt};
            return new(a.Envelope with{ObservedAt=Now},a,true);
        },ct);
    }
    public Task<ActionReceipt> CompleteAsync(RelayComplete done,CancellationToken ct)
    {
        if(done.Outcome.Message.Length>1500 || done.Outcome.Code.Length>80 || JsonSerializer.SerializeToUtf8Bytes(done.Outcome).Length>ActionResultLimits.MaximumOutcomeBytes ||
            done.Outcome.Success is null && done.Outcome.Code!="outcome_unknown")throw new RelayDenied(400,"result_invalid");
        return store.TransactionAsync<ActionReceipt>(done.ActionId,false,(s,a)=>{
            if(a is not null && s.PrivacyBlockedUsers.Contains(a.Envelope.UserId))throw new RelayDenied(410,"privacy_erased");
            if(a is null || a.Envelope.SessionId!=done.SessionId || a.State=="queued")throw new RelayDenied(409,"unclaimed_action");
            if(a.State is "applied" or "rejected"){
                if(a.Outcome!=done.Outcome)throw new RelayDenied(409,"result_conflict");return new(Receipt(a));
            }
            a.Outcome=done.Outcome;a.State=done.Outcome.Success is true?"applied":done.Outcome.Success is false?"rejected":"unknown";
            s.Queue.Remove(done.ActionId);return new(Receipt(a),a,true);
        },ct);
    }
}
