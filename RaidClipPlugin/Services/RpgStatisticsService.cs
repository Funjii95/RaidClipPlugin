using SnapshotProtocol;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
/// <summary>Record only inside the same idempotent gameplay transaction as its reward. No historical inference.</summary>
public static class RpgStatisticsService
{
 private static void Increment(Dictionary<string,long> map,string key,long count){if(count>0)map[key]=Math.Min(RpgStatistics.Maximum,map.GetValueOrDefault(key)+Math.Min(count,RpgStatistics.Maximum));}
 public static void Add(RpgProfile p,string key,long count=1){if(!RpgStatistics.Keys.Contains(key))throw new ArgumentException("Unknown statistic.");p.Statistics??=new();Increment(p.Statistics.Counters,key,count);}
 public static void Maximum(RpgProfile p,string key,long count){if(!RpgStatistics.Keys.Contains(key))throw new ArgumentException("Unknown statistic.");p.Statistics??=new();p.Statistics.Counters[key]=Math.Min(RpgStatistics.Maximum,Math.Max(p.Statistics.Counters.GetValueOrDefault(key),count));}
 public static void Zone(RpgProfile p,string id,string key,long count=1){if(!RpgStatistics.ZoneIds.Contains(id)||!RpgStatistics.ZoneKeys.Contains(key))throw new ArgumentException("Unknown region statistic.");p.Statistics??=new();if(!p.Statistics.Zones.TryGetValue(id,out var map))p.Statistics.Zones[id]=map=new();Increment(map,key,count);}
 public static void Fish(RpgProfile p,string id,int count){Add(p,"fishCaught",count);Increment(p.Statistics.Fish,id,count);}
 public static void BossKill(RpgProfile p,string id){Add(p,"bossKills");Increment(p.Statistics.BossKills,id,1);}
 public static void Hit(RpgProfile p,int damage,bool critical){Add(p,"damage",damage);Maximum(p,"highestHit",damage);if(critical&&damage>0)Add(p,"criticalHits");}
}
