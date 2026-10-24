using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed record RpgLootLocation(string ZoneId,string Kind,string? BossId=null);
public sealed record RpgStatRange(string Attribute,decimal Min,decimal Max,bool Optional=false);
public sealed record RpgLocatedItem(string Id,string Name,string Rarity,string SetId,IReadOnlyList<RpgStatRange> Ranges,IReadOnlyList<string> Effects,IReadOnlyList<RpgLootLocation> Sources);
public static class RpgLootLocations
{
 public static object Wire(RpgConfig c)=>Build(c).Select(i=>new object[]{i.Id,i.Name,i.Rarity,i.SetId,i.Ranges.Select(r=>new object[]{r.Attribute,r.Min,r.Max,r.Optional}),i.Effects,i.Sources.Select(s=>new object?[]{s.ZoneId,s.Kind,s.BossId})}).ToArray();
 public static IReadOnlyList<RpgLocatedItem> Build(RpgConfig c)
 {
  var eternal=RpgEternalCatalog.Items().Concat(RpgWeaponCatalog.Items()).Where(i=>i.Rarity==ItemRarity.Eternal).ToArray();
  var eight=RpgEternalEightCatalog.Sets().SelectMany(s=>s.ItemIds).ToHashSet();
  return c.DiveLoot.Items.Concat(eternal).DistinctBy(i=>i.ItemId).Where(i=>i.Rarity!=ItemRarity.Divine).Select(item=>{
   var locations=new List<RpgLootLocation>();
   foreach(var z in c.Zones.Areas){
    var fights=z.EnemyChance>0||z.MimicChance>0;
    if(item.Slot is null&&item.Kind!=RpgItemKind.Consumable&&item.Rarity<=ItemRarity.Legendary&&z.ChestChance>0)locations.Add(new(z.Id,"chest"));
    if(!fights||item.Slot is null)continue;
    if(item.Rarity<=ItemRarity.Legendary&&item.Rarity<=z.MaximumRarity&&c.BossLootChance>0&&c.DiveLoot.MaximumDropChance>0&&item.DropWeight>0&&item.DropActivities.Contains(RpgActivity.Boss)&&c.DiveLoot.RarityWeights.GetValueOrDefault(item.Rarity)>0)locations.Add(new(z.Id,"enemy"));
    if((item.Rarity==ItemRarity.Cursed&&z.Level>=70||item.Rarity==ItemRarity.True&&z.Level>=80||item.Rarity==ItemRarity.Abyss&&z.Level>=90)&&c.Upgrades.EquipmentDropChances.GetValueOrDefault(item.Rarity.ToString())>0)locations.Add(new(z.Id,"enemy"));
    if(z.Id==RpgEternalLoot.Throne&&z.Level==100&&item.Rarity==ItemRarity.Eternal){
     var possible=eight.Contains(item.ItemId)?c.Zones.EternalEightChances.Any(p=>p.Key!="eonath"&&p.Value>0):eternal.Any(i=>i.ItemId==item.ItemId)&&c.Zones.EternalChances.Any(p=>p.Value>0);
     if(possible)locations.Add(new(z.Id,"eternal"));
    }
   }
   foreach(var boss in c.AreaBosses.Bosses.Where(b=>b.Enabled&&b.ItemChance>0))if(BossPool(c,boss).Any(i=>i.ItemId==item.ItemId))locations.Add(new(boss.AreaId,"boss",boss.Id));
   if(eight.Contains(item.ItemId)&&c.AreaBosses.Bosses.Any(b=>b.Id=="boss_eonath"&&b.Enabled)&&c.Zones.EternalEightChances.GetValueOrDefault("eonath")>0)locations.Add(new("primordial_throne","boss","boss_eonath"));
   var effects=item.Effects.Select(e=>$"{e.Name}: {e.Chance} % Chance · {e.CooldownSeconds} s Abklingzeit").Where(s=>!string.IsNullOrWhiteSpace(s)).ToList();
   if(item.Generation is {} rules&&rules.GenerateEffect){foreach(var effect in c.DiveLoot.Effects.Where(e=>rules.EffectIds.Contains(e.Id))){effects.Add(effect.Trigger==RpgEffectTrigger.DoubleAttack?$"Doppelangriff: {rules.DoubleAttackMinimumChance}–{rules.DoubleAttackMaximumChance} % Chance":$"{effect.Name}: {effect.Chance} % Chance · {effect.CooldownSeconds} s Abklingzeit");}}
   return new RpgLocatedItem(item.ItemId,item.DisplayName,item.Rarity.ToString(),item.SetId,StatRanges(item),effects,locations);
  }).ToArray();
 }
 public static RpgItem[] BossPool(RpgConfig c,RpgAreaBossDefinition boss)=>c.DiveLoot.Items.Where(i=>i.Rarity!=ItemRarity.Divine&&i.Rarity.ToString()==boss.Rarity&&i.Slot.HasValue&&i.DropWeight>0)
  .Where(i=>i.Slot!=EquipmentSlot.Weapon||boss.Level>=80||boss.Id=="boss_vortexion"&&i.ItemId=="weapon_tide_crown"||boss.Id=="boss_krysalith"&&i.ItemId=="weapon_leviathan_call")
  .Where(i=>boss.Rarity!="Eternal"||!new[]{"eternal_cosmic_forge","eternal_soul_devourer","eternal_ten_seas_treasure"}.Contains(i.SetId)).ToArray();
 public static IReadOnlyList<RpgStatRange> StatRanges(RpgItem item)
 {
  if(item.Generation is not {} rules){var copy=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(item))!;RpgCoreStatService.Apply(copy);return typeof(RpgAttributes).GetProperties().Select(p=>new RpgStatRange(p.Name,(decimal)p.GetValue(copy.Attributes)!,(decimal)p.GetValue(copy.Attributes)!)).Where(r=>r.Min!=0).ToArray();}
  var ranges=rules.BaseAttributes.ToDictionary(r=>r.Attribute,r=>new RpgStatRange(r.Attribute,r.Minimum,r.Maximum));
  var count=rules.AttributeCount??rules.MinimumAttributes??RpgItemGenerationService.Counts(item.Rarity).Min;
  foreach(var r in rules.Attributes){var optional=count<rules.Attributes.Count&&!ranges.ContainsKey(r.Attribute);ranges[r.Attribute]=new(r.Attribute,r.Minimum,r.Maximum,optional);}
  if(rules.BaseAttributes.Count==0){var baseline=RpgCoreStatService.Baseline(item.Rarity);ranges.TryAdd("Power",new("Power",baseline.Power,baseline.Power));ranges.TryAdd("Stamina",new("Stamina",baseline.Stamina,baseline.Stamina));}
  if(item.Slot is EquipmentSlot.Helmet or EquipmentSlot.Chest or EquipmentSlot.Gloves or EquipmentSlot.Boots){var core=RpgCoreStatService.Baseline(item.Rarity);ranges.TryAdd("Power",new("Power",core.Power,core.Power));ranges.TryAdd("Stamina",new("Stamina",core.Stamina,core.Stamina));ranges.TryAdd("Armor",new("Armor",core.Stamina*2,core.Stamina*2));}
  foreach(var stat in new[]{"Armor","Power","Stamina"})if(ranges.TryGetValue(stat,out var range))ranges[stat]=range with{Min=range.Min>0?range.Min*1.5m:range.Min,Max=range.Max>0?range.Max*1.5m:range.Max};
  return ranges.Values.ToArray();
 }
}
