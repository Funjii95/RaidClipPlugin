namespace RaidClipPlugin.Config;
public sealed class RpgLagoonBalanceConfig
{
 public int ExperienceMultiplier {get;set;}=3;
 public decimal EquipmentChanceBonus {get;set;}=15;
 public decimal MaterialChanceBonus {get;set;}=10;
 public decimal EarlyLegendaryChanceBonus {get;set;}=5;
 public void Validate(){if(ExperienceMultiplier is <1 or >100||new[]{EquipmentChanceBonus,MaterialChanceBonus,EarlyLegendaryChanceBonus}.Any(v=>v is <0 or >100))throw new InvalidOperationException("Ungültige Sonnenlagunen-Boni.");}
}
