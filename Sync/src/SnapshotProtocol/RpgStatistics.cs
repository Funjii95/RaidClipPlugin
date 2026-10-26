using System.Text.RegularExpressions;
namespace SnapshotProtocol;
public sealed class RpgStatistics
{
 public int Version {get;set;}=1;
 public Dictionary<string,long> Counters {get;set;}=new();
 public Dictionary<string,Dictionary<string,long>> Zones {get;set;}=new();
 public Dictionary<string,long> Fish {get;set;}=new();
 public Dictionary<string,long> BossKills {get;set;}=new();
 public Dictionary<string,long> Dungeons {get;set;}=new();
 public static readonly string[] Keys=["dives","successfulDives","failedDives","enemyKills","lostFights","damage","highestHit","criticalHits","damageTaken","damageBlocked","bossParticipations","bossKills","bossDefeats","bossDamage","highestBossDamage","fishCaught","legendaryFinds","pearlsEarned","salvagedItems","groupDives","groupBossKills","groupHealing","healing"];
 public static readonly string[] ZoneIds=["sunny_lagoon","sunken_temples","shipwreck_graveyard","glowing_coral_forest","tidal_maelstrom","crystal_grotto","cursed_depths","true_abyss","abyss","primordial_throne"];
 public static readonly string[] ZoneKeys=["dives","success","failure","kills","materials","legendary"];
 public const long Maximum=9000000000000000;
 public static void Validate(RpgStatistics value)
 {
  if(value.Version!=1||value.Counters is null||value.Zones is null||value.Fish is null||value.BossKills is null||value.Dungeons is null||value.Zones.Count>10||value.Zones.Any(z=>!ZoneIds.Contains(z.Key)))throw new InvalidDataException("Invalid statistics.");
  Map(value.Counters,Keys.Length,k=>Keys.Contains(k));
  foreach(var zone in value.Zones.Values)Map(zone,ZoneKeys.Length,k=>ZoneKeys.Contains(k));
  Map(value.Fish,128,k=>Regex.IsMatch(k,"^[a-zA-Z0-9_-]{1,80}$"));
  Map(value.BossKills,64,k=>Regex.IsMatch(k,"^[a-zA-Z0-9_-]{1,80}$"));
  Map(value.Dungeons,3,k=>k is "normal" or "heroic" or "mythic");
 }
 private static void Map(Dictionary<string,long>? map,int limit,Func<string,bool> key)
 {if(map is null||map.Count>limit||map.Any(p=>!key(p.Key)||p.Value<0||p.Value>Maximum))throw new InvalidDataException("Invalid statistics counter.");}
}
