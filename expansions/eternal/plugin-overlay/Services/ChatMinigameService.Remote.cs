using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// Uses the SAME command handler, configuration, random draws and cooldown dictionaries as chat.
public sealed partial class ChatMinigameService
{
    internal AdventureGameService RemoteAdventures=>_adventures;
    internal string RemoteCommand(string game,string operation)=>game switch {
        "heist" when operation=="join"=>_heist?.JoinCommand??"",
        "race"=>operation=="start"?"!start race":"!joinrace",
        "duel"=>_duel?.RemoteCommand(operation)??"",_=>""};
    internal object RemoteHeistRules(Func<string,string,bool> permitted,string name){
        if(_heist is null)return new{id="heist",name="Otter-Heist",enabled=false,canStart=false,canJoin=false,usesStake=false,minimumStake=0,maximumStake=0,cooldownSeconds=0,joinSeconds=0,minimumParticipants=0,maximumParticipants=0,startCommand="",joinCommand="",choices=Array.Empty<string>(),description="Heist-Dienst nicht konfiguriert."};
        var rule=System.Text.Json.JsonSerializer.SerializeToNode(_heist.RemoteRules)!;rule["enabled"]=_heist.RemoteEnabled&&permitted(_heist.JoinCommand,name);return rule;
    }
    internal object RemoteDuelRules(Func<string,string,bool> permitted,string name){
        if(_duel is null)return new{id="duel",name="Duell",enabled=false,canStart=false,canJoin=false,usesStake=true,minimumStake=0,maximumStake=0,cooldownSeconds=0,joinSeconds=0,minimumParticipants=2,maximumParticipants=2,startCommand="",joinCommand="",choices=Array.Empty<string>(),description="Duell-Dienst nicht konfiguriert."};
        var rule=System.Text.Json.JsonSerializer.SerializeToNode(_duel.RemoteRules)!;
        rule["enabled"]=(bool)rule["enabled"]! && permitted(_duel.RemoteCommand("challenge"),name) && permitted(_duel.RemoteCommand("accept"),name) && permitted(_duel.RemoteCommand("deny"),name);return rule;
    }
    internal object ReadRemoteCommunityRules()=>new {
        heistEnabled=_heist?.RemoteEnabled==true,heistJoin=_heist?.JoinCommand??"",raceEnabled=_config.Adventures.Race.Enabled,
        race=_config.Adventures.Race,duel=_duel?.RemoteRules};
    internal async Task<object> ReadRemoteBossAsync(ViewerPointEntry account,CancellationToken ct){
        var status=await _adventures.ReadRemoteStateAsync(account.UserId,ct);
        var hot=_points.HotTime.Status;
        return new{area=await _adventures.ReadAreaBossAsync(account.UserId,ct),status=status.Status,damage=account.Rpg.Health.BossId==status.Status.BossEventId?account.Rpg.Health.BossDamage:0,
            hotTime=new{active=_config.Adventures.Rpg.HotTime.Enabled&&hot.Active,endsAt=hot.Event?.End.ToUnixTimeMilliseconds()??0}};
    }
    internal Task<BossAttackResult> ExecuteRemoteBossAsync(string user,string name,string request,CancellationToken ct)=>
        _adventures.ExecuteBossAttackAsync(user,name,request,ct,communityVictory:true);
    internal async Task<object[]> ReadRemoteCommunityAsync(string user,long version,CancellationToken ct){
        var adventure=await _adventures.ReadRemoteStateAsync(user,ct);
        var heist=_heist?.Snapshot;var me=heist?.Players.SingleOrDefault(p=>p.UserId==user);
        var active=heist?.State==HeistState.Joining;
        var heistState=new{game="heist",name="Otter-Heist",eventId=heist?.EventId??"-",version,
            phase=active?"joining":heist?.State==HeistState.Evaluating?"running":heist is null?"inactive":"result",
            startedAt=heist?.StartedAt.ToUnixTimeMilliseconds(),joinEndsAt=heist?.JoinEndsAt.ToUnixTimeMilliseconds(),
            participants=Array.Empty<object>(),participantCount=heist?.Players.Count??0,joined=me is not null,personalStake=0,jackpot=heist?.Jackpot??0,
            result=heist is not null && heist.State is HeistState.Successful or HeistState.Failed or HeistState.Cancelled?new {
                outcome=heist.State==HeistState.Successful?"success":heist.State==HeistState.Cancelled?"cancelled":"failure",
                personalNet=me?.Payout??0,personalPayout=me?.Payout??0,personalLoss=0,totalPayout=heist.TotalPayout,totalLoss=0,message="Ergebnis des gemeinsamen Plugin-Heists."}:null};
        var race=adventure.Status;
        var raceState=new{game="race",name="Otter-Rennen",eventId=race.RaceActive?race.RaceEventId:adventure.LastRaceId.Length>0?adventure.LastRaceId:"-",version,phase=race.RaceActive?"joining":adventure.RaceResult is null?"inactive":"result",
            startedAt=race.RaceStartedAt?.ToUnixTimeMilliseconds(),joinEndsAt=race.RaceEndsAt?.ToUnixTimeMilliseconds(),participants=Array.Empty<object>(),participantCount=race.RacePlayers,joined=race.RaceActive?adventure.RaceJoined:adventure.WasJoined,
            personalStake=race.RaceActive?(adventure.RaceJoined?race.RaceStake:0):adventure.LastStake,requiredStake=race.RaceStake,result=race.RaceActive?null:adventure.RaceResult};
        var duel=_duel is null?new{game="duel",name="Duell",eventId="-",version,phase="inactive",participants=Array.Empty<object>(),participantCount=0,joined=false,personalStake=0} as object:await _duel.ReadRemoteAsync(user,version,ct);
        return [heistState,raceState,duel];
    }
    internal sealed record CommunityResult(bool Success,string Message);
    internal async Task<CommunityResult> ExecuteRemoteCommunityAsync(string user,string name,string request,string game,string operation,string eventId,string stake,string target,IReadOnlySet<string> allowedUsers,CancellationToken ct){
        if(!_isRunning || _disposed || !GameplayAllowed)return new(false,"Plugin nicht spielbereit.");
        var command=RemoteCommand(game,operation);
        if(string.IsNullOrEmpty(command))return new(false,"Spieldienst fehlt.");
        var message=new ChatMessage{UserId=user,UserName=name,UserLogin=name,Id=request,Text=command,BroadcasterUserId=_broadcasterId,RoomId=_broadcasterId};
        if(game=="heist")return new(await _heist!.JoinRemoteAsync(message,eventId,ct),"Heist-Beitritt vom Plugin geprüft.");
        if(game=="duel")return new(await _duel!.ExecuteRemoteAsync(message,operation,eventId,stake,target,allowedUsers,ct),"Duell-Auftrag vom Plugin geprüft.");
        if(game=="race"){
            var result=await _adventures.ExecuteRemoteRaceAsync(message,operation,eventId,stake,ct);
            return new(result,"Renn-Anmeldung vom Plugin geprüft. Einsätze werden erst bei der Abrechnung geprüft und gebucht.");
        }
        return new(false,"Unbekanntes Spiel.");
    }

