namespace RaidClipPlugin.Config;
public sealed record RpgDungeonDefinition(string Id,string Name,string ZoneId,int MinimumLevel,string Description,string BossId);
public sealed class RpgDungeonConfig
{
 public bool Enabled {get;set;}=true;
 public Dictionary<string,decimal> HpMultipliers {get;set;}=new(){["normal"]=1,["heroic"]=1.5m,["mythic"]=2};
 public Dictionary<string,decimal> AttackMultipliers {get;set;}=new(){["normal"]=1,["heroic"]=1.25m,["mythic"]=1.5m};
 public Dictionary<string,decimal> RewardMultipliers {get;set;}=new(){["normal"]=1,["heroic"]=1.5m,["mythic"]=2};
 public Dictionary<string,decimal> LegendaryChances {get;set;}=new(){["normal"]=30,["heroic"]=50,["mythic"]=80};
 public int RecoveryPercent {get;set;}=50;public int RecoveryCooldownSeconds {get;set;}=60;
 public static readonly RpgDungeonDefinition[] Definitions=[new("runen_halls","Die Versunkenen Runenhallen","sunken_temples",10,"Vergessene Wächter, Runenmagie und der Tempelhüter.","boss_thalruun"),new("coral_labyrinth","Das Herz des Korallenlabyrinths","glowing_coral_forest",35,"Lebendige Korallen und gefährliche Lichtblüten.","boss_luminara"),new("void_citadel","Zitadelle der Leere","abyss",70,"Schatten, Seelenräuber und der Verschlinger der Leere.","boss_nharzul")];
 public static int Minimum(RpgDungeonDefinition d,string difficulty)=>Math.Min(99,d.MinimumLevel+(difficulty=="heroic"?10:difficulty=="mythic"?20:0));
 public void Validate(){foreach(var map in new[]{HpMultipliers,AttackMultipliers,RewardMultipliers,LegendaryChances})if(map.Count!=3||new[]{"normal","heroic","mythic"}.Any(k=>!map.ContainsKey(k))||map.Values.Any(v=>v<0||v>100))throw new InvalidOperationException("Ungültige Dungeonregeln.");if(HpMultipliers.Values.Any(v=>v<1)||AttackMultipliers.Values.Any(v=>v<1)||RecoveryPercent is <1 or >100||RecoveryCooldownSeconds is <30 or >600)throw new InvalidOperationException("Ungültige Dungeon-Skalierung.");}
}
