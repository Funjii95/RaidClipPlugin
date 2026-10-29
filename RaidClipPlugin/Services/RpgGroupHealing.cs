using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal static class RpgGroupHealing
{
 internal static bool Has(RpgProfile p,RpgConfig c,string id,int n)=>RpgSetCombatService.Active(p,c.Sets,id,n);
 internal static bool CanHeal(RpgProfile p,RpgConfig c)=>p.Role==RpgRole.Healer||Has(p,c,"coral_oracle",4)||Has(p,c,"eternal_tide_blessing",2)||Has(p,c,"true_ancient_current",4);
 internal static int Heal(string actor,string target,int raw,PartyInstance instance,Func<string,ViewerPointEntry> get,RpgConfig c,IDuelRandom rng,DateTimeOffset now,bool canCrit=true,bool applyPower=true)
 {
  var sender=get(actor).Rpg;var recipient=get(target).Rpg;if(sender.Health.CurrentHp is null or <=0||recipient.Health.CurrentHp is null or <=0||raw<=0)return 0;
  var stats=CharacterStatService.Calculate(sender,c,RpgActivity.Boss,now:now);var critical=canCrit&&rng.NextInclusive(0,9999)<(sender.Role==RpgRole.Healer?c.Roles.HealerCriticalHealing:0)*100;
  var amount=(int)Math.Min(int.MaxValue,decimal.Floor(raw*(applyPower?1+Math.Clamp(stats.HealingPower,-90,200)/100:1)*(critical?1.5m:1)*(1+RpgTankCombat.ReceivedHealing(recipient,c.Health,now)/100)));
  var max=RpgHealthService.Maximum(recipient,c.Health);var granted=Math.Min(max-recipient.Health.CurrentHp.Value,amount);recipient.Health.CurrentHp+=granted;
  if(granted>0){RpgStatisticsService.Add(sender,"healing",granted);RpgStatisticsService.Add(sender,"groupHealing",granted);RpgEternalCombat.Healed(recipient,c.Health,granted,now);RpgPartyCombatService.AddThreat(instance,actor,RpgRoleService.Threat(sender,c,0,granted));RpgPartyCombatService.Log(instance,actor,target,critical?"critical-heal":"heal",granted);}
  if(amount>granted&&(Has(sender,c,"coral_oracle",6)||Has(sender,c,"eternal_tide_blessing",6))){var cap=(int)(max*c.Multiplayer.ShieldMaxHpPercent/100);instance.Shields[target]=Math.Min(cap,instance.Shields.GetValueOrDefault(target)+amount-granted);}
  return granted;
 }
 internal static string? Lowest(PartyState party,Func<string,ViewerPointEntry> get,RpgConfig c,string? except=null)=>party.Members.Where(id=>id!=except&&get(id).Rpg.Health.CurrentHp>0).OrderBy(id=>(decimal)get(id).Rpg.Health.CurrentHp!.Value/RpgHealthService.Maximum(get(id).Rpg,c.Health)).FirstOrDefault();
 internal static void AfterTurn(string actor,long ownHealing,bool critical,PartyState party,Func<string,ViewerPointEntry> get,RpgConfig c,IDuelRandom rng,DateTimeOffset now)
 {
  var p=get(actor).Rpg;var i=party.Instance!;if(p.Health.CurrentHp<=0)return;var target=Lowest(party,get,c,actor);if(target is null)return;
  var key="transfer:"+actor;if(i.Cooldowns.GetValueOrDefault(key)<=i.Turn){
   var fraction=Has(p,c,"eternal_tide_blessing",2)?.15m:Has(p,c,"coral_oracle",4)?.1m:0m;
   var amount=(int)Math.Min(int.MaxValue,ownHealing*fraction);
   if(critical&&Has(p,c,"true_ancient_current",4))amount+=(int)(RpgHealthService.Maximum(p,c.Health)*.05m);
   if(amount>0){Heal(actor,target,amount,i,get,c,rng,now,false,false);i.Cooldowns[key]=i.Turn+3;}
  }
  if(Has(p,c,"moon_tide_jewelry",2)&&i.Cooldowns.GetValueOrDefault("moon:"+actor)<=i.Turn){var max=RpgHealthService.Maximum(get(target).Rpg,c.Health);var shield=(int)(max*.1m);i.Shields[target]=Math.Max(i.Shields.GetValueOrDefault(target),shield);i.Cooldowns["moon:"+actor]=i.Turn+4;RpgPartyCombatService.Log(i,actor,target,"moon-shield",shield);RpgPartyCombatService.AddThreat(i,actor,shield*.5m);}
 }
 internal static void Describe(IEnumerable<RpgItemSet> sets)
 {
  foreach(var set in sets){var text=set.Id switch{"coral_oracle"=>" Gruppe: ab 4 Teilen 10 % tatsächlicher Eigenheilung auf einen lebenden Verbündeten; aktive Heilung trifft einen zweiten; ab 6 Teilen kann Überheilung schützen.","moon_tide_jewelry"=>" Gruppe: alle vier Kampfaktionen ein Mondschutz für den verletztesten lebenden Verbündeten.","true_ancient_current"=>" Gruppe: ab 4 Teilen heilen kritische Treffer einen lebenden Verbündeten um 5 % eigene Max-HP, höchstens alle drei Kampfaktionen.","eternal_tide_blessing"=>" Gruppe: ab 2 Teilen 15 % tatsächliche Eigenheilung übertragen; ab 4 Teilen aktive Heilung auf ein zweites Ziel; ab 6 Teilen Heilwelle und Überheilungsschutz.",_=>""};if(text.Length>0&&set.Bonuses.Count>0&&!set.Bonuses[^1].Description.Contains(" Gruppe:"))set.Bonuses[^1].Description+=text;}
 }
}
