using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
public sealed partial class ViewerPointStore
{
 internal async Task<List<(string Id,string Name,DiveSeriesState Series)>> DueDiveSeriesAsync(DateTimeOffset now,CancellationToken ct){
  await _lock.WaitAsync(ct);try{await EnsureLoadedAsync(ct);return _entries!.Values.Where(a=>a.Rpg.DiveSeries is {Status:"active"} s&&s.NextAt<=now).Select(a=>(a.UserId,a.DisplayName,System.Text.Json.JsonSerializer.Deserialize<DiveSeriesState>(System.Text.Json.JsonSerializer.Serialize(a.Rpg.DiveSeries))!)).ToList();}finally{_lock.Release();}
 }
}
internal sealed class RpgDiveSeriesService(ViewerPointStore points,MinigameConfig config,IDuelRandom? random=null,Func<DateTimeOffset>? clock=null)
{
 private DateTimeOffset Now=>clock?.Invoke()??DateTimeOffset.UtcNow;
 internal Task<RpgTransactionResult> StartAsync(string user,string name,string request,int count,CancellationToken ct){
  if(count is not (10 or 20 or 100))throw new ArgumentException("Ungültige Anzahl.");
  var now=Now;return points.ApplyRpgAsync(user,name,request,a=>{
   if(!config.Enabled||!config.PointsEnabled||!config.Adventures.Rpg.Enabled||!config.Adventures.Treasure.Enabled)return new(false,"Tauchen ist deaktiviert.");
   if(a.Rpg.DiveSeries is {Status:"active"})return new(false,"Eine Tauchserie läuft bereits.");
   if(points.HasActivePartyCombatUnsafe(user,now))return new(false,"Beende zuerst den Gruppenkampf.");
   RpgHealthService.Refresh(a.Rpg,config.Adventures.Rpg.Health,now);if(a.Rpg.Health.CurrentHp<=0)return new(false,"Du hast 0 HP. Erst heilen, dann tauchen.");
   a.Rpg.DiveSeries=new(){Id=Guid.NewGuid().ToString("N"),ZoneId=a.Rpg.SelectedDivingZone,Count=count,NextAt=a.Rpg.Cooldowns.GetValueOrDefault("!dive",now)};return new(true);
  },ct);
 }
 internal Task<RpgTransactionResult> StopAsync(string user,string name,string request,CancellationToken ct)=>points.ApplyRpgAsync(user,name,request,a=>{if(a.Rpg.DiveSeries is not {Status:"active"} s)return new(false,"Keine laufende Tauchserie.");s.Status="stopped";s.Reason="Manuell beendet";return new(true);},ct);
 internal async Task<int> TickAsync(CancellationToken ct,Func<string,string,CancellationToken,Task>? discovered=null){
  if(!config.Enabled||!config.PointsEnabled||!config.Adventures.Rpg.Enabled||!config.Adventures.Treasure.Enabled)return 0;
  var now=Now;var changed=0;
  foreach(var entry in await points.DueDiveSeriesAsync(now,ct)){
   var s=entry.Series;
   if(config.PointsBlacklist.Any(n=>n.Equals(entry.Name,StringComparison.OrdinalIgnoreCase))){await StopAsync(entry.Id,entry.Name,"series-blocked:"+s.Id,ct);continue;}
   var result=await new RpgAdventureService(points,config,random,()=>now).ExecuteAsync(entry.Id,entry.Name,"series:"+s.Id+":"+s.Completed,RpgActivity.Dive,0,ct,s.Id,s.Completed);
   if(result.Success){changed++;if(result.BossDiscovered&&discovered is not null)await discovered(entry.Name,result.ZoneId,ct);}
   else if(result.RemainingCooldown==0)await points.ApplyRpgAsync(entry.Id,entry.Name,"series-end:"+s.Id+":"+s.Completed,a=>{if(a.Rpg.DiveSeries is {Status:"active"} current&&current.Id==s.Id&&current.Completed==s.Completed){current.Status=a.Rpg.Health.CurrentHp<=0?"defeated":"stopped";current.Reason=result.Error.Length>200?result.Error[..200]:result.Error;}return new(true);},ct);
  }return changed;
 }
 internal static void Record(ViewerPointEntry account,int damage,long pearls,long experience,IEnumerable<RpgItem> items,IReadOnlyDictionary<string,int> materials){
  var s=account.Rpg.DiveSeries!;s.Completed++;s.Damage=checked(s.Damage+damage);s.Pearls=checked(s.Pearls+pearls);s.Experience=checked(s.Experience+experience);
  foreach(var item in items){var owned=s.Items.FirstOrDefault(i=>i.ItemId==item.ItemId);if(owned is not null)owned.Quantity++;else s.Items.Add(new(){ItemId=item.ItemId,Name=item.DisplayName,Quantity=1});}
  foreach(var m in materials)s.Materials[m.Key]=checked(s.Materials.GetValueOrDefault(m.Key)+m.Value);
  s.NextAt=account.Rpg.Cooldowns["!dive"];
  if(account.Rpg.Health.CurrentHp<=0){s.Status="defeated";s.Reason="Bei 0 HP beendet";}else if(s.Completed>=s.Count){s.Status="completed";s.Reason="Alle Tauchgänge abgeschlossen";}
  DiveSeriesState.Validate(s);
 }
}
