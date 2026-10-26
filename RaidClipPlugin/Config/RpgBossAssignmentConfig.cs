namespace RaidClipPlugin.Config;
public sealed class RpgBossAssignmentConfig
{
 public bool Automatic {get;set;}=true;
 public int TargetPersonalTurns {get;set;}=12;
 public decimal RatingMultiplier {get;set;}=3;
 public decimal SurvivalBudget {get;set;}=.45m;
 public decimal ParticipationRewardFraction {get;set;}=.2m;
 public void Validate(){if(TargetPersonalTurns is <5 or >60||RatingMultiplier is <.1m or >10||SurvivalBudget is <.1m or >.9m||ParticipationRewardFraction is <0 or >.5m)throw new InvalidOperationException("Ungültige automatische Bosszuweisung.");}
}
