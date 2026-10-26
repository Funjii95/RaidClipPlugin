using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed record RpgCombatPower(decimal Rating,decimal ExpectedHit,decimal EffectiveHp);
public static class RpgCombatPowerService
{
 public static RpgCombatPower Evaluate(RpgProfile p,RpgConfig c,DateTimeOffset now)
 {
  var s=CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now);
  var crit=1+Math.Clamp(s.CritChance,0,100)/100*Math.Clamp(c.BaseCritMultiplier+s.CritDamage/100-1,0,20);
  var hit=(20+Math.Clamp(s.Attack,0,1000000))*(1+Math.Clamp(s.AttackPercent,0,1000)/100)*(1+Math.Clamp(s.Damage,0,1000)/100)*(1+Math.Clamp(s.BossDamage,0,1000)/100)*crit;
  hit*=1+Math.Clamp(s.DoubleAttackChance,0,100)/200+Math.Clamp(s.TripleAttackChance,0,5)/100;
  if(RpgEarlyLegendaryEffects.Has(p,"crab_breaker"))hit*=1+c.EarlyLegendaries.CrabChance/100*c.EarlyLegendaries.CrabStrength;
  var weapon=p.Inventory.SingleOrDefault(i=>p.Equipment.GetValueOrDefault(EquipmentSlot.Weapon)==i.InstanceId);
  if(weapon is not null&&RpgWeaponCatalog.Find(weapon.ItemId) is {} spell)hit+=Math.Clamp(s.MagicPower,0,1000000)*2*Math.Min(1,5*(1+Math.Clamp(s.ManaRegeneration,0,500)/100)*6/spell.ManaCost);
  var interval=(decimal)RpgSetCombatService.AttackSeconds(p,c,6,now);var criticalRate=Math.Clamp(s.CritChance,0,100)/100;var extraRate=Math.Clamp(s.DoubleAttackChance+s.TripleAttackChance,0,100)/100;
  bool Has(string id,int n)=>RpgSetCombatService.Active(p,c.Sets,id,n);
  decimal proc=0;
  if(Has("tide_king_regalia",4))proc+=1.6m*Math.Min(criticalRate,interval/8);
  if(Has("abyss_hunter",4))proc+=1.7m*Math.Min(extraRate,interval/6);
  if(Has("true_ancient_current",6))proc+=2*Math.Min(1,interval/20);
  if(Has("abyss_devourer",4))proc+=2.5m*Math.Min(criticalRate*.12m,interval/8);
  if(Has("abyss_sunken_world",4))proc+=1.2m*Math.Min(criticalRate*.15m,interval/6);
  if(Has("eternal_primordial_wrath",4))proc+=1.5m*extraRate;
  if(Has("eternal_primordial_wrath",6))proc+=4.5m*Math.Min(1,interval/6);
  if(Has("eternal_cosmic_forge",3))proc+=1.8m*Math.Min(criticalRate,interval/5);
  if(Has("eternal_cosmic_forge",6))proc+=1.5m*extraRate;
  if(Has("eternal_cosmic_forge",7))proc+=5*Math.Min(.1m,interval/10);
  if(Has("eternal_cosmic_forge",8))proc+=8*Math.Min(1,interval/8);
  if(Has("eternal_soul_devourer",7))proc+=4*Math.Min(1,interval/7);
  hit=Math.Clamp(hit*(1+proc),1,1000000000);
  var hp=RpgHealthService.Maximum(p,c.Health)*(1+Math.Clamp(s.Armor*(1+s.ArmorPercent/100),0,10000)/100)/(1-Math.Clamp(s.BossDamageReduction,0,95)/100)/(1-Math.Clamp(s.BossDodgeChance,0,90)/100);
  if(p.Role==RpgRole.Healer)hp*=1+c.Roles.HealerHealingPercent/100;
  // Neither one enormous armor/HP roll nor one damage roll may determine the whole tier.
  var balancedHp=hp*(hit*100)/(hp+hit*100);var balancedHit=hit*(hp/5)/(hit+hp/5);
  var score=(decimal)Math.Sqrt((double)(balancedHp*balancedHit))*(1+Math.Clamp(s.AttackSpeed,0,200)/300)+Math.Clamp(p.PlayerLevel,1,100)*2;
  return new(Math.Min(score,1000000000),hit,Math.Min(hp,1000000000));
 }
 public static RpgAreaBossDefinition Choose(RpgProfile p,RpgConfig c,IEnumerable<RpgAreaBossDefinition> choices,DateTimeOffset now)
 {
  var available=choices.Where(b=>b.Enabled).OrderBy(b=>b.Level).ToArray();if(available.Length==0)throw new InvalidOperationException("Keine Gebietsbosse verfügbar.");
  if(RpgEternalCombat.Judgment(p,c))return available[^1];var power=Evaluate(p,c,now);
  var balanced=available.LastOrDefault(b=>(decimal)Math.Sqrt((double)((decimal)b.BaseHp*b.Attack*b.AttackFactor*b.Hits))<=power.Rating*c.BossAssignment.RatingMultiplier)??available[0];
  // Defense must not keep a high-damage build on a boss it can immediately defeat.
  // Counterattack scaling supplies the survival budget for the higher assignment.
  var targetHp=power.ExpectedHit*c.BossAssignment.TargetPersonalTurns;
  return available.FirstOrDefault(b=>b.Level>=balanced.Level&&b.BaseHp>=targetHp)??available[^1];
 }
 public static int PersonalHp(RpgProfile p,RpgConfig c,RpgAreaBossDefinition boss,DateTimeOffset now)=>Math.Clamp((int)Math.Min(int.MaxValue,decimal.Ceiling(Evaluate(p,c,now).ExpectedHit*c.BossAssignment.TargetPersonalTurns)),Math.Min(100,boss.BaseHp),int.MaxValue);
 public static decimal AttackScale(RpgProfile p,RpgConfig c,RpgAreaBossDefinition boss,DateTimeOffset now)
 {
  var s=CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now);var incoming=boss.Attack*boss.AttackFactor*boss.Hits*100/(100+Math.Clamp(s.Armor*(1+s.ArmorPercent/100),0,10000))*(1-Math.Clamp(s.BossDamageReduction,0,80)/100);
  return Math.Clamp((decimal)RpgHealthService.Maximum(p,c.Health)/c.BossAssignment.TargetPersonalTurns*c.BossAssignment.SurvivalBudget/Math.Max(1,incoming),.01m,1);
 }
}
