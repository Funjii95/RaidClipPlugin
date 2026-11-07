using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;

public static class RpgMythicRoleCatalog
{
 public const string DamageSet="mythic_stormkraken", HealerSet="mythic_lifespring";
 public static bool IsSet(string id)=>id is DamageSet or HealerSet or RpgDawnHealerCatalog.SetId;
 static readonly EquipmentSlot[] Slots=[EquipmentSlot.Helmet,EquipmentSlot.Chest,EquipmentSlot.Gloves,EquipmentSlot.Boots,EquipmentSlot.Weapon,EquipmentSlot.Ring1,EquipmentSlot.Ring2,EquipmentSlot.Amulet,EquipmentSlot.Belt,EquipmentSlot.Trinket1,EquipmentSlot.Trinket2];
 static readonly string[] Parts=["helmet","chest","gloves","boots","weapon","ring","seal","amulet","belt","trinket1","trinket2"];
 static readonly string[] DamageNames=["Krone des Sturmkrakens","Schuppenpanzer der Donnerflut","Klauen des Sturmjägers","Schritte des Orkans","Dreizack des Sturmkrakens","Ring der tosenden Brandung","Siegel des Donnerauges","Herz der Sturmflut","Gurt der peitschenden Gezeiten","Auge des Sturmkrakens","Donnerkern der Urtiefe"];
 static readonly string[] HealNames=["Diadem der Lebensquelle","Gewand der heilenden Strömung","Hände der Meeresgnade","Schritte der stillen Gezeiten","Stab der Lebensquelle","Ring des sanften Regens","Siegel der Wiederkehr","Träne der Meeresmutter","Gurt der lebenden Korallen","Perle des ersten Lebens","Kelch der ewigen Quelle"];
 public static List<RpgItem> Items()
 {
  var result=new List<RpgItem>();
  foreach(var heal in new[]{false,true})for(var n=0;n<Slots.Length;n++){
   var armor=n<4;var weapon=n==4;var trinket=n>=9;
   var a=new RpgAttributes{Armor=armor?(heal?36:44):n==8?28:0,Power=heal?8:18,Stamina=heal?18:14,MaximumHp=armor?120:60};
   if(heal){a.HealingPower=weapon?12:trinket?6:3;a.CriticalHealing=weapon?4:2;a.Attack=weapon?180:trinket?30:0;}
   else {a.Attack=weapon?280:trinket?58:12;a.CritChance=weapon?4:2;a.CritDamage=weapon?12:4;}
   var id=(heal?HealerSet:DamageSet)+"_"+Parts[n];
   result.Add(new(){ItemId=id,IconId=id,DisplayName=(heal?HealNames:DamageNames)[n],Description="Mythisches 11-Teile-"+(heal?"Heiler":"DD")+"-Set. Fundort: Mythische Dungeons ab Stufe 80. Setboni zählen unterschiedliche ausgerüstete Teile.",MinimumLevel=80,CoreStatsVersion=3,Rarity=ItemRarity.Mythic,Kind=RpgItemKind.Equipment,Slot=Slots[n],SetId=heal?HealerSet:DamageSet,DropWeight=1,DropActivities=[RpgActivity.Boss],Attributes=a});
  }
  result.AddRange(RpgDawnHealerCatalog.Items());
  foreach(var item in result)RpgMythicBalance.Apply(item);
  return result;
 }
 public static List<RpgItemSet> Sets()=>[
  RpgDawnHealerCatalog.Set(),
  new(){Id=DamageSet,Name="Zorn des Sturmkrakens",Description="Mythisches DD-Set mit elf unterschiedlichen Teilen. Boni sind kumulativ.",ItemIds=Parts.Select(p=>DamageSet+"_"+p).ToList(),Bonuses=[
   new(){RequiredPieces=2,Description="Erwachender Sturm: +15 % Angriff",Attributes=new(){AttackPercent=15}},
   new(){RequiredPieces=4,Description="Auge des Jägers: +8 Prozentpunkte Krit-Chance, +20 % Krit-Schaden",Attributes=new(){CritChance=8,CritDamage=20}},
   new(){RequiredPieces=6,Description="Brechende Wellen: +20 % Boss-Schaden",Attributes=new(){BossDamage=20}},
   new(){RequiredPieces=8,Description="Zwillingsblitz: +10 Prozentpunkte Doppelangriffschance (bestehendes Limit gilt)",Attributes=new(){DoubleAttackChance=10}},
   new(){RequiredPieces=9,Description="Entfesselte Brandung: +20 % Gesamtschaden",Attributes=new(){Damage=20}},
   new(){RequiredPieces=11,Description="Zorn des Sturmkrakens: Kritische Hauptangriffe entfesseln 250 % Angriffsschaden als Wasserschaden; 8 s Abklingzeit, keine Kettenauslösung",Attributes=new()}
  ]},
  new(){Id=HealerSet,Name="Gesang der Lebensquelle",Description="Mythisches Heiler-Set mit elf unterschiedlichen Teilen. Boni sind kumulativ.",ItemIds=Parts.Select(p=>HealerSet+"_"+p).ToList(),Bonuses=[
   new(){RequiredPieces=2,Description="Quellsegen: +15 % Heilwirkung; aktive Gruppenheilung verfügbar",Attributes=new(){HealingPower=15}},
   new(){RequiredPieces=4,Description="Lebendige Gezeiten: +15 % maximales Leben",Attributes=new(){MaximumHpPercent=15}},
   new(){RequiredPieces=6,Description="Reine Quelle: +10 Prozentpunkte kritische Heilchance",Attributes=new(){CriticalHealing=10}},
   new(){RequiredPieces=8,Description="Chor der Tiefe: +20 % Heilwirkung",Attributes=new(){HealingPower=20}},
   new(){RequiredPieces=9,Description="Bewahrendes Wasser: Überheilung wird zum Gruppenschild bis zum bestehenden Schildlimit",Attributes=new()},
   new(){RequiredPieces=11,Description="Gesang der Lebensquelle: Aktive Gruppenheilung heilt zusätzlich das verletzteste andere lebende Mitglied mit 60 % der Grundheilung; 8 s Abklingzeit",Attributes=new()}
  ]}
 ];
 public static void AddMissing(RpgConfig c){foreach(var item in Items())if(!c.DiveLoot.Items.Any(i=>i.ItemId==item.ItemId))c.DiveLoot.Items.Add(item);foreach(var set in Sets())if(!c.Sets.Any(s=>s.Id==set.Id))c.Sets.Add(set);}
 public static RpgItem[] DungeonPool(RpgConfig c,int level)=>c.DiveLoot.Items.Where(x=>(IsSet(x.SetId)||x.SetId is "tank_sentinel" or "tank_leviathan"||RpgTrinketCatalog.IsSlot(x.Slot)&&x.Rarity==ItemRarity.Mythic)&&x.MinimumLevel<=level).ToArray();
 public static int Storm(int attack,bool critical,RpgProfile p,RpgConfig c,RpgAttributes stats,DateTimeOffset now){
  if(attack<=0||!critical||p.Health.CurrentHp is null or <=0||!RpgSetCombatService.Active(p,c.Sets,DamageSet,11)||p.Cooldowns.GetValueOrDefault("mythic:storm")>now)return 0;
  p.Cooldowns["mythic:storm"]=now.AddSeconds(8);
  return (int)Math.Clamp(decimal.Floor(attack*2.5m*(1+Math.Clamp(stats.WaterDamage+stats.ElementalDamage,0,10000)/100)),0,int.MaxValue);
 }
}
