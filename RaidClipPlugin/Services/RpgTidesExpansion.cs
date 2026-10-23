using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Additive catalog: existing definitions and rolled instances are never replaced.</summary>
public static class RpgTidesExpansion
{
    public static void AddMissing(RpgConfig config)
    {
        AddMissing(config.DiveLoot);
        foreach (var set in Sets())
            if (!config.Sets.Any(s => s.Id == set.Id)) config.Sets.Add(set);
    }
    public static void AddMissing(RpgLootConfig config)
    {
        foreach (var item in Items())
            if (!config.Items.Any(i => i.ItemId == item.ItemId)) config.Items.Add(item);
    }
    public static List<RpgItem> Items()
    {
        var result = new List<RpgItem>();
        void Series(string prefix, ItemRarity rarity, string[] names, bool set = false)
        {
            string[] suffixes = ["helmet", "chest", "gloves", "boots", "ring", "amulet"];
            EquipmentSlot[] slots = [EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Gloves, EquipmentSlot.Boots, EquipmentSlot.Ring1, EquipmentSlot.Amulet];
            for (var n = 0; n < 6; n++) Add(prefix + "_" + suffixes[n], names[n], rarity, slots[n], set ? prefix == "tide_king" ? "tide_king_regalia" : prefix : "");
        }
        void Add(string id, string name, ItemRarity rarity, EquipmentSlot slot, string set = "") => result.Add(new()
        {
            ItemId=id, IconId=id, DisplayName=name, Description="Gezeiten-Erweiterung. Werte werden einmal beim Fund ausgewürfelt.",
            Rarity=rarity, Slot=slot, SetId=set, Kind=RpgItemKind.Equipment, CoreStatsVersion=1,
            DropActivities=[RpgActivity.Dive,RpgActivity.Fishing,RpgActivity.Boss], Generation=Rules(slot,rarity)
        });
        Series("tide_king",ItemRarity.Legendary,["Krone des Gezeitenkönigs","Brustpanzer der Königsflut","Krallen der Sturmflut","Stiefel des Tiefenstroms","Siegel des Gezeitenkönigs","Herz der Königsflut"],true);
        Series("abyss_hunter",ItemRarity.Legendary,["Maske des Abgrundjägers","Panzer der Leere","Griffe der Finsternis","Schritte des Niemals","Siegel der Finsternis","Auge des Abgrunds"],true);
        Series("coral_oracle",ItemRarity.Legendary,["Diadem des Orakels","Gewand der lebenden Riffe","Hände des Orakels","Stiefel der Korallenwächter","Ring des Orakels","Amulett der Gezeitenweisheit"],true);
        Add("current_twin_ring_a","Ring der ersten Strömung",ItemRarity.Legendary,EquipmentSlot.Ring1,"current_twins");
        Add("current_twin_ring_b","Ring der zweiten Strömung",ItemRarity.Legendary,EquipmentSlot.Ring1,"current_twins");
        Add("dark_pearl_ring_a","Ring der dunklen Perle",ItemRarity.Legendary,EquipmentSlot.Ring1,"dark_pearl_twins");
        Add("dark_pearl_ring_b","Ring der schwarzen Strömung",ItemRarity.Legendary,EquipmentSlot.Ring1,"dark_pearl_twins");
        Add("moon_tide_ring","Mondflut-Band",ItemRarity.Legendary,EquipmentSlot.Ring1,"moon_tide_jewelry");
        Add("moon_tide_amulet","Tiefsee-Amulett",ItemRarity.Legendary,EquipmentSlot.Amulet,"moon_tide_jewelry");
        Series("reef_guardian",ItemRarity.Epic,["Riffwächter-Helm","Riffwächter-Panzer","Riffwächter-Krallen","Riffwächter-Stiefel","Riffwächter-Ring","Riffwächter-Amulett"]);
        Series("lagoon_runner",ItemRarity.Epic,["Lagunenläufer-Helm","Lagunenläufer-Wams","Lagunenläufer-Handschuhe","Lagunenläufer-Stiefel","Lagunenläufer-Ring","Lagunenläufer-Amulett"]);
        Series("shell_speaker",ItemRarity.Epic,["Muschelsprecher-Krone","Muschelsprecher-Gewand","Muschelsprecher-Handschuhe","Muschelsprecher-Stiefel","Muschelsprecher-Ring","Muschelsprecher-Amulett"]);
        Series("kelp_collector",ItemRarity.Uncommon,["Tangsammler-Helm","Tangsammler-Weste","Tangsammler-Handschuhe","Tangsammler-Stiefel","Tangsammler-Ring","Tangsammler-Amulett"]);
        Series("coast_runner",ItemRarity.Uncommon,["Küstenläufer-Kapuze","Küstenläufer-Wams","Küstenläufer-Handschuhe","Küstenläufer-Stiefel","Küstenläufer-Ring","Küstenläufer-Amulett"]);
        Series("pearl_digger",ItemRarity.Uncommon,["Perlenbuddler-Helm","Perlenbuddler-Panzer","Perlenbuddler-Handschuhe","Perlenbuddler-Stiefel","Perlenbuddler-Ring","Perlenbuddler-Amulett"]);
        Add("otter_captain_hood","Otterkapitäns-Haube",ItemRarity.Epic,EquipmentSlot.Helmet);
        Add("coral_keeper_vest","Korallenhüter-Wams",ItemRarity.Rare,EquipmentSlot.Chest);
        Add("pearl_diver_claws","Perlentaucher-Krallen",ItemRarity.Epic,EquipmentSlot.Gloves);
        Add("tide_runner_boots","Gezeitenläufer-Stiefel",ItemRarity.Legendary,EquipmentSlot.Boots);
        Add("still_lagoon_ring","Ring der stillen Lagune",ItemRarity.Rare,EquipmentSlot.Ring1);
        Add("abyss_moon_amulet","Abyssischer Mondanhänger",ItemRarity.Epic,EquipmentSlot.Amulet);
        return result;
    }
    public static RpgItemGeneration Rules(EquipmentSlot slot,ItemRarity rarity)
    {
        var tier=rarity==ItemRarity.Uncommon?0:rarity==ItemRarity.Legendary?2:1;
        (int Min,int Max)[] hp=slot switch
        {
            EquipmentSlot.Helmet => [(8,20),(20,40),(35,65)], EquipmentSlot.Chest => [(16,32),(35,65),(70,110)],
            EquipmentSlot.Boots => [(10,24),(20,45),(35,65)], EquipmentSlot.Amulet => [(12,28),(25,50),(45,85)], _=>[(0,0),(0,0),(0,0)]
        };
        var basis=new List<RpgAttributeRange>();
        void Base(string name,(int Min,int Max) range) {if(range.Max>0)basis.Add(new(){Attribute=name,Minimum=range.Min,Maximum=range.Max});}
        Base("MaximumHp",hp[tier]);
        if(slot is EquipmentSlot.Helmet or EquipmentSlot.Chest) Base("Armor",slot==EquipmentSlot.Helmet?new[]{(3,8),(8,16),(12,25)}[tier]:new[]{(5,12),(12,24),(22,40)}[tier]);
        if(slot is EquipmentSlot.Gloves or EquipmentSlot.Ring1) Base("Damage",slot==EquipmentSlot.Gloves?new[]{(4,10),(10,20),(20,35)}[tier]:new[]{(3,9),(8,17),(15,28)}[tier]);
        // Rare bases interpolate between uncommon and epic; all ranges remain editable in balancing config.
        if(rarity==ItemRarity.Rare)foreach(var b in basis){b.Minimum=Math.Max(1,(int)(b.Minimum*0.8m));b.Maximum=Math.Max(b.Minimum,(int)(b.Maximum*0.85m));}
        var (min,max)=rarity switch{ItemRarity.Uncommon=>(1,4),ItemRarity.Rare=>(2,6),ItemRarity.Epic=>(4,9),_=>(6,14)};
        string[] pool=["CritChance","CritDamage","BossDamage","Luck","PearlBonus","FishingLuck","BossDodgeChance","AttackSpeed","HealingPower","DoubleAttackChance","BossDamageReduction"];
        return new(){GenerateEffect=false,BaseAttributes=basis,MinimumAttributes=rarity==ItemRarity.Legendary?3:rarity==ItemRarity.Uncommon?1:2,
            MaximumAttributes=rarity==ItemRarity.Legendary?4:rarity==ItemRarity.Uncommon?2:3,
            Attributes=pool.Select(a=>new RpgAttributeRange{Attribute=a,Minimum=a is "DoubleAttackChance" or "BossDamageReduction"?1:min,Maximum=a is "DoubleAttackChance" or "BossDamageReduction"?Math.Max(2,max/2):max}).ToList()};
    }
    public static List<RpgItemSet> Sets()
    {
        RpgSetBonus B(int n,string text,RpgAttributes? a=null)=>new(){RequiredPieces=n,Description=text,Attributes=a??new()};
        RpgItemSet S(string id,string name,params RpgSetBonus[] bonuses)=>new(){Id=id,Name=name,ItemIds=Items().Where(i=>i.SetId==id).Select(i=>i.ItemId).ToList(),Bonuses=bonuses.ToList()};
        return [
            S("tide_king_regalia","Regalia des Gezeitenkönigs",B(2,"+18 % maximales Leben, +10 % Rüstung",new(){MaximumHpPercent=18,ArmorPercent=10}),B(4,"Krits: Gezeitenwelle (160 % Angriffsschaden), 8 s Abklingzeit"),B(6,"+24 % Boss-Schaden; nach Boss-Treffer 8 s lang 3 % HP/s",new(){BossDamage=24})),
            S("abyss_hunter","Rüstung des Abgrundjägers",B(2,"+12 % Krit-Chance, +14 % Luck",new(){CritChance=12,Luck=14}),B(4,"Doppelangriff: Schattenflosse (170 % Angriffsschaden), 6 s Abklingzeit"),B(6,"Nach Ausweichen 8 s: +28 % Angriffstempo, +18 % Boss-Schaden")),
            S("coral_oracle","Gewand des Korallenorakels",B(2,"+20 % Heilungswirkung, +16 % maximales Leben",new(){HealingPower=20,MaximumHpPercent=16}),B(4,"Alle 10 s 22 % maximale HP heilen, sofern HP fehlen"),B(6,"Überheilung: Schild bis 18 % HP; neuer Schild: +20 % Boss-Schaden für 8 s")),
            S("current_twins","Zwillinge der Strömung",B(2,"+12 % Angriffstempo, +12 % Krit-Chance",new(){AttackSpeed=12,CritChance=12})),
            S("dark_pearl_twins","Dunkle Perlen",B(2,"+18 % Boss-Schaden, +10 % Luck",new(){BossDamage=18,Luck=10})),
            S("moon_tide_jewelry","Mondflut",B(2,"+20 % Heilungswirkung; alle 15 s Schild mit 12 % maximalen HP",new(){HealingPower=20}))
        ];
    }
}
