using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
/// <summary>One regional face per discovered shared event. Rewards use the same regional pool as its area boss.</summary>
public static class RpgRegionalMimics
{
 public const decimal EquipmentChance=50;
 public static IReadOnlyList<RpgAreaBossDefinition> Catalog(RpgConfig config)=>RpgAreaBossConfig.Defaults().Select((boss,n)=>{
  string[] names=["Schnappmuschel der Sonnenlagune","Runenlade der versunkenen Tempel","Käpt'ns bissige Beutekiste","Korallenmaul des Leuchtwaldes","Strudelschlund der Gezeiten","Kristallrachen der Grotten","Fluchlade der verlorenen Seelen","Urkorallen-Truhe des Abgrunds","Leerenmaul des Abyss","Königsmimic der Urtiefe"];
  string[] abilities=["Schnappbiss: ein schwerer Angriff der goldenen Muschelkiefer","Runenbiss: leuchtende Runen verstärken den Kieferschlag","Ankerzähne: rostige Eisenkiefer schlagen zweimal zu","Korallendornen: ein kräftiger Biss aus lebenden Korallen","Gezeitenkiefer: zwei rasche Bisse aus dem Strudel","Splittermaul: scharfkantige Kristalle durchdringen 20 % Rüstung","Fluchbiss: ein schwerer Schlag verfluchter Kiefer","Ursprungsbiss: durchdringt 20 % Schadensreduktion","Leerenrachen: zwei Bisse aus der schwarzen Tiefe","Königsbiss: zwei gewaltige Schläge der uralten Schatzkammer"];
  boss.Name="Mimic – "+names[n];boss.BaseHp=checked(boss.BaseHp*2);boss.Attack=(int)decimal.Ceiling(boss.Attack*1.5m);boss.AttackFactor=1;boss.Hits=n is 2 or 4 or 8 or 9?2:1;boss.ArmorIgnore=n==5?20:0;boss.ReductionIgnore=n==7?20:0;boss.Ability=abilities[n];boss.ItemChance=EquipmentChance;boss.Experience*=2;boss.Pearls*=2;boss.Materials=config.Zones.Get(boss.AreaId).Materials;return boss;
 }).ToArray();
 public static RpgAreaBossDefinition ForZone(RpgConfig config,string zone)=>Catalog(config).Single(m=>m.AreaId==config.Zones.Get(zone).Id);
 public static RpgItem[] Pool(RpgConfig config,RpgAreaBossDefinition mimic)=>RpgLootLocations.BossPool(config,mimic);
 public static RpgItem? Roll(RpgConfig config,RpgAreaBossDefinition mimic,IDuelRandom rng,DateTimeOffset now){
  // Exactly one 50% equipment roll, independent of Hot Time, Luck and the general +5% combat bonus.
  if(rng.NextInclusive(0,9999)>=5000)return null;
  var pool=Pool(config,mimic);if(pool.Length==0)return null;var roll=rng.NextInclusive(1,pool.Sum(i=>i.DropWeight));var definition=pool[^1];foreach(var candidate in pool){roll-=candidate.DropWeight;if(roll<=0){definition=candidate;break;}}
  var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=now;RpgItemGenerationService.Generate(item,config.DiveLoot,rng);return item;
 }
}
