namespace SnapshotProtocol;
public sealed class QuestProgress
{
 public bool TutorialStarted {get;set;}
 public Dictionary<string,int> Counters {get;set;}=[];
 public HashSet<string> Claimed {get;set;}=[];
}
public static class QuestProgressRules
{
 public static readonly string[] Ids=["tutorial","lagoon","temple","wreck","coral","tides","crystal","curse","origin","abyss","throne"];
 public static readonly string[] Requirements=["dive","equip","upgrade","fish","fish2","craft","eat","kills","buffedDive"];
 public static void Validate(QuestProgress state)
 {
  if(state.Counters is null||state.Claimed is null||state.Counters.Count>99||state.Claimed.Count>11||state.Claimed.Any(id=>!Ids.Contains(id))||state.Counters.Any(p=>p.Value is <0 or >100||!Ids.Any(id=>Requirements.Any(key=>p.Key==id+":"+key))))throw new InvalidDataException("Invalid quest progress.");
 }
}
