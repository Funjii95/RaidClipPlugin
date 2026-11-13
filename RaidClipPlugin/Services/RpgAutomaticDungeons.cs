using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed record AutomaticPartyMember(string Id,string Name,RpgRole Role,int Hp,int MaximumHp,bool CanHeal);
internal sealed record AutomaticPartyView(PartyState Party,List<AutomaticPartyMember> Members);
public sealed partial class ViewerPointStore
{
 internal async Task<List<AutomaticPartyView>> AutomaticPartiesAsync(RpgConfig config,DateTimeOffset now,CancellationToken ct){
  await _lock.WaitAsync(ct);try{await EnsureLoadedAsync(ct);return _partyWorld.Parties.Values.Where(p=>p.Instance is {Status:"active",AutomaticCombat:true} i&&i.NextAutomaticAt<=now).Select(p=>new AutomaticPartyView(PartyCopy(p),p.Members.Select(id=>{var a=_entries![id];return new AutomaticPartyMember(id,a.DisplayName,a.Rpg.Role,a.Rpg.Health.CurrentHp??0,RpgHealthService.Maximum(a.Rpg,config.Health),RpgGroupHealing.CanHeal(a.Rpg,config));}).ToList())).ToList();}finally{_lock.Release();}
 }
}
internal sealed class RpgAutomaticDungeons(ViewerPointStore points,Func<MinigameConfig> configuration,IDuelRandom? random=null,Func<DateTimeOffset>? clock=null)
{
 internal async Task<int> TickAsync(CancellationToken ct){
  var now=clock?.Invoke()??DateTimeOffset.UtcNow;var config=configuration();if(!config.Enabled||!config.PointsEnabled||!config.Adventures.Rpg.Enabled||!config.Adventures.Rpg.Multiplayer.Enabled)return 0;
  var changed=0;foreach(var battle in await points.AutomaticPartiesAsync(config.Adventures.Rpg,now,ct)){
   var party=battle.Party;var i=party.Instance!;var alive=battle.Members.Where(m=>m.Hp>0).OrderBy(m=>m.Id).ToArray();var actor=alive.Length>0?alive[i.Turn%alive.Length]:battle.Members.First(m=>m.Id==party.LeaderId);var op="attack";var target="";
   bool Ready(AutomaticPartyMember member,string skill)=>i.Cooldowns.GetValueOrDefault(member.Id+":"+skill)<=i.Turn;
   if(i.WaveCleared){var rest=battle.Members.FirstOrDefault(m=>i.Cooldowns.GetValueOrDefault(m.Id+":camp")<=i.Stage);if(rest is not null){actor=rest;op="recover";}else{actor=battle.Members.First(m=>m.Id==party.LeaderId);op="advance";}}
   else if(i.PendingAbility.Length>0&&alive.FirstOrDefault(m=>Ready(m,"interrupt")) is {} interrupt){actor=interrupt;op="interrupt";}
   else if(alive.Any(m=>(decimal)m.Hp/m.MaximumHp<.7m)&&alive.FirstOrDefault(m=>m.CanHeal&&Ready(m,"heal")) is {} healer){actor=healer;op="heal";target="auto";}
   else if(alive.FirstOrDefault(m=>m.Role==RpgRole.Tank&&Ready(m,"guard")&&i.Shields.GetValueOrDefault(m.Id)<m.MaximumHp*.05m) is {} tank){actor=tank;op="guard";target="auto";}
   else if(i.RallyUntilTurn<=i.Turn&&actor.Role==RpgRole.Damage&&Ready(actor,"rally"))op="rally";
   var result=await new RpgPartyCombatService(points,config,random,()=>now).ExecuteAsync(actor.Id,actor.Name,"automatic:"+i.Id+":"+party.Revision,op,"","","",target,ct,i.Id,party.Revision);if(result.Success)changed++;
  }return changed;
 }
}
