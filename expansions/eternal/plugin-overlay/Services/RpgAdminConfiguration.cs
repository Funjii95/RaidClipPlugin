using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.ComponentModel;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed record AdminField(string Path,string Label,string Value,string Kind,string DefaultValue);
public sealed record AdminChange(string Path,string Value);
public static class RpgAdminConfiguration
{
 public static readonly string[] Sections=["system","loot","zones","enemies","boss","minigames","upgrades","cuisine","talents","events"];
 private static string[] Roots(string section)=>section switch{
  "system"=>["Minigame.Enabled","Minigame.PointsEnabled","Minigame.Adventures.Rpg.Enabled","Minigame.Adventures.Rpg.Health","Minigame.Adventures.Rpg.AdminAccess.TestPlayers"],
  "loot"=>["Minigame.Adventures.Rpg.BossLootChance","Minigame.Adventures.Rpg.DiveLoot","Minigame.Adventures.Rpg.Upgrades.EquipmentDropChances"],
  "zones"=>["Minigame.Adventures.Rpg.Zones"],"enemies"=>["Minigame.Adventures.Rpg.Zones.Areas","Minigame.Adventures.Rpg.Enemies"],
  "boss"=>["Minigame.Adventures.Boss","Minigame.Adventures.Rpg.AreaBosses"],
  "minigames"=>["Heist","Minigame.Adventures.Race","Minigame.GambleEnabled","Minigame.GambleCooldownSeconds","Minigame.MinimumBet","Minigame.MaximumBet","Minigame.GambleRanges","Minigame.CoinflipEnabled","Minigame.CoinflipMultiplier","Minigame.SlotsEnabled","Minigame.RouletteEnabled"],
  "upgrades"=>["Minigame.Adventures.Rpg.Upgrades"],
  "cuisine"=>["Minigame.Adventures.Rpg.FishSpecies","Minigame.Adventures.Rpg.Recipes","Minigame.Adventures.Fishing"],
  "talents"=>["Minigame.Adventures.Rpg.Perks"],"events"=>["Minigame.Adventures.Rpg.HotTime"],_=>throw new ArgumentException("Unbekannter Verwaltungsbereich.")};
 private static JsonNode? Node(JsonNode root,string path){JsonNode? n=root;foreach(var key in path.Split('.')){if(n is JsonArray a){if(!int.TryParse(key,out var index)||index<0||index>=a.Count)return null;n=a[index];}else n=n?[key];}return n;}
 private static readonly HashSet<string> Locked=["Id","ItemId","IconId","InstanceId","SetId","Slot","Rarity","MaximumRarity","Level","DisplayName","Generation","Effects","DropActivities","EquipmentSets","UpgradeRules","FoundAt","ModeratorIds","CoreStatsVersion"];
 public static IReadOnlyList<AdminField> Fields(AppConfig config,string section)
 {
  var root=JsonSerializer.SerializeToNode(config)!;var defaults=JsonSerializer.SerializeToNode(new AppConfig())!;var result=new List<AdminField>();
  void Walk(JsonNode? node,string path,string label){
   if(node is JsonObject obj){if(obj["ItemId"]?.GetValue<string>()?.StartsWith("divine_")==true)return;foreach(var pair in obj){if(!Locked.Contains(pair.Key)&&!pair.Key.EndsWith("Message")&&!pair.Key.EndsWith("Text")&&!pair.Key.Contains("Command"))Walk(pair.Value,path+"."+pair.Key,label+" · "+pair.Key);}return;}
   if(node is JsonArray array){if(array.All(n=>n is JsonValue v&&v.TryGetValue<string>(out _))){result.Add(new(path,label,string.Join(", ",array.Select(n=>n!.GetValue<string>())),"list",Node(defaults,path) is JsonArray d?string.Join(", ",d.Select(n=>n!.GetValue<string>())):""));return;}for(int i=0;i<array.Count;i++)Walk(array[i],path+"."+i,label+" · "+(array[i] is JsonObject o?o["Name"]?.ToString()??o["DisplayName"]?.ToString()??o["Id"]?.ToString()??o["ItemId"]?.ToString()??(i+1).ToString():(i+1).ToString()));return;}
   if(node is not JsonValue value)return;
   var kind=value.TryGetValue<bool>(out _)?"boolean":value.TryGetValue<decimal>(out _)?"number":"text";
   result.Add(new(path,label,value.ToString(),kind,Node(defaults,path)?.ToString()??value.ToString()));
  }
  foreach(var path in Roots(section))Walk(Node(root,path),path,path.Split('.').Last());
  return result;
 }
 public static string Version(AppConfig config,string section)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Fields(config,section))));
 public static AppConfig Apply(AppConfig current,string section,string version,IReadOnlyList<AdminChange> changes)
 {
  if(version!=Version(current,section))throw new InvalidOperationException("Konfiguration wurde zwischenzeitlich geändert. Bitte neu laden.");
  if(changes.Count is <1 or >64||changes.Select(c=>c.Path).Distinct().Count()!=changes.Count)throw new InvalidOperationException("Ungültige Änderungsliste.");
  var allowed=Fields(current,section).ToDictionary(f=>f.Path);var root=JsonSerializer.SerializeToNode(current)!;
  foreach(var change in changes){
   if(!allowed.TryGetValue(change.Path,out var field)||change.Value.Length>1000)throw new InvalidOperationException("Dieses Feld ist nicht freigegeben.");
   var keys=change.Path.Split('.');var parent=Node(root,string.Join('.',keys[..^1]))!;var old=Node(root,change.Path);JsonNode? value;
   if(field.Kind=="list")value=JsonSerializer.SerializeToNode(change.Value.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries));
   else if(field.Kind=="boolean")value=JsonValue.Create(bool.Parse(change.Value));
   else if(field.Kind=="number"){var number=decimal.Parse(change.Value,System.Globalization.CultureInfo.InvariantCulture);if(number is <-1000000000 or >1000000000)throw new InvalidOperationException("Zahl außerhalb des erlaubten Bereichs.");value=JsonValue.Create(number);}
   else value=JsonValue.Create(change.Value);
   if(parent is JsonArray a)a[int.Parse(keys[^1])]=value;else parent[keys[^1]]=value;
  }
  var next=root.Deserialize<AppConfig>()!;
  foreach(var change in changes){const string prefix="Minigame.Adventures.Rpg.DiveLoot.Items.";if(!change.Path.StartsWith(prefix))continue;var parts=change.Path[prefix.Length..].Split('.');var item=next.Minigame.Adventures.Rpg.DiveLoot.Items[int.Parse(parts[0])];if(item.Rarity==ItemRarity.Divine)throw new InvalidOperationException("Divine bleibt gesperrt.");
   if(parts.Length==3&&parts[1]=="Attributes"){if(!next.Minigame.Adventures.Rpg.ItemSettings.TryGetValue(item.ItemId,out var setting))next.Minigame.Adventures.Rpg.ItemSettings[item.ItemId]=setting=new();setting.Attributes[parts[2]]=decimal.Parse(change.Value,System.Globalization.CultureInfo.InvariantCulture);}
   if(parts.Length==2&&parts[1]=="DropWeight"){if(!next.Minigame.Adventures.Rpg.ItemSettings.TryGetValue(item.ItemId,out var setting))next.Minigame.Adventures.Rpg.ItemSettings[item.ItemId]=setting=new();setting.DropWeight=int.Parse(change.Value,System.Globalization.CultureInfo.InvariantCulture);}
  }
  ConfigurationService.ValidateHeistAndCommands(next);ConfigurationService.ValidateMinigameSettings(next.Minigame);next.Minigame.Adventures.Rpg.Validate();
  if(next.Heist.MinimumParticipants<1||next.Heist.MaximumParticipants<next.Heist.MinimumParticipants||next.Heist.MaximumParticipants>1000||next.Heist.JoinDurationSeconds is <1 or >3600||next.Heist.SuccessChancePercent is <0 or >100)throw new InvalidOperationException("Ungültige Heist-Einstellungen.");
  return next;
 }
}
