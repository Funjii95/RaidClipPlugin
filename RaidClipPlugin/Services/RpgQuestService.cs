using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
public sealed class RpgQuestService(ViewerPointStore points,MinigameConfig config,string channel="42")
{
 public static int Chapter(RpgProfile p,QuestDefinition q){for(var n=0;n<q.Chapters.Count;n++)if(q.Chapters[n].Requirements.Any(r=>p.Quests.Counters.GetValueOrDefault(q.Id+":"+r.Id)<r.Target))return n;return q.Chapters.Count;}
 public static void Record(RpgProfile p,string action,string zone="",int quantity=1,string fish="",RpgRecipe? recipe=null){
  if(quantity<1)return;
  p.Quests??=new();
  foreach(var q in RpgQuestCatalog.All){
   if(p.PlayerLevel<q.Level||p.Quests.Claimed.Contains(q.Id)||q.Id=="tutorial"&&!p.Quests.TutorialStarted)continue;
   var chapter=Chapter(p,q);if(chapter==q.Chapters.Count)continue;
   foreach(var requirement in q.Chapters[chapter].Requirements){
    if(requirement.Event!=action||(action is "dive" or "fish" or "kills" or "buffedDive")&&zone!=q.ZoneId||requirement.FishId.Length>0&&requirement.FishId!=fish||requirement.RecipeFishId.Length>0&&(recipe is null||!RpgSushiService.Ingredients(recipe).ContainsKey(requirement.RecipeFishId)))continue;
    var key=q.Id+":"+requirement.Id;p.Quests.Counters[key]=Math.Min(requirement.Target,p.Quests.Counters.GetValueOrDefault(key)+quantity);
   }
  }
 }
 public Task<RpgTransactionResult> StartAsync(string user,string name,string action,string id,CancellationToken ct)=>points.ApplyRpgAsync(user,name,action,a=>{
  if(!config.Enabled||!config.PointsEnabled||!config.Adventures.Rpg.Enabled)return new(false,"RPG ist deaktiviert.");
  if(id!="tutorial")return new(false,"Gebietsquests werden beim Erreichen ihres Levels automatisch freigeschaltet.");
  a.DisplayName=name;a.UpdatedAt=DateTimeOffset.UtcNow;var p=a.Rpg;p.Quests??=new();if(p.Quests.TutorialStarted)return new(false,"Die Rundführung läuft bereits.");
  var c=config.Adventures.Rpg;c.Validate();
  var cost=RpgEquipmentUpgradeRules.Cost(0,ItemRarity.Common,c.Upgrades);
  var budget=checked(cost.Pearls+(c.Recipes.Min(r=>r.PearlCost)));
  var support=Math.Max(0,budget-a.Points);
  if(config.MaximumAccountEnabled&&support>config.MaximumAccountPoints-a.Points)return new(false,"Kontolimit reicht nicht für die Tutorial-Starthilfe.");
  a.Points=checked(a.Points+support);foreach(var material in cost.Materials)RpgUpgradeCatalog.Add(p.Materials,material.Key,material.Value);
  if(!p.Inventory.Any(i=>i.Slot.HasValue&&i.Rarity!=ItemRarity.Divine)){
   var definition=c.DiveLoot.Items.FirstOrDefault(i=>i.Rarity==ItemRarity.Common&&i.Slot==EquipmentSlot.Weapon);
   if(definition is null)return new(false,"Keine Anfängerwaffe konfiguriert.");
   var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=DateTimeOffset.UtcNow;RpgItemGenerationService.Generate(item,c.DiveLoot,new CryptoDuelRandom());p.Inventory.Add(item);
  }
  p.Quests.TutorialStarted=true;
  if(p.Equipment.Values.Any(id=>p.Inventory.Any(i=>i.InstanceId==id)))p.Quests.Counters["tutorial:equip"]=1;
  if(p.Inventory.Any(i=>i.Slot.HasValue&&i.UpgradeLevel>0))p.Quests.Counters["tutorial:upgrade"]=1;
  p.Quests.Counters["tutorial:fish"]=Math.Min(3,c.Zones.Get("sunny_lagoon").Fish.Sum(id=>p.Fish.GetValueOrDefault(id)?.Quantity??0));
  if(p.Sushi.Values.Any(n=>n>0))p.Quests.Counters["tutorial:craft"]=1;
  if(p.Buffs.Any(b=>RpgSushiService.Active(b,DateTimeOffset.UtcNow)))p.Quests.Counters["tutorial:eat"]=1;
  if(!Fits(a,user))return new(false,"Inventar zu groß. Bitte zuerst Platz schaffen; Starthilfe wurde nicht gebucht.");
  return new(true,"Rundführung gestartet. Anfängerwaffe bei Bedarf sowie Aufwertungsmaterialien und nötige Startperlen wurden bereitgestellt.");
 },ct);
 public Task<RpgTransactionResult> ClaimAsync(string user,string name,string action,string id,CancellationToken ct)=>points.ApplyRpgAsync(user,name,action,a=>{
  if(!config.Enabled||!config.PointsEnabled||!config.Adventures.Rpg.Enabled)return new(false,"RPG ist deaktiviert.");
  var q=RpgQuestCatalog.All.SingleOrDefault(q=>q.Id==id);if(q is null||a.Rpg.PlayerLevel<q.Level)return new(false,"Diese Quest ist noch nicht freigeschaltet.");
  var p=a.Rpg;if(p.Quests.Claimed.Contains(id))return new(false,"Belohnung bereits abgeholt.");
  if(id=="tutorial"&&!p.Quests.TutorialStarted||Chapter(p,q)!=q.Chapters.Count)return new(false,"Noch nicht alle Questschritte abgeschlossen.");
  if(id=="tutorial"){
   if(a.Points>long.MaxValue-RpgQuestCatalog.TutorialReward||config.MaximumAccountEnabled&&config.MaximumAccountPoints-a.Points<RpgQuestCatalog.TutorialReward)return new(false,"Nicht genug Platz unter dem Perlenlimit. Die Belohnung bleibt abholbar.");
   a.Points+=RpgQuestCatalog.TutorialReward;
  }else{
   var definitions=config.Adventures.Rpg.DiveLoot.Items.Where(i=>i.SetId==q.SetId).ToArray();
   if(definitions.Length!=8||definitions.Select(i=>i.ItemId).Distinct().Count()!=8||Enumerable.Range(0,8).Any(n=>!definitions.Any(i=>i.ItemId==q.SetId+"_"+n)))return new(false,"Das Belohnungsset ist nicht vollständig konfiguriert.");
   foreach(var definition in definitions){
    var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=DateTimeOffset.UtcNow;item.IsLocked=true;p.Inventory.Add(item);
   }
   if(p.Inventory.Count(i=>i.SetId==q.SetId)<8)return new(false,"Das Belohnungsset ist nicht vollständig konfiguriert.");
  }
  p.Quests.Claimed.Add(id);
  if(!Fits(a,user))return new(false,"Inventar zu groß. Bitte zuerst Platz schaffen; die Questbelohnung bleibt erhalten.");
  return new(true,id=="tutorial"?"Tutorial abgeschlossen: 100.000 Perlen erhalten.":"Questreihe abgeschlossen: vollständiges legendäres Achtteil-Set erhalten und gegen Zerlegen gesperrt.");
 },ct);
 private bool Fits(ViewerPointEntry account,string user){try{SnapshotValidation.ValidateLocalSource(new(1,channel,user,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),channel);return true;}catch(InvalidDataException){return false;}}
 private static object Stats(RpgAttributes a)=>typeof(RpgAttributes).GetProperties().Where(p=>(decimal)p.GetValue(a)! !=0).ToDictionary(p=>p.Name,p=>(decimal)p.GetValue(a)!);
 public static object View(RpgProfile p,RpgConfig c)=>new{quests=RpgQuestCatalog.All.Select(q=>new{q.Id,q.Name,q.Level,q.ZoneId,q.SetId,started=q.Id!="tutorial"||p.Quests.TutorialStarted,unlocked=p.PlayerLevel>=q.Level,claimed=p.Quests.Claimed.Contains(q.Id),chapter=Chapter(p,q),chapters=q.Chapters.Select(ch=>new{name=ch.Name,requirements=ch.Requirements.Select(r=>new{id=r.Id,text=r.Text,target=r.Target,current=p.Quests.Counters.GetValueOrDefault(q.Id+":"+r.Id)})}),rewardPearls=q.Id=="tutorial"?RpgQuestCatalog.TutorialReward:0,rewardItems=q.SetId.Length==0?Array.Empty<object>():c.DiveLoot.Items.Where(i=>i.SetId==q.SetId).Select(i=>(object)new{id=i.ItemId,name=i.DisplayName,slot=i.Slot!.Value.ToString(),rarity=i.Rarity.ToString(),attributes=Stats(i.Attributes)}).ToArray()}),rewardSets=c.Sets.Where(s=>s.Id.StartsWith("quest_")).Select(s=>new{s.Id,s.Name,s.Description,s.ItemIds,Bonuses=s.Bonuses.Select(b=>new{b.RequiredPieces,b.Description,Attributes=Stats(b.Attributes)})}).ToArray()};
}
