using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class AdventureGameService
{
    // Called under the same gate as normal boss attacks and configuration changes.
    private async Task<bool> HandleAdministrationAsync(ChatMessage actor,string command,string[] parts,CancellationToken ct)
    {
        if(command is not ("!heal" or "!kill"))return false;
        if(!EventStartCommand.IsAllowed(actor,_broadcasterId)){
            await Say("Nur Mods/Broadcaster dürfen !heal und !kill boss verwenden.",ct);return true;
        }
        if(command=="!kill"){
            if(parts.Length!=2 || !parts[1].Equals("boss",StringComparison.OrdinalIgnoreCase)){
                await Say("Nutzung: !kill boss.",ct);return true;
            }
            if(!_config.Adventures.Boss.Enabled || _boss is null){
                await Say("Aktuell ist kein Boss aktiv.",ct);return true;
            }
            if(_boss.Players.Count==0){
                await Say("Der Boss hat noch keine Teilnehmer. Erst mit !attack teilnehmen.",ct);return true;
            }
            _boss.Hp=0;
            await FinishBossVictoryAsync(_boss,true,ct);
            return true;
        }
        var rpg=_config.Adventures.Rpg;
        if(!rpg.Enabled || !rpg.Health.Enabled){await Say("RPG-Lebenspunkte sind deaktiviert.",ct);return true;}
        if(parts.Length!=2 || string.IsNullOrWhiteSpace(parts[1].TrimStart('@'))){
            await Say("Nutzung: !heal <@Spieler|all>.",ct);return true;
        }
        var all=parts[1].Equals("all",StringComparison.OrdinalIgnoreCase);
        var name=parts[1].TrimStart('@');
        var profiles=await _points.GetTopAsync(int.MaxValue,ct);
        var targets=all?profiles.ToArray():profiles.Where(p=>p.DisplayName.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(targets.Length==0 || (!all && targets.Length!=1)){
            await Say("Kein eindeutiger gespeicherter Spieler mit diesem Namen gefunden.",ct);return true;
        }
        var request="admin-heal:"+(string.IsNullOrWhiteSpace(actor.Id)?Guid.NewGuid().ToString("N"):actor.Id);
        var healed=0;
        foreach(var target in targets){
            var result=await _points.ApplyRpgAsync(target.UserId,target.DisplayName,request,a=>{
                RpgHealthService.Refresh(a.Rpg,rpg.Health,_now());
                a.Rpg.Health.CurrentHp=RpgHealthService.Maximum(a.Rpg,rpg.Health);
                // A defeated participant remains ineligible for the same boss, as with ordinary healing.
                return new(true);
            },ct);
            if(result.Success)healed++;
        }
        if(healed>0)DataChanged?.Invoke();
        await Say(all?$"❤️ {healed} gespeicherte Spieler vollständig geheilt.":healed>0?$"❤️ @{targets[0].DisplayName} vollständig geheilt.":"Heilung wurde bereits verarbeitet.",ct);
        return true;
    }
}

