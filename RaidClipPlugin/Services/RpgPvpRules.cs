using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public static class RpgPvpRules
{
 public static decimal EquipmentPower(RpgProfile p,RpgConfig c){
  var gear=new RpgProfile{Inventory=p.Inventory,Equipment=p.Equipment};
  var s=CharacterStatService.Calculate(gear,c,RpgActivity.Boss);
  var hit=Math.Max(1,s.Attack+s.Power+s.MagicPower)*(1+Math.Max(-90,s.Damage+s.AttackPercent)/100)*(1+Math.Max(0,s.CritChance)/100*(.5m+Math.Max(0,s.CritDamage)/100));
  hit*=1+Math.Max(-90,s.AttackSpeed+s.DoubleAttackChance+2*s.TripleAttackChance)/100;
  var survival=Math.Max(1,100+s.MaximumHp+s.Stamina*5)*(1+Math.Max(0,s.Armor*(1+s.ArmorPercent/100))/100)/(1-Math.Clamp(s.BossDodgeChance+s.BossDamageReduction,0,95)/100);
  return decimal.Round(Math.Min(1000000000,hit)*Math.Min(1000000000,survival),4);
 }
}
