namespace RaidClipPlugin.Config;
public sealed class RpgMultiplayerConfig
{
 public bool Enabled {get;set;}=true;
 public int InvitationSeconds {get;set;}=120;
 public int InstanceMinutes {get;set;}=30;
 public decimal GroupHpPerExtraMember {get;set;}=.7m;
 public decimal GroupAttackPerExtraMember {get;set;}=.15m;
 public decimal GroupHealMaxHpPercent {get;set;}=12;
 public decimal ShieldMaxHpPercent {get;set;}=15;
 public int SkillCooldownTurns {get;set;}=3;
 public int MaximumLevelSpreadForRewards {get;set;}=20;
 public void Validate(){if(InvitationSeconds is <30 or >600||InstanceMinutes is <5 or >120||GroupHpPerExtraMember is <.1m or >2||GroupAttackPerExtraMember is <0 or >1||GroupHealMaxHpPercent is <1 or >30||ShieldMaxHpPercent is <1 or >30||SkillCooldownTurns is <1 or >20||MaximumLevelSpreadForRewards is <1 or >40)throw new InvalidOperationException("Ungültige Gruppenregeln.");}
}
