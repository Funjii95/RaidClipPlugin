using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public sealed record RpgWeaponSpell(int Damage,int Healing=0,int Shield=0,int CounterReduction=0);
// Only called on detached authoritative transaction drafts. No spell recursively invokes an attack.
public static class RpgWeaponService
{
 public const decimal MaximumMana=100;
 private static RpgItem? Equipped(RpgProfile p)=>p.Equipment.TryGetValue(EquipmentSlot.Weapon,out var id)?p.Inventory.FirstOrDefault(i=>i.InstanceId==id&&i.Slot==EquipmentSlot.Weapon):null;
 private static int Bounded(decimal n)=>(int)Math.Clamp(decimal.Floor(n),0,int.MaxValue);
 public static void RefreshMana(RpgProfile p,DateTimeOffset now)
 {
  var h=p.Health;
  if(h.CurrentMana is null){h.CurrentMana=MaximumMana;h.LastManaRegen=now;}
  h.CurrentMana=Math.Clamp(h.CurrentMana.Value,0,MaximumMana);
  h.LastManaRegen??=now;
  if(now>h.LastManaRegen){var seconds=Math.Min(120,(decimal)(now-h.LastManaRegen.Value).TotalSeconds);var regen=Math.Clamp(RpgSetCombatService.Gear(p,"ManaRegeneration"),0,1000);h.CurrentMana=Math.Min(MaximumMana,decimal.Round(h.CurrentMana.Value+seconds*5*(1+regen/100),4));h.LastManaRegen=now;}
  var weapon=Equipped(p);
  if(h.ManaShieldExpires is null||now>=h.ManaShieldExpires||weapon?.InstanceId!=h.ManaShieldSource){h.ManaShieldHp=0;h.ManaShieldSource="";h.ManaShieldExpires=null;}
 }
 public static decimal ElementMultiplier(RpgProfile p,RpgAttributes a)
 {
  var weapon=Equipped(p);var element=weapon is null?null:RpgWeaponCatalog.Find(weapon.ItemId)?.Element;
  if(element is null)return 1;
  var bonus=element switch{"Water"=>a.WaterDamage,"Light"=>a.LightDamage,"Abyss"=>a.AbyssDamage,_=>0};
  return 1+Math.Clamp(a.ElementalDamage+bonus,0,10000)/100;
 }
 public static int ElementDamage(int damage,RpgProfile p,RpgAttributes a)=>Bounded(damage*ElementMultiplier(p,a));
 public static decimal DamageBonus(RpgProfile p,DateTimeOffset now){var w=Equipped(p);return w?.ItemId=="weapon_abyss_claw"&&p.Cooldowns.TryGetValue("weapon:rage:"+w.InstanceId,out var end)&&now<end?20:0;}
 public static RpgWeaponSpell? Cast(RpgProfile p,RpgConfig c,RpgAttributes a,int attack,DateTimeOffset now)
 {
  RefreshMana(p,now);var item=Equipped(p);var w=item is null?null:RpgWeaponCatalog.Find(item.ItemId);
  if(w is null||p.Health.CurrentHp is null or <=0)return null;
  var key="weapon:cast:"+item!.InstanceId;
  if(p.Cooldowns.TryGetValue(key,out var end)&&now<end||p.Health.CurrentMana<w.ManaCost)return null;
  p.Health.CurrentMana-=w.ManaCost;p.Cooldowns[key]=now.AddSeconds(w.Cooldown);
  foreach(var old in p.Cooldowns.Where(k=>k.Key.StartsWith("weapon:")&&k.Value<=now).Select(k=>k.Key).ToArray())p.Cooldowns.Remove(old);
  var magic=a.MagicPower*(1+Math.Clamp(a.Damage,0,10000)/100)*(1+Math.Clamp(a.BossDamage,0,10000)/100)*ElementMultiplier(p,a);
  var physical=attack*ElementMultiplier(p,a);var max=RpgHealthService.Maximum(p,c.Health);
  if(w.Id=="weapon_abyss_claw")p.Cooldowns["weapon:rage:"+item.InstanceId]=now.AddSeconds(8);
  return w.Id switch{
   "weapon_tide_crown"=>new(Bounded(physical*1.5m),CounterReduction:50),
   "weapon_leviathan_call"=>new(Bounded(magic*2),CounterReduction:30),
   "weapon_sunken_kings"=>new(Bounded(physical)),
   "weapon_eternal_coral"=>new(Bounded(physical*1.5m),Bounded(max*.12m)),
   "weapon_origin_current"=>new(Bounded(magic*1.6m),Bounded(a.MagicPower)),
   "weapon_reef_wardens"=>new(Bounded(physical*.5m),Shield:Bounded(max*.2m),CounterReduction:50),
   "weapon_abyss_claw"=>new(Bounded(physical*.75m)),
   "weapon_despair_staff"=>new(Bounded(magic*2),Bounded(a.MagicPower*.5m),CounterReduction:50),
   "weapon_void_devourer"=>new(Bounded(physical*2),Bounded(max*.1m)),
   "weapon_star_tide"=>new(Bounded(physical*2.5m),CounterReduction:50),
   "weapon_infinite_tides"=>new(Bounded(magic*3),CounterReduction:75),
   "weapon_worldbreaker"=>new(Bounded(physical*3),Shield:Bounded(max*.15m)),_=>null};
 }
 public static void AfterHit(RpgProfile p,RpgConfig c,RpgWeaponSpell? spell,int actualDamage,DateTimeOffset now)
 {
  if(spell is null||actualDamage<=0||p.Health.CurrentHp is null or <=0)return;
  if(spell.Healing>0)RpgSetCombatService.Heal(p,c.Health,spell.Healing,now);
  if(spell.Shield>0){var w=Equipped(p);if(w is not null){p.Health.ManaShieldHp=Math.Max(p.Health.ManaShieldHp,spell.Shield);p.Health.ManaShieldSource=w.InstanceId;p.Health.ManaShieldExpires=now.AddSeconds(15);}}
 }
 public static int Counter(int damage,RpgProfile p,RpgWeaponSpell? spell,DateTimeOffset now)
 {
  RefreshMana(p,now);var reduced=Bounded(damage*(1-Math.Clamp(spell?.CounterReduction??0,0,100)/100m));
  var absorbed=Math.Min(reduced,p.Health.ManaShieldHp);p.Health.ManaShieldHp-=absorbed;return reduced-absorbed;
 }
}
