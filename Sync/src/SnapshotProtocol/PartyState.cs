using System.Text.RegularExpressions;
namespace SnapshotProtocol;
public sealed class PartyInvitation
{
 public string Id {get;set;}="";public string PartyId {get;set;}="";public string InviterId {get;set;}="";public string TargetId {get;set;}="";public DateTimeOffset ExpiresAt {get;set;}
}
public sealed class PartyCombatLog
{
 public int Turn {get;set;} public string ActorId {get;set;}="";public string TargetId {get;set;}="";public string Action {get;set;}="";public int Amount {get;set;}
}
public sealed class PartyInstance
{
 public bool AutomaticCombat {get;set;}public DateTimeOffset NextAutomaticAt {get;set;}
 public string Id {get;set;}="";public string Kind {get;set;}="dive";public string ZoneId {get;set;}="sunny_lagoon";public string Difficulty {get;set;}="normal";public string Status {get;set;}="active";
 public int AddsHp {get;set;}public int PendingTurn {get;set;}public string PendingAbility {get;set;}="";public bool WaveCleared {get;set;}public bool Enraged {get;set;}public string LastActorId {get;set;}="";public int RallyUntilTurn {get;set;}
 public Dictionary<string,decimal> DamageScale {get;set;}=[];
 public string DungeonId {get;set;}="";public int Stage {get;set;} public int Turn {get;set;}public string EnemyId {get;set;}="";public string EnemyName {get;set;}="";public int MaximumHp {get;set;}public int Hp {get;set;}public int Attack {get;set;}public int Armor {get;set;}
 public DateTimeOffset ExpiresAt {get;set;}public List<string> Rewarded {get;set;}=[];
 public Dictionary<string,decimal> Threat {get;set;}=[];public Dictionary<string,int> Shields {get;set;}=[];public Dictionary<string,int> Cooldowns {get;set;}=[];public List<PartyCombatLog> Log {get;set;}=[];
}
public sealed class PartyState
{
 public string Id {get;set;}="";public string LeaderId {get;set;}="";public List<string> Members {get;set;}=[];public DateTimeOffset CreatedAt {get;set;}public long Revision {get;set;}public PartyInstance? Instance {get;set;}
}
public sealed class PartySnapshot
{
 public int Version {get;set;}=1;public PartyState? Party {get;set;}public List<PartyInvitation> Invitations {get;set;}=[];
 public static bool User(string id)=>Regex.IsMatch(id,"^[0-9]{1,25}$");
 public static bool Id(string id)=>Regex.IsMatch(id,"^[a-f0-9]{32}$");
 public static void Validate(PartySnapshot snapshot,string owner)
 {
  if(snapshot.Version!=1||snapshot.Invitations is null||snapshot.Invitations.Count>16)throw new InvalidDataException("Invalid party snapshot.");
  foreach(var invite in snapshot.Invitations)if(!Id(invite.Id)||!Id(invite.PartyId)||!User(invite.InviterId)||invite.TargetId!=owner||invite.ExpiresAt==default)throw new InvalidDataException("Invalid party invitation.");
  if(snapshot.Party is not {} party)return;
  if(!Id(party.Id)||party.Members is null||party.Members.Count is <1 or >4||party.Members.Distinct().Count()!=party.Members.Count||party.Members.Any(id=>!User(id))||!party.Members.Contains(owner)||!party.Members.Contains(party.LeaderId)||party.Revision<0)throw new InvalidDataException("Invalid party membership.");
  if(party.Instance is not {} i)return;
  if(!Id(i.Id)||i.Kind is not ("dive" or "boss" or "dungeon")||i.Difficulty is not ("normal" or "heroic" or "mythic")||i.Status is not ("active" or "won" or "lost" or "aborted" or "expired")||!RpgStatistics.ZoneIds.Contains(i.ZoneId)||i.MaximumHp<1||i.Hp<0||i.Hp>i.MaximumHp||i.Attack<0||i.Armor<0||i.Turn<0||i.Stage is <0 or >30||i.EnemyId.Length>80||i.EnemyName.Length>120||i.Rewarded is null||i.Rewarded.Count>4||i.Rewarded.Any(id=>!party.Members.Contains(id))||i.Threat is null||i.Shields is null||i.Cooldowns is null||i.Log is null||i.Log.Count>60)throw new InvalidDataException("Invalid party instance.");
  if(i.AddsHp<0||i.PendingTurn<0||i.PendingAbility.Length>30||i.LastActorId.Length>25||i.RallyUntilTurn<0||i.DungeonId.Length>80||i.DamageScale is null||i.DamageScale.Count>4||i.DamageScale.Any(p=>!party.Members.Contains(p.Key)||p.Value is <=0 or >1))throw new InvalidDataException("Invalid encounter state.");
  if(i.Threat.Count>4||i.Threat.Any(t=>!party.Members.Contains(t.Key)||t.Value is <0 or >9000000000000000)||i.Shields.Count>4||i.Shields.Any(t=>!party.Members.Contains(t.Key)||t.Value<0)||i.Cooldowns.Count>64||i.Cooldowns.Any(t=>t.Key.Length>80||t.Value<0)||i.Log.Any(l=>l.Turn<0||l.Amount<0||l.ActorId.Length>80||l.TargetId.Length>80||l.Action.Length>80))throw new InvalidDataException("Invalid party effects.");
 }
}
