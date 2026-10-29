using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public sealed record RpgTrinketDefinition(string Id,string Name,ItemRarity Rarity,int Level,int Attack,int Chance,int Power,int Cooldown,string Element,bool Critical,string Description);
public static class RpgTrinketCatalog
{
 public static readonly RpgTrinketDefinition[] Definitions=[
 new("trinket_shell_chip","Splitter der Brandung",ItemRarity.Common,1,3,20,25,4,"Water",false,"20 % Chance bei einem Angriff: 25 % Angriffsschaden als Wasserschaden. 4 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_crab_tooth","Zahn des Strandjägers",ItemRarity.Common,1,3,15,40,5,"Physical",false,"15 % Chance bei einem Angriff: 40 % Angriffsschaden als physischer Schaden. 5 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_reef_fang","Reißzahn des Riffs",ItemRarity.Uncommon,10,7,25,40,5,"Physical",false,"25 % Chance bei einem Angriff: 40 % Angriffsschaden als physischer Schaden. 5 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_spark_pearl","Funkenperle",ItemRarity.Uncommon,10,7,20,50,6,"Light",false,"20 % Chance bei einem Angriff: 50 % Angriffsschaden als Lichtschaden. 6 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_tide_compass","Kompass der Sturmflut",ItemRarity.Rare,20,12,25,65,6,"Water",false,"25 % Chance bei einem Angriff: 65 % Angriffsschaden als Wasserschaden. 6 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_hunter_eye","Auge des Wrackjägers",ItemRarity.Rare,20,12,45,90,5,"Physical",true,"45 % Chance bei einem kritischen Angriff: 90 % Angriffsschaden als physischer Schaden. 5 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_storm_conch","Horn der Tiefenstürme",ItemRarity.Epic,30,20,30,90,7,"Water",false,"30 % Chance bei einem Angriff: 90 % Angriffsschaden als Wasserschaden. 7 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_prism_core","Prismenkern der Jagd",ItemRarity.Epic,40,20,55,130,6,"Light",true,"55 % Chance bei einem kritischen Angriff: 130 % Angriffsschaden als Lichtschaden. 6 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_sun_idol","Idol der Sonnenflut",ItemRarity.Legendary,50,32,35,140,8,"Light",false,"35 % Chance bei einem Angriff: 140 % Angriffsschaden als Lichtschaden. 8 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_king_harpoon","Harpunensiegel des Seekönigs",ItemRarity.Legendary,50,32,65,190,7,"Physical",true,"65 % Chance bei einem kritischen Angriff: 190 % Angriffsschaden als physischer Schaden. 7 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_cursed_heart","Herz der Gierflut",ItemRarity.Cursed,70,45,40,180,8,"Abyss",false,"40 % Chance bei einem Angriff: 180 % Angriffsschaden als Abgrundschaden. 8 s Abklingzeit. Kein Auslösen durch Zusatztreffer. Fluch: −5 % maximales Leben."),
 new("trinket_whisper_bell","Glocke des letzten Flüsterns",ItemRarity.Cursed,70,45,70,230,7,"Abyss",true,"70 % Chance bei einem kritischen Angriff: 230 % Angriffsschaden als Abgrundschaden. 7 s Abklingzeit. Kein Auslösen durch Zusatztreffer. Fluch: −5 % maximales Leben."),
 new("trinket_coral_seed","Samen der Urkoralle",ItemRarity.True,80,52,40,200,8,"Water",false,"40 % Chance bei einem Angriff: 200 % Angriffsschaden als Wasserschaden. 8 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_origin_scale","Schuppe der Ursprungsströmung",ItemRarity.True,80,52,70,260,8,"Light",true,"70 % Chance bei einem kritischen Angriff: 260 % Angriffsschaden als Lichtschaden. 8 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_void_lantern","Laterne des Verschlingers",ItemRarity.Abyss,90,62,40,240,9,"Abyss",false,"40 % Chance bei einem Angriff: 240 % Angriffsschaden als Abgrundschaden. 9 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_blood_hourglass","Sanduhr der Blutströmung",ItemRarity.Abyss,90,62,75,300,8,"Physical",true,"75 % Chance bei einem kritischen Angriff: 300 % Angriffsschaden als physischer Schaden. 8 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_kraken_eye","Auge des Urkraken",ItemRarity.Mythic,80,58,45,230,9,"Abyss",false,"45 % Chance bei einem Angriff: 230 % Angriffsschaden als Abgrundschaden. 9 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_volcano_heart","Herz des Tiefseevulkans",ItemRarity.Mythic,80,58,75,290,8,"Light",true,"75 % Chance bei einem kritischen Angriff: 290 % Angriffsschaden als Lichtschaden. 8 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_eternal_orbit","Orbit der Ewigen Gezeiten",ItemRarity.Eternal,99,80,50,320,10,"Water",false,"50 % Chance bei einem Angriff: 320 % Angriffsschaden als Wasserschaden. 10 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 new("trinket_star_fragment","Splitter des ersten Sterns",ItemRarity.Eternal,99,80,80,400,10,"Light",true,"80 % Chance bei einem kritischen Angriff: 400 % Angriffsschaden als Lichtschaden. 10 s Abklingzeit. Kein Auslösen durch Zusatztreffer."),
 ];
 public static bool IsSlot(EquipmentSlot? slot)=>slot is EquipmentSlot.Trinket1 or EquipmentSlot.Trinket2;
 public static List<RpgItem> Items()=>Definitions.Select(d=>new RpgItem {ItemId=d.Id,IconId=d.Id,DisplayName=d.Name,Description=d.Description,MinimumLevel=d.Level,CoreStatsVersion=3,Rarity=d.Rarity,Kind=RpgItemKind.Equipment,Slot=EquipmentSlot.Trinket1,DropWeight=1,DropActivities=[RpgActivity.Boss],Attributes=new(){Attack=d.Attack,MaximumHpPercent=d.Rarity==ItemRarity.Cursed?-5:0}}).ToList();
 public static void AddMissing(RpgConfig c){foreach(var item in Items())if(!c.DiveLoot.Items.Any(i=>i.ItemId==item.ItemId))c.DiveLoot.Items.Add(item);}
 // A definition-wide cooldown survives swapping copies and is persisted with the profile.
 public static int Proc(int attack,bool critical,RpgProfile p,RpgAttributes stats,IDuelRandom random,DateTimeOffset now)
 {
  if(attack<=0||p.Health.CurrentHp is <=0)return 0;
  decimal total=0;
  foreach(var item in p.Inventory.Where(i=>IsSlot(i.Slot)&&p.Equipment.Any(e=>IsSlot(e.Key)&&e.Value==i.InstanceId)).DistinctBy(i=>i.ItemId))
  {
   var d=Definitions.FirstOrDefault(d=>d.Id==item.ItemId);if(d is null||d.Critical&&!critical)continue;
   var key="trinket:"+d.Id;if(p.Cooldowns.TryGetValue(key,out var until)&&now<until)continue;
   if(random.NextInclusive(0,9999)>=d.Chance*100)continue;
   p.Cooldowns[key]=now.AddSeconds(d.Cooldown);
   var bonus=d.Element switch {"Water"=>stats.WaterDamage+stats.ElementalDamage,"Light"=>stats.LightDamage+stats.ElementalDamage,"Abyss"=>stats.AbyssDamage+stats.ElementalDamage,_=>0};
   total+=decimal.Floor(attack*d.Power/100m*(1+Math.Clamp(bonus,0,10000)/100));
  }
  return (int)Math.Clamp(total,0,int.MaxValue);
 }
}
