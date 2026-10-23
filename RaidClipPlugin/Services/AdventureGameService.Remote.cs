using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class AdventureGameService
{
    internal sealed record RemoteRaceResolution(string EventId,string[] Users,int Winner,long Stake,bool Success,string Message);
    private RemoteRaceResolution? _lastRemoteRace;
    internal sealed record RemoteAdventureState(AdventureStatus Status,bool RaceJoined,object? RaceResult=null,string LastRaceId="",bool WasJoined=false,long LastStake=0);
    internal async Task<RemoteAdventureState> ReadRemoteStateAsync(string user,CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try{
            await RecoverAreaBossAsync(ct);PublishStatus();var last=_lastRemoteRace;var joined=last?.Users.Contains(user)==true;
            var won=joined && last!.Success && last.Users[last.Winner]==user;
            var payout=won?checked(last!.Stake*last.Users.Length):0;
            var loss=joined && last!.Success && !won?last.Stake:0;
            var result=last is null?null:new{outcome=!last.Success?"cancelled":won?"success":"failure",personalNet=joined&&last.Success?payout-last.Stake:0,personalPayout=payout,personalLoss=loss,totalPayout=last.Success?checked(last.Stake*last.Users.Length):0,totalLoss=last.Success?checked(last.Stake*(last.Users.Length-1)):0,message=last.Message};
            return new(Status,_race?.Players.ContainsKey(user)==true,result,last?.EventId??"",joined,joined?last!.Stake:0);
        }
        finally{_gate.Release();}
    }
    internal async Task<bool> ExecuteRemoteRaceAsync(ChatMessage actor,string operation,string eventId,string stake,CancellationToken ct)
    {
        if(operation is not ("start" or "join"))return false;
        var before=await ReadRemoteStateAsync(actor.UserId,ct);
        if(before.RaceJoined || (operation=="start" && before.Status.RaceActive) || (operation=="join" && !before.Status.RaceActive))return false;
        var command=operation=="start"?"!startrace":"!joinrace";
        var parts=operation=="start"?new[]{command,stake}:new[]{command};
        await HandleAsync(actor,command,parts,ct,expectedRace:eventId);
        return (await ReadRemoteStateAsync(actor.UserId,ct)).RaceJoined;
    }
}
