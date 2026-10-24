using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class DuelService
{
    private sealed record RemoteDuelResult(string EventId,long Stake,bool Won,bool Paid,string Message);
    private readonly Dictionary<string,RemoteDuelResult> _remoteResults=new(StringComparer.Ordinal);
    private void RecordRemoteResult(PendingDuel duel,string? winner=null,string? detail=null){
        var paid=duel.State==DuelState.Paid;
        _remoteResults[duel.Challenger.UserId]=new(duel.Id,duel.Stake,winner==duel.Challenger.UserId,paid,detail??(paid?"Originale Plugin-Duellabrechnung.":"Duell beendet; reservierter Einsatz zurückgegeben."));
        _remoteResults[duel.Target.Id]=new(duel.Id,duel.Stake,winner==duel.Target.Id,paid,detail??(paid?"Originale Plugin-Duellabrechnung.":"Duell beendet; kein Einsatz abgebucht."));
    }
    private readonly AsyncLocal<string?> _remoteExpected=new();
    private readonly AsyncLocal<IReadOnlySet<string>?> _remoteUsers=new();
    private sealed class RemoteResult { public bool Accepted; }
    private readonly AsyncLocal<RemoteResult?> _remoteAccepted=new();
    internal string RemoteCommand(string operation)=>operation switch{"challenge"=>_config.DuelCommand,"accept"=>_config.AcceptCommand,"deny"=>_config.DenyCommand,_=>""};
    internal object RemoteRules=>new{id="duel",name="Duell",enabled=_config.Enabled,canStart=false,canJoin=false,usesStake=true,
        minimumStake=_config.MinimumBet,maximumStake=_config.MaximumBet,cooldownSeconds=_config.UserCooldownSeconds,joinSeconds=_config.RequestTimeoutSeconds,
        minimumParticipants=2,maximumParticipants=2,startCommand=_config.DuelCommand,joinCommand=_config.AcceptCommand,choices=Array.Empty<string>(),description="Ausrüstungs-PvP: Die höhere serverseitig berechnete Ausrüstungsstärke gewinnt. Bei Gleichstand wird der Einsatz zurückgegeben."};
    internal async Task<object> ReadRemoteAsync(string user,long version,CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try{
            _byUserId.TryGetValue(user,out var duel);
            _remoteResults.TryGetValue(user,out var last);
            var payout=last?.Paid==true && last.Won?checked(last.Stake*2):0;
            var result=last is null?null:new{outcome=!last.Paid?"cancelled":last.Won?"success":"failure",personalNet=last.Paid?(last.Won?last.Stake:-last.Stake):0,personalPayout=payout,personalLoss=last.Paid&&!last.Won?last.Stake:0,totalPayout=last.Paid?checked(last.Stake*2):0,totalLoss=last.Paid?last.Stake:0,message=last.Message};
            return new{game="duel",name="Duell",eventId=duel?.Id??last?.EventId??"-",version,phase=duel is null?(last is null?"inactive":"result"):"joining",
                joinEndsAt=duel?.ExpiresAt.ToUnixTimeMilliseconds(),participants=Array.Empty<object>(),participantCount=duel is null?0:2,joined=duel is not null,
                personalStake=duel?.Challenger.UserId==user?duel.Stake:0,requiredStake=duel?.Stake??0,result=duel is null?result:null,
                challenge=duel is null?null:new{challenger=duel.Challenger.UserName,target=duel.Target.DisplayName,canAccept=duel.Target.Id==user}};
        }finally{_gate.Release();}
    }
    internal async Task<bool> ExecuteRemoteAsync(ChatMessage actor,string operation,string eventId,string stake,string target,IReadOnlySet<string> allowedUsers,CancellationToken ct)
    {
        var command=RemoteCommand(operation);if(string.IsNullOrEmpty(command))return false;
        _remoteUsers.Value=allowedUsers;
        var capture=new RemoteResult();_remoteAccepted.Value=capture;_remoteExpected.Value=operation=="challenge"?null:eventId;
        actor=new ChatMessage{UserId=actor.UserId,UserName=actor.UserName,UserLogin=actor.UserLogin,Id=actor.Id,Text=operation=="challenge"?$"{command} {target} {stake}":command};
        try{await ProcessAsync(actor,ct);return capture.Accepted;}
        finally{_remoteUsers.Value=null;_remoteExpected.Value=null;_remoteAccepted.Value=null;}
    }
}
