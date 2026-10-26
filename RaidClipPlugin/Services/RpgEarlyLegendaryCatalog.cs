using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public static class RpgEarlyLegendaryCatalog
{
 public static List<RpgItem> Items()
 {
  RpgItem Item(string id,string name,int level,EquipmentSlot slot,string effect,RpgAttributes? stats=null){var a=stats??new();a.Power=2+level/10;a.Stamina=2+level/8;if(slot is EquipmentSlot.Helmet or EquipmentSlot.Chest or EquipmentSlot.Gloves or EquipmentSlot.Boots)a.Armor=6+level;return new(){ItemId="starter_"+id,IconId="starter_"+id,DisplayName=name,MinimumLevel=level,Slot=slot,Kind=RpgItemKind.Equipment,Rarity=ItemRarity.Legendary,CoreStatsVersion=3,DropWeight=0,DropActivities=[],Attributes=a,Description=$"Legendärer Einzelgegenstand · benötigt Stufe {level}. {effect} Fundort: gewonnene Gebietskämpfe in Sonnenlagune, Versunkenen Tempeln und Wrackfriedhof."};}
  return [
   Item("crab_breaker","Krabbenbrecher",5,EquipmentSlot.Weapon,"15 % Chance auf einen Zusatztreffer mit 50 % des regulären Schadens; löst keine weiteren Effekte aus.",new(){Attack=18}),
   Item("pearl_spear","Perlenlanze",12,EquipmentSlot.Weapon,"+5 % Lebensraub aus tatsächlich verursachtem Schaden.",new(){Attack=25,BossLifeleech=5}),
   Item("tide_dagger","Gezeitendolch",22,EquipmentSlot.Weapon,"+8 Prozentpunkte kritische Trefferchance.",new(){Attack=35,CritChance=8}),
   Item("hunter_helm","Helm des Jägers",20,EquipmentSlot.Helmet,"Kritische Treffer verursachen +20 % kritischen Schaden.",new(){CritDamage=20}),
   Item("bubble_hood","Blasenhaube",3,EquipmentSlot.Helmet,"+10 % maximale Lebenspunkte; Anlegen heilt nicht.",new(){MaximumHpPercent=10}),
   Item("coral_crown","Korallenkrone",28,EquipmentSlot.Helmet,"+15 % Heilungswirkung.",new(){HealingPower=15}),
   Item("forgotten_plate","Panzer des Vergessenen",25,EquipmentSlot.Chest,"Einmal pro Kampf einen tödlichen Treffer überleben und 20 % maximale HP behalten; kein Wiederbeleben bereits besiegter Spieler."),
   Item("shell_guard","Muschelbastion",10,EquipmentSlot.Chest,"+15 % Rüstung.",new(){ArmorPercent=15}),
   Item("kelp_vest","Weste des Lebenskelps",1,EquipmentSlot.Chest,"Zusätzlich +1 HP pro Minute passive Regeneration; nur lebend."),
   Item("reef_grip","Riffgreifer",7,EquipmentSlot.Gloves,"+5 Prozentpunkte Doppelangriffschance.",new(){DoubleAttackChance=5}),
   Item("echo_gloves","Handschuhe des Echos",18,EquipmentSlot.Gloves,"Bossangriffe sind 10 % schneller.",new(){AttackSpeed=10}),
   Item("ghost_flippers","Geisterflossen",15,EquipmentSlot.Boots,"+8 Prozentpunkte Ausweichchance bei Gegner- und Bossangriffen.",new(){BossDodgeChance=8}),
   Item("current_boots","Stiefel der Strömung",4,EquipmentSlot.Boots,"+10 % Bewegungstempo verkürzt auch Tauch- und Angelpausen.",new(){MovementSpeed=10}),
   Item("turtle_boots","Schildkrötenstiefel",26,EquipmentSlot.Boots,"8 % zusätzliche Schadensreduktion.",new(){BossDamageReduction=8}),
   Item("healing_ring","Ring der Tiefenheilung",10,EquipmentSlot.Ring1,"12 % Chance nach einem direkten Treffer, 5 % Max-HP zu heilen; keine Wirkungsketten."),
   Item("sun_ring","Sonnenperlenring",2,EquipmentSlot.Ring1,"+10 % Heilungswirkung.",new(){HealingPower=10}),
   Item("abyss_luck","Abyssaler Glücksring",30,EquipmentSlot.Ring2,"+5 Prozentpunkte Chance auf legendäre Anfängerbeute in den drei Anfängergebieten."),
   Item("precision_ring","Ring der Runenpräzision",14,EquipmentSlot.Ring2,"+5 Prozentpunkte kritische Trefferchance.",new(){CritChance=5}),
   Item("pearl_amulet","Perlentaucher-Amulett",8,EquipmentSlot.Amulet,"10 % Chance auf 10 zusätzliche Perlen bei einem erfolgreichen Tauchgang."),
   Item("life_amulet","Amulett der Lebensströmung",24,EquipmentSlot.Amulet,"+5 % Lebensraub aus tatsächlich verursachtem Schaden.",new(){BossLifeleech=5})
  ];
 }
 public static void AddMissing(RpgConfig c){foreach(var item in Items())if(!c.DiveLoot.Items.Any(i=>i.ItemId==item.ItemId))c.DiveLoot.Items.Add(item);}
 public static RpgItem? Roll(RpgProfile p,RpgConfig c,RpgZoneDefinition zone,IDuelRandom random,DateTimeOffset now)
 {
  if(!c.EarlyLegendaries.Enabled||zone.Level>30)return null;
  var chance=Math.Clamp(RpgCombatDropRules.Chance(c.EarlyLegendaries.DropChance)+(RpgEarlyLegendaryEffects.Has(p,"abyss_luck")?c.EarlyLegendaries.LuckyRingBonus:0),0,100);
  if(random.NextInclusive(0,999999)>=chance*10000)return null;
  var pool=c.DiveLoot.Items.Where(i=>i.ItemId.StartsWith("starter_")&&i.MinimumLevel<=p.PlayerLevel).ToArray();if(pool.Length==0)return null;
  var found=System.Text.Json.JsonSerializer.Deserialize<RpgItem>(System.Text.Json.JsonSerializer.Serialize(pool[random.NextInclusive(0,pool.Length-1)]))!;found.InstanceId=Guid.NewGuid().ToString("N");found.FoundAt=now;return found;
 }
}
public static class RpgEarlyLegendaryEffects
{
 public static bool Has(RpgProfile p,string suffix)=>p.Inventory.Any(i=>i.ItemId=="starter_"+suffix&&i.MinimumLevel<=p.PlayerLevel&&p.Equipment.Values.Contains(i.InstanceId));
 public static int ExtraHit(int regular,RpgProfile p,RpgConfig c,IDuelRandom random)=>c.EarlyLegendaries.Enabled&&Has(p,"crab_breaker")&&random.NextInclusive(0,9999)<c.EarlyLegendaries.CrabChance*100?(int)decimal.Floor(regular*c.EarlyLegendaries.CrabStrength):0;
 public static void AfterHit(int actual,RpgProfile p,RpgConfig c,IDuelRandom random,DateTimeOffset now){if(actual>0&&c.EarlyLegendaries.Enabled&&Has(p,"healing_ring")&&random.NextInclusive(0,9999)<c.EarlyLegendaries.HealingChance*100)RpgSetCombatService.Heal(p,c.Health,(int)decimal.Floor(RpgHealthService.Maximum(p,c.Health)*c.EarlyLegendaries.HealingMaxHpPercent/100),now);}
 public static int Receive(int damage,RpgProfile p,RpgConfig c,string combat,DateTimeOffset now)
 {
  if(!c.EarlyLegendaries.Enabled||p.Health.CurrentHp is null or <=0||damage<p.Health.CurrentHp||!Has(p,"forgotten_plate"))return damage;
  var key="starter:rescue:"+combat;foreach(var old in p.Cooldowns.Where(k=>k.Key.StartsWith("starter:rescue:")&&k.Value<now).Select(k=>k.Key).ToArray())p.Cooldowns.Remove(old);
  if(p.Cooldowns.ContainsKey(key))return damage;p.Cooldowns[key]=now.AddDays(1);
  var saved=Math.Max(1,(int)decimal.Floor(RpgHealthService.Maximum(p,c.Health)*c.EarlyLegendaries.RescueMaxHpPercent/100));
  if(p.Health.CurrentHp<saved)RpgSetCombatService.Heal(p,c.Health,saved-p.Health.CurrentHp.Value,now);
  return Math.Max(0,p.Health.CurrentHp.Value-saved);
 }
}
