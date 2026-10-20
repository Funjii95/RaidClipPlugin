using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class HeistService
{
    internal bool RemoteEnabled=>_config.Enabled;
    internal string JoinCommand=>_config.JoinCommand;
    internal object RemoteRules=>new{id="heist",name=_config.Overlay.Title,enabled=_config.Enabled,canStart=false,canJoin=true,usesStake=false,
        minimumStake=0,maximumStake=0,cooldownSeconds=_config.UserCooldownMinutes*60,joinSeconds=_config.JoinDurationSeconds,
        minimumParticipants=_config.MinimumParticipants,maximumParticipants=_config.MaximumParticipants,startCommand="",joinCommand=_config.JoinCommand,
        choices=Array.Empty<string>(),description="Bestehender gemeinsamer Heist; Start über Chat oder Plugin durch Mods/Broadcaster."};
    internal async Task<bool> JoinRemoteAsync(ChatMessage actor,string eventId,CancellationToken ct)
    {
        var before=Snapshot;
        if(before is null || before.EventId!=eventId || before.Players.Any(p=>p.UserId==actor.UserId))return false;
        await JoinAsync(actor,ct,eventId);
        var after=Snapshot;
        return after?.EventId==eventId && after.Players.Any(p=>p.UserId==actor.UserId);
    }
}
