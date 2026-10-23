using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class ChatMinigameService
{
    internal async Task<CommunityResult> ExecuteAdminEventAsync(string user,string request,string kind,long stake,CancellationToken ct)
    {
        if(user!=_broadcasterId)return new(false,"Nur der Kanalinhaber darf Events starten.");
        if(!_isRunning||_disposed||!GameplayAllowed)return new(false,"Spieldienst ist nicht verbunden.");
        var message=new ChatMessage{Id=request,UserId=user,UserName=user,UserLogin=user,IsBroadcaster=true,BroadcasterUserId=_broadcasterId,RoomId=_broadcasterId};
        if(kind=="hotTime")
        {
            var started=_points.HotTime.StartAdministrative(request,_config.Adventures.Rpg.HotTime);
            return new(started,started?"Hot-Time gestartet.":"Hot-Time ist deaktiviert oder läuft bereits.");
        }
        if(kind=="heist")
        {
            if(_heist is null)return new(false,"Heist-Dienst ist nicht eingerichtet.");
            var before=_heist.Snapshot?.EventId;await _heist.StartAsync(message,ct);
            var started=_heist.Snapshot?.State==HeistState.Joining&&_heist.Snapshot.EventId!=before;
            return new(started,started?"Heist gestartet.":"Heist konnte nach den aktuellen Spielregeln nicht gestartet werden.");
        }
        if(kind is not ("boss" or "mimic" or "race"))return new(false,"Unbekanntes Event.");
        var old=_adventures.Status;
        var command=kind=="race"?"!startrace":kind=="mimic"?"!start mimic":"!start boss";
        var parts=kind=="race"?new[]{command,stake.ToString(System.Globalization.CultureInfo.InvariantCulture)}:new[]{command};
        await _adventures.HandleAsync(message,command,parts,ct);
        var current=_adventures.Status;
        var success=kind=="race"?current.RaceActive&&current.RaceEventId!=old.RaceEventId:current.BossActive&&current.BossEventId!=old.BossEventId;
        return new(success,success?"Event gestartet.":"Event konnte nicht gestartet werden. Aktivierung, laufende Runde, Einsatz und Rundenpause im Plugin prüfen.");
    }
}
