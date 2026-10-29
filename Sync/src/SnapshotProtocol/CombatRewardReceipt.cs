namespace SnapshotProtocol;
public sealed class CombatRewardItem
{
 public string InstanceId {get;set;}="";public string ItemId {get;set;}="";public string Name {get;set;}="";public int Rarity {get;set;}
}
public sealed class CombatRewardReceipt
{
 public string Id {get;set;}="";public string Kind {get;set;}="boss";public string EnemyId {get;set;}="";public string EnemyName {get;set;}="";public string ZoneId {get;set;}="";public DateTimeOffset AwardedAt {get;set;}
 public long Pearls {get;set;}public long Experience {get;set;}public List<CombatRewardItem> Items {get;set;}=[];public Dictionary<string,int> Materials {get;set;}=[];
 public static void Validate(List<CombatRewardReceipt> receipts){
  if(receipts is null||receipts.Count>10||receipts.Any(r=>r is null)||receipts.Select(r=>r.Id).Distinct().Count()!=receipts.Count)throw new InvalidDataException("Invalid combat rewards.");
  foreach(var r in receipts)if(r.Id.Length is <1 or >120||r.Kind is not ("boss" or "mimic" or "dungeon" or "groupBoss" or "groupDive" or "participation")||r.EnemyId.Length>80||r.EnemyName.Length>120||!RpgStatistics.ZoneIds.Contains(r.ZoneId)||r.AwardedAt==default||r.Pearls<0||r.Experience<0||r.Items is null||r.Items.Count>8||r.Items.Any(i=>i is null||i.InstanceId.Length is <1 or >100||i.ItemId.Length is <1 or >100||i.Name.Length>200||i.Rarity is <0 or >10)||r.Materials is null||r.Materials.Count>20||r.Materials.Any(m=>m.Key.Length is <1 or >80||m.Value<1))throw new InvalidDataException("Invalid combat reward receipt.");
 }
}
