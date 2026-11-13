namespace RaidClipPlugin.Config;
public sealed class RpgRoleConfig
{
 public decimal TankArmorPercent {get;set;}=20;
 public decimal TankReductionPercent {get;set;}=15;
 public decimal TankThreatMultiplier {get;set;}=3;
 public decimal HealerHealingPercent {get;set;}=20;
 public decimal HealerCriticalHealing {get;set;}=5;
 public decimal HealerDamagePercent {get;set;}=5;
 public decimal DamagePercent {get;set;}=15;
 public decimal HealingThreatMultiplier {get;set;}=.5m;
 public void Validate(){if(TankArmorPercent is <0 or >100||TankReductionPercent is <0 or >50||TankThreatMultiplier is <1 or >10||HealerHealingPercent is <0 or >100||HealerCriticalHealing is <0 or >100||HealerDamagePercent is <0 or >100||DamagePercent is <0 or >100||HealingThreatMultiplier is <0 or >2)throw new InvalidOperationException("Ungültige Rollenregeln.");}
}
