using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed class RpgCombatRewardService(ViewerPointEntry account)
{
 private readonly long pearls=account.Points,experience=account.Rpg.Experience;
 private readonly HashSet<string> items=account.Rpg.Inventory.Select(i=>i.InstanceId).ToHashSet();
 private readonly Dictionary<string,int> materials=RpgUpgradeCatalog.Materials.ToDictionary(m=>m.Id,m=>RpgUpgradeCatalog.Count(account.Rpg.Materials,m.Id));
 internal void Complete(string id,string kind,string enemyId,string enemyName,string zone,DateTimeOffset now){
  var p=account.Rpg;if(p.CombatRewards.Any(r=>r.Id==id))return;
  p.CombatRewards.Add(new(){Id=id,Kind=kind,EnemyId=enemyId,EnemyName=enemyName,ZoneId=zone,AwardedAt=now,Pearls=Math.Max(0,account.Points-pearls),Experience=Math.Max(0,p.Experience-experience),Items=p.Inventory.Where(i=>!items.Contains(i.InstanceId)).Take(8).Select(i=>new CombatRewardItem{InstanceId=i.InstanceId,ItemId=i.ItemId,Name=i.DisplayName,Rarity=(int)i.Rarity}).ToList(),Materials=RpgUpgradeCatalog.Materials.Select(m=>(m.Id,Count:RpgUpgradeCatalog.Count(p.Materials,m.Id)-materials[m.Id])).Where(m=>m.Count>0).ToDictionary(m=>m.Id,m=>m.Count)});
  if(p.CombatRewards.Count>10)p.CombatRewards.RemoveRange(0,p.CombatRewards.Count-10);CombatRewardReceipt.Validate(p.CombatRewards);
 }
}
