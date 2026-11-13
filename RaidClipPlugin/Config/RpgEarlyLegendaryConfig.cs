namespace RaidClipPlugin.Config;
public sealed class RpgEarlyLegendaryConfig
{
 public bool Enabled {get;set;}=true;
 public decimal DropChance {get;set;}=1;
 public decimal CrabChance {get;set;}=15;
 public decimal CrabStrength {get;set;}=.5m;
 public decimal HealingChance {get;set;}=12;
 public decimal HealingMaxHpPercent {get;set;}=5;
 public decimal PearlChance {get;set;}=10;
 public int PearlAmount {get;set;}=10;
 public decimal RescueMaxHpPercent {get;set;}=20;
 public decimal RegenerationPerMinute {get;set;}=1;
 public decimal LuckyRingBonus {get;set;}=5;
 public void Validate(){if(new[]{DropChance,CrabChance,HealingChance,HealingMaxHpPercent,PearlChance,RescueMaxHpPercent,LuckyRingBonus}.Any(n=>n is <0 or >100)||CrabStrength is <=0 or >1||PearlAmount is <0 or >1000||RegenerationPerMinute is <0 or >100)throw new InvalidOperationException("Ungültige legendäre Anfängerregeln.");}
}
