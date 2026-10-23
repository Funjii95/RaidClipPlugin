using RaidClipPlugin.Config;
namespace RaidClipPlugin.Models;
public sealed class RpgGlobalBossParticipant
{
 public string UserId {get;set;}="";public string Name {get;set;}="";public string BossId {get;set;}="boss_korallus";
 public long Damage {get;set;} public decimal Contribution {get;set;} public int Cycles {get;set;} public bool Defeated {get;set;}
 public int CurseIntervals {get;set;}
 public int PersonalShield {get;set;}
}
public sealed class RpgGlobalBossState
{
 public string Id {get;set;}="";public bool Active {get;set;} public bool Victory {get;set;} public bool RewardsSettled {get;set;}
 public decimal Remaining {get;set;}=1;public DateTimeOffset End {get;set;}
 public long Revision {get;set;}
 public BossAdventureConfig EventConfig {get;set;}=new();public List<RpgAreaBossDefinition> Bosses {get;set;}=[];
 public Dictionary<string,RpgGlobalBossParticipant> Participants {get;set;}=[];
 public List<string> Log {get;set;}=[];
}
