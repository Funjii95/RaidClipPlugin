using RaidClipPlugin.Models;
namespace RaidClipPlugin.Config;
public sealed class RpgItemSettings
{
 public Dictionary<string,decimal> Attributes {get;set;}=[];
 public int? DropWeight {get;set;}
 public static void Apply(RpgConfig c){foreach(var (id,setting) in c.ItemSettings){var item=c.DiveLoot.Items.SingleOrDefault(i=>i.ItemId==id);if(item is null||item.Rarity==ItemRarity.Divine)throw new InvalidOperationException("Ungültige oder geschützte Itemanpassung.");if(setting.DropWeight is {} weight){if(weight is <0 or >1000000)throw new InvalidOperationException("Ungültiges Lootgewicht.");item.DropWeight=weight;}
  foreach(var (key,value) in setting.Attributes){var prop=typeof(RpgAttributes).GetProperty(key);if(prop is null||value is <-1000000 or >1000000)throw new InvalidOperationException("Ungültige Itemattribute.");prop.SetValue(item.Attributes,value);}}}
}
