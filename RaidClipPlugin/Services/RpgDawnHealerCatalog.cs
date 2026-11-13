using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;

public static class RpgDawnHealerCatalog
{
 public const string SetId="mythic_dawn";
 static readonly EquipmentSlot[] Slots=[EquipmentSlot.Helmet,EquipmentSlot.Chest,EquipmentSlot.Gloves,EquipmentSlot.Boots,EquipmentSlot.Weapon,EquipmentSlot.Ring1,EquipmentSlot.Ring2,EquipmentSlot.Amulet,EquipmentSlot.Belt,EquipmentSlot.Trinket1,EquipmentSlot.Trinket2];
 public static readonly string[] Parts=["helmet","chest","gloves","boots","weapon","ring","seal","amulet","belt","trinket1","trinket2"];
 static readonly string[] Names=["Diadem der Morgenflut","Gewand der goldenen Gnade","Hände des sanften Lichts","Schritte der weißen Brandung","Stab der Morgenflut","Ring des Perlensegens","Siegel der goldenen Sonne","Träne der Morgenröte","Gurt der lichten Gezeiten","Reliquie der ersten Perle","Kelch der Morgenflut"];
 public static List<RpgItem> Items()=>Slots.Select((slot,n)=>new RpgItem{
  ItemId=SetId+"_"+Parts[n],IconId=SetId+"_"+Parts[n],DisplayName=Names[n],SetId=SetId,
  Description="Gnade der Morgenflut: mächtiges weiß-goldenes Heiler-Set mit elf Teilen, ab Level 1 tragbar. Fundort: Mythische Dungeons; auch als vollständiges Set über die Adminvergabe erhältlich.",
  MinimumLevel=1,CoreStatsVersion=3,Rarity=ItemRarity.Mythic,Kind=RpgItemKind.Equipment,Slot=slot,DropWeight=1,DropActivities=[RpgActivity.Boss],
  Attributes=new(){Armor=n<4?60:n==8?40:0,Power=12,Stamina=30,MaximumHp=n<4?180:90,HealingPower=n==4?18:n>=9?9:5,CriticalHealing=n==4?5:3,Attack=n==4?220:n>=9?40:0}
 }).ToList();
 public static RpgItemSet Set()=>new(){Id=SetId,Name="Gnade der Morgenflut",Description="Mythisches Heiler-Set mit elf unterschiedlichen Teilen. Ohne Levelbeschränkung tragbar. Alle Setboni sind kumulativ.",ItemIds=Parts.Select(p=>SetId+"_"+p).ToList(),Bonuses=[
  new(){RequiredPieces=2,Description="Erstes Licht: +25 % Heilwirkung; aktive Gruppenheilung verfügbar",Attributes=new(){HealingPower=25}},
  new(){RequiredPieces=4,Description="Weißer Schutz: +25 % maximales Leben",Attributes=new(){MaximumHpPercent=25}},
  new(){RequiredPieces=6,Description="Goldene Zuversicht: +15 Prozentpunkte kritische Heilchance",Attributes=new(){CriticalHealing=15}},
  new(){RequiredPieces=8,Description="Flut der Gnade: +35 % Heilwirkung",Attributes=new(){HealingPower=35}},
  new(){RequiredPieces=9,Description="Perlenschleier: Überheilung der Gruppenheilung wird zum Schild für das geheilte Ziel; bestehendes Schildlimit gilt",Attributes=new()},
  new(){RequiredPieces=11,Description="Morgenflut: Aktive Gruppenheilung heilt zusätzlich alle anderen lebenden Gruppenmitglieder mit 75 % der Grundheilung; 8 s Abklingzeit, keine Kettenauslösung",Attributes=new()}
 ]};
}