    private sealed class RemoteCasinoCapture
    {
        public CasinoApplyResult? Result; public long Stake; public long Payout; public bool Faulted;
        public List<string> Messages {get;}=[];
    }
    private readonly AsyncLocal<RemoteCasinoCapture?> _remoteCasino=new();
    internal sealed record RemoteCasinoResult(bool Success,string Message,long Stake,long Payout,long Balance,long Jackpot);
    internal async Task<RemoteCasinoResult> ExecuteRemoteCasinoAsync(string user,string displayName,Guid actionId,
        string game,string stake,string choice,CancellationToken ct)
    {
        if(!_isRunning || _disposed || !GameplayAllowed || !_config.Enabled || !_config.PointsEnabled)
            return new(false,"Plugin nicht spielbereit.",0,0,0,0);
        if(game is not ("gamble" or "coinflip" or "slots" or "roulette") ||
            !long.TryParse(stake,out var amount) || amount<0 || amount>long.MaxValue/500)
            return new(false,"Ungültiger Spielauftrag.",0,0,0,0);
        if(game=="coinflip" && choice is not ("kopf" or "zahl") ||
            game=="roulette" && !RouletteRules.TryParseBet(choice,out _))
            return new(false,"Ungültige Spielauswahl.",0,0,0,0);
        var text=game is "coinflip" or "roulette"?$"!{game} {choice} {stake}":$"!{game} {stake}";
        var capture=new RemoteCasinoCapture();_remoteCasino.Value=capture;
        try
        {
            // Deliberately bypass ProcessMessageAsync: no attendance/chat points or chat-heal reward.
            var message=new ChatMessage{Id="extension:"+actionId.ToString("N"),UserId=user,UserLogin=displayName,
                UserName=displayName,Text=text,BroadcasterUserId=_broadcasterId,RoomId=_broadcasterId};
            await HandleCommandAsync(message,ParseMinigameCommand(text),ct);
            if(capture.Faulted)throw new InvalidOperationException("Spielausgang unklar; nicht neu buchen.");
            var result=capture.Result;
            return new(result?.Success==true,string.Join(" ",capture.Messages) is {Length:>0} response?response:
                result?.Error??"Spiel deaktiviert oder Befehl nicht freigegeben.",capture.Stake,capture.Payout,
                result?.Balance??await _points.GetPointsAsync(user,ct),result?.JackpotWon??0);
        }
        finally{_remoteCasino.Value=null;}
    }

    private RpgMutation RecordCasinoCommit(ViewerPointEntry account,ChatMessage message,string game)
    {
        var (command,seconds)=game switch{
            "Coinflip"=>("!coinflip",_config.CoinflipCooldownSeconds),"Slots"=>("!slots",_config.SlotsCooldownSeconds),
            "Roulette"=>("!roulette",_config.RouletteCooldownSeconds),_=>("!gamble",_config.GambleCooldownSeconds)};
        var now=DateTimeOffset.UtcNow;
        var id=string.IsNullOrWhiteSpace(message.Id)?null:"casino:"+message.Id;
        if(id is not null && account.Rpg.CompletedActions.Contains(id))return new(false,"Aktion bereits verarbeitet.");
        if(account.Rpg.Cooldowns.TryGetValue(command,out var until) && until>now)
            return new(false,$"Cooldown: noch {Math.Ceiling((until-now).TotalSeconds)} Sekunden.");
        account.Rpg.Cooldowns[command]=now.AddSeconds(Math.Max(0,seconds));
        if(id is not null)account.Rpg.CompletedActions.Add(id);
        return new(true);
    }
}
