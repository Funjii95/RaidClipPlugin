using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed record RpgEnemyResult(string Name,bool Victory,int Rounds,int DamageTaken,int CurrentHp,int MaximumHp,int Experience,RpgItem? Loot)
{
 public RpgItem? ExtraLoot {get;init;} public int Level {get;init;} public string ZoneId {get;init;}=""; public string Source {get;init;}="normal";
 public IReadOnlyDictionary<string,int> Materials {get;init;}=new Dictionary<string,int>();
}
public sealed class RpgEnemyService(IDuelRandom random)
{
 public RpgEnemyResult? Encounter(RpgProfile p,RpgConfig c,long paidStake,DateTimeOffset now,decimal hotTimePercent=0)
 {
  if(!c.Enemies.Enabled||!c.Health.Enabled)return null;
  if(random.NextInclusive(1,10000)>c.Enemies.EncounterChance*100)return null;
  return Fight(p,c,c.Zones.Get(p.SelectedDivingZone),"normal",random.NextInclusive(0,2),now);
 }
 public RpgEnemyResult Fight(RpgProfile p,RpgConfig c,RpgZoneDefinition zone,string source,int enemyIndex,DateTimeOffset now)
 {
  if(source is not ("normal" or "elite" or "mimic" or "endgame")||enemyIndex is <0 or >2)throw new ArgumentException("Ungültiger Gebietskampf.");
  RpgHealthService.Refresh(p,c.Health,now);if(p.Health.CurrentHp<=0)throw new InvalidOperationException("Vor dem Tauchen erst heilen.");
  var elite=source is "elite" or "endgame" or "mimic";
  var hp=zone.Hp*(elite?2:1);var attack=zone.Attack*(elite?1.35m:1);var armor=zone.Armor*(elite?1.25m:1);
  int taken=0,rounds=0;var combatId="zone:"+Guid.NewGuid().ToString("N");
  while(hp>0&&p.Health.CurrentHp>0&&rounds<c.Enemies.MaximumRounds) {
   rounds++;RpgEternalCombat.Refresh(p,c.Health,now);var stats=CharacterStatService.Calculate(p,c,RpgActivity.Dive,now:now);
   var regular=CharacterStatService.BossDamage(random.NextInclusive(10,30),stats,random.NextInclusive(0,9999)/100m,out var critical,c.BaseCritMultiplier);
   regular=RpgTalentService.UnderwaterDamage(regular,p);
   var ignore=critical&&RpgEternalCombat.Has(p,c,"eternal_primordial_wrath",6)?.30m:0m;
   int extra=stats.DoubleAttackChance>0&&random.NextInclusive(0,9999)<stats.DoubleAttackChance*100?1:0;
   var damage=(int)Math.Min(int.MaxValue,decimal.Floor((regular*(1+extra*.5m)+RpgEternalCombat.Proc(regular,critical,extra,p,c,now)+RpgDepthsCombatService.AttackProc(regular,critical,extra,p,c,random,now))*100/(100+armor*(1-ignore))));
   var actual=RpgEternalCombat.Judgment(p,c)?hp:Math.Min(hp,Math.Max(1,damage));hp-=actual;
   var leech=Math.Clamp(stats.BossLifeleech+RpgPerkCatalog.Combined(p,"lifeleech"),0,100);
   RpgSetCombatService.Heal(p,c.Health,(int)(actual*leech/100),now);RpgEternalCombat.AfterHit(p,c,actual,now);
   if(hp>0) {
    var playerArmor=Math.Max(0,stats.Armor*(1+stats.ArmorPercent/100));var dodge=RpgPerkCatalog.Proc(p,"underwaterDodge",random)||(stats.BossDodgeChance>0&&random.NextInclusive(0,9999)<stats.BossDodgeChance*100);
    var raw=dodge?0:(int)decimal.Ceiling(attack*100/(100+playerArmor)*(1-Math.Clamp(stats.BossDamageReduction,-80,95)/100));
    var absorbed=Math.Min(p.Health.SetShieldHp,raw);p.Health.SetShieldHp-=absorbed;raw-=absorbed;
    raw=RpgEternalCombat.Receive(raw,p,c,combatId,now);var received=Math.Min(p.Health.CurrentHp!.Value,raw);p.Health.CurrentHp-=received;taken+=received;
   }
  }
  if(p.Health.CurrentHp==0&&!string.IsNullOrEmpty(p.Health.BossId))p.Health.Defeated=true;
  bool victory=hp==0&&p.Health.CurrentHp>0;RpgItem? loot=null,eternal=null;IReadOnlyDictionary<string,int> materials=new Dictionary<string,int>();var experience=zone.Level*(elite?20:10);
  if(victory) {
   RpgHealthService.Experience(p,c.Health,experience,now);RpgPerkCatalog.Victory(p,now);
   materials=RpgZoneService.Materials(p,c,zone,source=="mimic"?"mimic":"enemy",random);
   loot=RpgZoneService.Equipment(p,c,zone,random,now);if(loot is not null)p.Inventory.Add(loot);
   eternal=RpgEternalLoot.Victory(p,c,zone.Id,zone.Level,source,true,random,now);
   if(RpgEternalCombat.Has(p,c,"divine_infinity",3)&&loot is not null)for(int i=0;i<2;i++){var copy=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(loot))!;copy.InstanceId=Guid.NewGuid().ToString("N");p.Inventory.Add(copy);}
  }
  var name=source=="mimic"?"Mimic der "+zone.Name:zone.Enemies[enemyIndex]+(source=="elite"?" (Elite)":"");
  return new(name,victory,rounds,taken,p.Health.CurrentHp!.Value,RpgHealthService.Maximum(p,c.Health),victory?(int)(experience*(1+RpgSetCombatService.Bonus(p,c.Sets,"ExperienceBonus")/100)):0,loot){ExtraLoot=eternal,Level=zone.Level,ZoneId=zone.Id,Source=source,Materials=materials};
 }
}
