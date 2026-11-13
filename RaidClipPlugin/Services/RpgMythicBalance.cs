using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;

/// <summary>Minimum base values: repeatable for catalog and owned items, never rerolls or reduces a value.</summary>
public static class RpgMythicBalance
{
 public static bool Apply(RpgItem item)
 {
  if(item.Rarity!=ItemRarity.Mythic || item.Kind!=RpgItemKind.Equipment || item.Slot is null)return false;
  var changed=false; var a=item.Attributes;
  decimal Floor(decimal value,decimal minimum){if(value>=minimum)return value;changed=true;return minimum;}
  var damage=item.SetId==RpgMythicRoleCatalog.DamageSet;
  a.BossDamage=Floor(a.BossDamage,damage?50:40);
  if(RpgMythicRoleCatalog.IsSet(item.SetId)){
   var weapon=item.Slot==EquipmentSlot.Weapon;
   a.Attack=Floor(a.Attack,weapon?(damage?3200:2400):(damage?260:140));
   a.AttackPercent=Floor(a.AttackPercent,damage?5:4);
   a.Damage=Floor(a.Damage,damage?20:15);
   a.Power=Floor(a.Power,damage?30:24);
   a.Stamina=Floor(a.Stamina,30);
   if(damage){a.CritChance=Floor(a.CritChance,8);a.CritDamage=Floor(a.CritDamage,20);}
  }
  return changed;
 }
}
