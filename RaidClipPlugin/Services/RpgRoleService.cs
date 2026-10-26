using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public static class RpgRoleService
{
 public static void Apply(RpgProfile p,RpgConfig c,RpgAttributes result)
 {
  if(p.Role==RpgRole.Tank){result.ArmorPercent+=c.Roles.TankArmorPercent;result.BossDamageReduction+=c.Roles.TankReductionPercent;}
  if(p.Role==RpgRole.Healer){result.HealingPower+=c.Roles.HealerHealingPercent;result.CriticalHealing+=c.Roles.HealerCriticalHealing;result.Damage+=c.Roles.HealerDamagePercent;}
  if(p.Role==RpgRole.Damage)result.Damage+=c.Roles.DamagePercent;
 }
 public static decimal Threat(RpgProfile p,RpgConfig c,int damage,int effectiveHealing)=>(Math.Max(0,damage)+Math.Max(0,effectiveHealing)*c.Roles.HealingThreatMultiplier)*(p.Role==RpgRole.Tank?c.Roles.TankThreatMultiplier:1);
}
public sealed partial class ViewerPointStore
{
 internal Task<RpgTransactionResult> SelectRoleAsync(string user,string name,string request,RpgRole role,RpgConfig config,CancellationToken ct)=>ApplyRpgAsync(user,name,request,a=>{
  if(!config.Enabled||role==RpgRole.Unselected||!Enum.IsDefined(role))return new(false,"Ungültige Rollenwahl.");
  if(_globalBoss is {Active:true} boss&&boss.Participants.ContainsKey(user))return new(false,"Rolle erst nach dem laufenden Bossereignis wechseln.");
  if(_partyWorld.For(user)?.Instance is {Status:"active"} instance&&instance.ExpiresAt>DateTimeOffset.UtcNow)return new(false,"Rolle erst nach der laufenden Gruppen- oder Dungeoninstanz wechseln.");
  a.Rpg.Role=role;return new(true);
 },ct);
}
