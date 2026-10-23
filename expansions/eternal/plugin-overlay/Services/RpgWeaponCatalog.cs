using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public sealed record RpgWeaponDefinition(string Id,string Name,ItemRarity Rarity,string Element,RpgAttributes Attributes,string Effect,int ManaCost,int Cooldown);
public static class RpgWeaponCatalog
{
 public static IReadOnlyList<RpgWeaponDefinition> Definitions {get;} = new RpgWeaponDefinition[]{
  new("weapon_tide_crown","Sturmkrone der Gezeiten",ItemRarity.Legendary,"Water",new(){Attack=320,CritChance=20,WaterDamage=45},"Gezeitenstoß: +150 % Angriffsschaden; der unmittelbar folgende Gegentreffer verursacht 50 % weniger Schaden.",40,8),
  new("weapon_leviathan_call","Leviathans Ruf",ItemRarity.Legendary,"Water",new(){Attack=300,MagicPower=350,ManaRegeneration=40},"Leviathanwelle: 200 % Magiekraft als Wasserschaden; der unmittelbar folgende Gegentreffer verursacht 30 % weniger Schaden.",60,8),
  new("weapon_sunken_kings","Klingen der Versunkenen Könige",ItemRarity.Legendary,"Water",new(){Attack=310,CritDamage=45,AttackSpeed=35},"Königsgeister: zwei zusätzliche Geistertreffer mit zusammen 100 % Angriffsschaden, ohne weitere Effektketten.",40,8),
  new("weapon_eternal_coral","Korallenschrei der Ewigkeit",ItemRarity.True,"Water",new(){Attack=280,CritChance=18,ElementalDamage=40},"Korallenschrei: +150 % Angriffsschaden und Heilung um 12 % der maximalen eigenen HP.",40,8),
  new("weapon_origin_current","Herz der Ursprungsströmung",ItemRarity.True,"Water",new(){Attack=270,MagicPower=320,HealingPower=45},"Ursprungsflut: 160 % Magiekraft als Wasserschaden und Eigenheilung in Höhe der Magiekraft, verstärkt durch Heilungswirkung.",60,8),
  new("weapon_reef_wardens","Wächterklingen des Riffs",ItemRarity.True,"Water",new(){Attack=290,Armor=220,MaximumHpPercent=35},"Riffwall: +50 % Angriffsschaden; eigener Schild über 20 % maximale HP für 15 Sekunden und 50 % weniger Schaden beim unmittelbaren Gegentreffer.",40,8),
  new("weapon_abyss_claw","Schattenklaue des Abgrunds",ItemRarity.Abyss,"Abyss",new(){Attack=300,CritChance=25,BossLifeleech=35},"Seelenraub: +75 % Angriffsschaden und 8 Sekunden +20 % Gesamtschaden. Lebensentzug heilt aus tatsächlich verursachtem Schaden.",40,8),
  new("weapon_despair_staff","Leerenstab der Verzweiflung",ItemRarity.Abyss,"Abyss",new(){Attack=280,MagicPower=340,AbyssDamage=50},"Riss der Verzweiflung: 200 % Magiekraft als Abgrundschaden, halbe Magiekraft als Eigenheilung und 50 % weniger Schaden beim unmittelbaren Gegentreffer.",60,8),
  new("weapon_void_devourer","Verschlingerklinge der Leere",ItemRarity.Abyss,"Abyss",new(){Attack=360,CritDamage=50,AbyssDamage=45},"Leerenhunger: +200 % Angriffsschaden und 10 % maximale eigene HP heilen.",40,8),
  new("weapon_star_tide","Sternenflut des Urmeers",ItemRarity.Eternal,"Light",new(){Attack=340,CritChance=25,LightDamage=50},"Sternenflut: +250 % Angriffsschaden als Lichtwelle und 50 % weniger Schaden beim unmittelbaren Gegentreffer.",40,6),
  new("weapon_infinite_tides","Stab der Unendlichen Gezeiten",ItemRarity.Eternal,"Water",new(){Attack=320,MagicPower=380,ManaRegeneration=60},"Zeitgezeiten: 300 % Magiekraft als Wasserschaden und 75 % weniger Schaden beim unmittelbaren Gegentreffer.",60,6),
  new("weapon_worldbreaker","Weltenbrecher der Urtiefe",ItemRarity.Eternal,"Light",new(){Attack=360,CritDamage=60,Damage=40},"Weltenbruch: +300 % Angriffsschaden als Lichtschaden und eigener Schild über 15 % maximale HP für 15 Sekunden.",40,6)
 };
 public static RpgWeaponDefinition? Find(string id)=>Definitions.FirstOrDefault(w=>w.Id==id);
 public static List<RpgItem> Items()=>Definitions.Select(w=>new RpgItem{ItemId=w.Id,IconId=w.Id,DisplayName=w.Name,Rarity=w.Rarity,Slot=EquipmentSlot.Weapon,Kind=RpgItemKind.Equipment,CoreStatsVersion=1,DropWeight=1,DropActivities=w.Rarity==ItemRarity.Eternal?[]:[RpgActivity.Boss],Attributes=System.Text.Json.JsonSerializer.Deserialize<RpgAttributes>(System.Text.Json.JsonSerializer.Serialize(w.Attributes))!,Description=w.Effect+ $" Element: {w.Element}. Automatischer Waffenzauber: {w.ManaCost} Mana, {w.Cooldown} s Abklingzeit. Keine Manakosten für gewöhnliche Angriffe."}).ToList();
 public static void AddMissing(RpgLootConfig c){foreach(var w in Items()){c.Items.RemoveAll(i=>i.ItemId==w.ItemId);c.Items.Add(w);}}
}
