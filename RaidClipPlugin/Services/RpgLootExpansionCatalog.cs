using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgLootExpansionCatalog
{
    public static void AddMissing(RpgLootConfig c)
    {
        foreach(var item in Items())
            if(!c.Items.Any(i=>i.ItemId==item.ItemId || i.DisplayName==item.DisplayName)) c.Items.Add(item);
    }
    public static List<RpgItemSet> Sets() => new()
    {
        new(){Id="depth-warden",Name="Tiefseewächter",Description="Defensive Variante: Wächter der Tiefsee. Bestehende Wächtersets bleiben unverändert.",
            ItemIds=new(){"warden-helm","warden-chest","warden-gloves","warden-boots"},Bonuses=new(){
                new(){RequiredPieces=2,Attributes=new(){MaximumHp=25}},new(){RequiredPieces=4,Attributes=new(){BossDamageReduction=10,BossDamage=3}}}},
        new(){Id="ocean-wrath",Name="Zorn des Ozeans",ItemIds=new(){"wrath-helm","wrath-chest","wrath-gloves","wrath-boots"},Bonuses=new(){
            new(){RequiredPieces=2,Attributes=new(){CritChance=5}},new(){RequiredPieces=4,Attributes=new(){BossDamage=10,DoubleAttackChance=5}}}}
    };
    public static List<RpgItem> Items()
    {
        var items=new List<RpgItem>();
        void Gear(string id,string name,ItemRarity rarity,EquipmentSlot slot,RpgAttributes a,string set="") => items.Add(new(){
            ItemId=id,DisplayName=name,Rarity=rarity,Kind=RpgItemKind.Equipment,Slot=slot,Attributes=a,SetId=set,
            DropActivities=new(){RpgActivity.Dive,RpgActivity.Boss,RpgActivity.Fishing}});
        void Treasure(string id,string name,ItemRarity rarity,RpgCollectorBonuses b) => items.Add(new(){
            ItemId=id,DisplayName=name,Rarity=rarity,Kind=RpgItemKind.Treasure,Collector=b,
            Description="Sammlerschatz: ein Exemplar genügt, Duplikate stapeln nicht. Keine Kampfboni.",DropActivities=new(){RpgActivity.Dive}});
        Gear("coral-dagger","Korallendolch",ItemRarity.Common,EquipmentSlot.Weapon,new(){BossDamage=3});
        Gear("pearl-sabre","Perlensäbel",ItemRarity.Uncommon,EquipmentSlot.Weapon,new(){BossDamage=6});
        Gear("depth-blade","Tiefseeklinge",ItemRarity.Rare,EquipmentSlot.Weapon,new(){BossDamage=12});
        Gear("storm-harpoon","Sturmharpune",ItemRarity.Epic,EquipmentSlot.Weapon,new(){BossDamage=22});
        Gear("leviathan-wrath","Zorn des Leviathans",ItemRarity.Legendary,EquipmentSlot.Weapon,new(){BossDamage=30,DoubleAttackChance=5});
        Gear("shell-helm","Muschelhelm",ItemRarity.Common,EquipmentSlot.Helmet,new(){MaximumHp=5});
        Gear("coral-crown","Korallenkrone",ItemRarity.Uncommon,EquipmentSlot.Helmet,new(){MaximumHp=10});
        Gear("depth-helm","Helm der Tiefen",ItemRarity.Rare,EquipmentSlot.Helmet,new(){MaximumHp=20});
        // Krone des Abgrunds already exists with rolled attributes; never replace its definition.
        Gear("leviathan-gaze","Leviathans Blick",ItemRarity.Legendary,EquipmentSlot.Helmet,new(){MaximumHp=45,CritChance=3});
        Gear("algae-armour","Algenpanzer",ItemRarity.Common,EquipmentSlot.Chest,new(){MaximumHp=10});
        Gear("scale-armour","Schuppenrüstung",ItemRarity.Uncommon,EquipmentSlot.Chest,new(){MaximumHp=20});
        Gear("coral-cuirass","Korallenharnisch",ItemRarity.Rare,EquipmentSlot.Chest,new(){MaximumHp=35});
        Gear("tidal-armour","Rüstung der Gezeiten",ItemRarity.Epic,EquipmentSlot.Chest,new(){MaximumHp=50,BossDamageReduction=5});
        Gear("abyss-armour","Panzer des Abgrunds",ItemRarity.Legendary,EquipmentSlot.Chest,new(){MaximumHp=60,BossDamageReduction=12});
        Gear("diver-gloves","Taucherhandschuhe",ItemRarity.Common,EquipmentSlot.Gloves,new(){CritChance=1});
        Gear("coral-grip","Korallengriff",ItemRarity.Uncommon,EquipmentSlot.Gloves,new(){CritChance=2});
        Gear("depth-gloves","Handschuhe der Tiefen",ItemRarity.Rare,EquipmentSlot.Gloves,new(){CritChance=4});
        Gear("storm-grip","Sturmgreifer",ItemRarity.Epic,EquipmentSlot.Gloves,new(){CritChance=6});
        Gear("leviathan-claws","Leviathans Klauen",ItemRarity.Legendary,EquipmentSlot.Gloves,new(){CritChance=8,DoubleAttackChance=15});
        Gear("sand-runner","Sandläufer",ItemRarity.Common,EquipmentSlot.Boots,new(){MaximumHp=5});
        Gear("coral-boots","Korallenstiefel",ItemRarity.Uncommon,EquipmentSlot.Boots,new(){MaximumHp=10});
        Gear("depth-steps","Tiefseeschritte",ItemRarity.Rare,EquipmentSlot.Boots,new(){MaximumHp=15});
        Gear("current-shoes","Schuhe der Strömung",ItemRarity.Epic,EquipmentSlot.Boots,new(){MaximumHp=20,BossDodgeChance=5});
        Gear("tidal-runner","Gezeitenläufer",ItemRarity.Legendary,EquipmentSlot.Boots,new(){MaximumHp=25,BossDodgeChance=10});
        Gear("shell-pendant","Muschelanhänger",ItemRarity.Common,EquipmentSlot.Amulet,new(){MaximumHp=5});
        Gear("lucky-pearl","Glücksperle",ItemRarity.Uncommon,EquipmentSlot.Amulet,new(){RareEquipmentLuck=2});
        Gear("depth-talisman","Tiefsee-Talisman",ItemRarity.Rare,EquipmentSlot.Amulet,new(){BossDamage=5});
        Gear("current-amulet","Amulett der Strömung",ItemRarity.Epic,EquipmentSlot.Amulet,new(){BossDamage=8});
        Gear("neptune-heart","Neptuns Herz",ItemRarity.Legendary,EquipmentSlot.Amulet,new(){BossDamage=10,CriticalHealing=5});
        Gear("shell-ring","Ring der Muscheln",ItemRarity.Common,EquipmentSlot.Ring1,new(){MaximumHp=5});
        Gear("tidal-ring","Gezeitenring",ItemRarity.Uncommon,EquipmentSlot.Ring1,new(){MaximumHp=10});
        Gear("hunter-ring","Ring des Jägers",ItemRarity.Rare,EquipmentSlot.Ring1,new(){CritChance=3});
        Gear("depth-ruby","Blutrubin der Tiefen",ItemRarity.Epic,EquipmentSlot.Ring1,new(){CritChance=5,MaximumHp=10});
        Gear("leviathan-oath","Leviathans Schwur",ItemRarity.Legendary,EquipmentSlot.Ring1,new(){CritChance=8,MaximumHp=30});
        Gear("warden-helm","Helm des Tiefseewächters",ItemRarity.Epic,EquipmentSlot.Helmet,new(){MaximumHp=35},"depth-warden");
        Gear("warden-chest","Brustplatte des Tiefseewächters",ItemRarity.Epic,EquipmentSlot.Chest,new(){MaximumHp=50,BossDamageReduction=5},"depth-warden");
        Gear("warden-gloves","Handschuhe des Tiefseewächters",ItemRarity.Epic,EquipmentSlot.Gloves,new(){CritChance=6},"depth-warden");
        Gear("warden-boots","Stiefel des Tiefseewächters",ItemRarity.Epic,EquipmentSlot.Boots,new(){MaximumHp=20,BossDodgeChance=5},"depth-warden");
        Gear("wrath-helm","Krone des Ozeanzorns",ItemRarity.Legendary,EquipmentSlot.Helmet,new(){MaximumHp=45,CritChance=3},"ocean-wrath");
        Gear("wrath-chest","Panzer des Ozeanzorns",ItemRarity.Legendary,EquipmentSlot.Chest,new(){MaximumHp=60,BossDamageReduction=12},"ocean-wrath");
        Gear("wrath-gloves","Klauen des Ozeanzorns",ItemRarity.Legendary,EquipmentSlot.Gloves,new(){CritChance=8,DoubleAttackChance=15},"ocean-wrath");
        Gear("wrath-boots","Schritte des Ozeanzorns",ItemRarity.Legendary,EquipmentSlot.Boots,new(){MaximumHp=25,BossDodgeChance=10},"ocean-wrath");
        Treasure("lucky-shell","Kleine Glücksmuschel",ItemRarity.Common,new(){FishingPearls=1});
        Treasure("pearl-compass","Perlenkompass",ItemRarity.Uncommon,new(){FishingPearls=2});
        Treasure("depth-map","Schatzkarte der Tiefsee",ItemRarity.Rare,new(){RareTreasureChance=2});
        Treasure("shell-seal","Muschelsammlersiegel",ItemRarity.Rare,new(){FishingPearls=3});
        Treasure("wealth-pearl","Perle des Reichtums",ItemRarity.Epic,new(){BossPearls=5});
        Treasure("captain-map","Alte Kapitänskarte",ItemRarity.Epic,new(){TreasureChance=3});
        Treasure("lucky-compass","Kompass des Glücks",ItemRarity.Legendary,new(){RareTreasureChance=5});
        Treasure("tidal-hourglass","Sanduhr der Gezeiten",ItemRarity.Legendary,new(){BossExperience=5});
        Treasure("collector-crown","Krone des Perlensammlers",ItemRarity.Legendary,new(){DivePearls=5});
        Treasure("golden-shell","Goldene Muschel",ItemRarity.Legendary,new(){RareTreasureChance=5});
        return items;
    }
}
