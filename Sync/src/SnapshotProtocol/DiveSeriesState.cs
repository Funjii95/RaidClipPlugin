namespace SnapshotProtocol;
public sealed class DiveSeriesLoot
{
 public string ItemId {get;set;}="";public string Name {get;set;}="";public int Quantity {get;set;}
}
public sealed class DiveSeriesState
{
 public string Id {get;set;}="";public string ZoneId {get;set;}="";public int Count {get;set;}public int Completed {get;set;}
 public string Status {get;set;}="active";public string Reason {get;set;}="";public DateTimeOffset NextAt {get;set;}
 public long Damage {get;set;}public long Pearls {get;set;}public long Experience {get;set;}
 public List<DiveSeriesLoot> Items {get;set;}=[];public Dictionary<string,int> Materials {get;set;}=[];
 public static void Validate(DiveSeriesState s){
  if(!Guid.TryParseExact(s.Id,"N",out _)||!RpgStatistics.ZoneIds.Contains(s.ZoneId)||s.Count is not (10 or 20 or 100)||s.Completed<0||s.Completed>s.Count||s.Status is not ("active" or "completed" or "defeated" or "stopped")||s.Reason.Length>200||s.NextAt==default||s.Damage<0||s.Pearls<0||s.Experience<0||s.Items is null||s.Items.Count>256||s.Items.Any(i=>i is null||i.ItemId.Length is <1 or >100||i.Name.Length>200||i.Quantity<1)||s.Items.Select(i=>i.ItemId).Distinct().Count()!=s.Items.Count||s.Materials is null||s.Materials.Count>30||s.Materials.Any(p=>p.Key.Length is <1 or >80||p.Value<1))throw new InvalidDataException("Invalid dive series.");
 }
}
