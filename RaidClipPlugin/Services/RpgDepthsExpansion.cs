using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Stable additive definitions from the supplied True/Abyss/Cursed artwork.</summary>
public static class RpgDepthsExpansion
{
    public static void AddMissing(RpgLootConfig c)
    {
        foreach(var item in Items()) if(!c.Items.Any(i=>i.ItemId==item.ItemId))c.Items.Add(item);
    }
    public static void AddMissing(RpgConfig c)
    {
        AddMissing(c.DiveLoot);
        foreach(var rarity in new[]{ItemRarity.True,ItemRarity.Cursed,ItemRarity.Abyss})
            if(!c.Salvage.Values.ContainsKey(rarity))c.Salvage.Values[rarity]=new RpgSalvageConfig().Values[rarity];
        foreach(var set in Sets())if(!c.Sets.Any(s=>s.Id==set.Id))c.Sets.Add(set);
    }
    public static List<RpgItem> Items()
    {
        var items=new List<RpgItem>();
        string[] suffix=["helmet","chest","gloves","boots","ring","amulet"];
        EquipmentSlot[] slots=[EquipmentSlot.Helmet,EquipmentSlot.Chest,EquipmentSlot.Gloves,EquipmentSlot.Boots,EquipmentSlot.Ring1,EquipmentSlot.Amulet];
        void Add(string id,string name,ItemRarity rarity,EquipmentSlot slot,RpgAttributes? stats=null,string set="")=>items.Add(new(){ItemId=id,IconId=id,DisplayName=name,Rarity=rarity,Slot=slot,SetId=set,Kind=RpgItemKind.Equipment,CoreStatsVersion=1,Attributes=stats??new(),Generation=stats is null?RpgTidesExpansion.Rules(slot,ItemRarity.Legendary):null,DropActivities=rarity==ItemRarity.Abyss?[RpgActivity.Boss]:[RpgActivity.Dive,RpgActivity.Fishing,RpgActivity.Boss],Description=rarity==ItemRarity.Cursed?"Verflucht: starke Vorteile mit dauerhaften Nachteilen. Aufwertungen verstärken ausschließlich positive Grundwerte.":"Werte werden beim Fund dauerhaft gespeichert. Setboni zählen nur unterschiedliche angelegte Teile."});
        void Series(string id,ItemRarity rarity,string[] names){for(int n=0;n<6;n++)Add(id+"_"+suffix[n],names[n],rarity,slots[n],set:id);}
        Series("true_eternal_reef",ItemRarity.True,["Helm des Ewigen Riffs","Riffherz-Panzer","Krallen der Reinheit","Stiefel der Korallenwurzel","Ring der Riffessenz","Amulett des Ewigen Riffs"]);
        Series("true_deep_guardian",ItemRarity.True,["Helm des Tiefenwächters","Brustpanzer des Meereswalls","Handschuhe des Wächters","Stiefel der Standhaftigkeit","Ring des Schutzwalls","Amulett des Tiefenherzens"]);
        Series("true_ancient_current",ItemRarity.True,["Diadem des Strömungssängers","Gewand des Urmeers","Handschuhe der Flutmagie","Stiefel des Meergesangs","Ring der Urströmung","Amulett des Gezeitenliedes"]);
        Series("abyss_devourer",ItemRarity.Abyss,["Krone des Verschlingers","Panzer der Endlosen Leere","Klauen der Auslöschung","Schritte des Untergangs","Ring des Nichts","Herz des Verschlingers"]);
        Series("abyss_blood_dynasty",ItemRarity.Abyss,["Blutkrone der Dynastie","Brustplatte des Blutmeers","Krallen des Tiefenblutes","Stiefel der Blutströmung","Blutring der Dynastie","Amulett des Ewigen Blutes"]);
        Series("abyss_sunken_world",ItemRarity.Abyss,["Maske der Versunkenen Welt","Schattenpanzer der Tiefe","Klauen des Schattenmeers","Schritte der Verlorenen","Ring der Schattenflut","Amulett der Versunkenen Seele"]);
        Add("abyss_void_tide_ring","Ring der Leerenflut",ItemRarity.Abyss,EquipmentSlot.Ring1,new(){CritDamage=18,CritChance=12,BossDamage=10,Luck=10});
        Add("abyss_devourer_fang","Zahn des Verschlingers",ItemRarity.Abyss,EquipmentSlot.Ring1,new(){Power=28,DoubleAttackChance=12,AttackSpeed=10,BossDamage=12});
        Add("abyss_depth_eye","Auge der Tiefen",ItemRarity.Abyss,EquipmentSlot.Ring1,new(){Luck=16,CritChance=14,PearlBonus=12,FishingLuck=10});
        Add("abyss_blood_pearl_seal","Blutperlen-Siegel",ItemRarity.Abyss,EquipmentSlot.Ring1,new(){BossDamage=22,BossDamageReduction=10,BossLifeleech=8,MaximumHpPercent=10});
        Add("abyss_lost_world_ring","Ring der Versunkenen Welt",ItemRarity.Abyss,EquipmentSlot.Ring1,new(){AttackSpeed=15,DoubleAttackChance=12,Luck=12,BossDamage=12});
        Add("abyss_bone_ring","Knochenring der Tiefe",ItemRarity.Abyss,EquipmentSlot.Ring1,new(){MaximumHpPercent=20,ArmorPercent=12,BossDamageReduction=8,BossDamage=10});
        Add("abyss_void_heart","Herz der Leere",ItemRarity.Abyss,EquipmentSlot.Amulet,new(){MaximumHp=35,BossDamage=10,HealingPower=12,BossDamageReduction=12});
        Add("abyss_depthlord_soul","Seele des Tiefenlords",ItemRarity.Abyss,EquipmentSlot.Amulet,new(){Power=28,CritChance=14,CritDamage=12,AttackSpeed=10});
        Add("abyss_whisper","Flüstern der Abgründe",ItemRarity.Abyss,EquipmentSlot.Amulet,new(){Luck=16,PearlBonus=12,FishingLuck=12,BossDamage=10});
        Add("abyss_sunken_crown","Krone der Versunkenen",ItemRarity.Abyss,EquipmentSlot.Amulet,new(){MaximumHp=45,ArmorPercent=12,BossDamageReduction=10,BossDamage=10});
        Add("abyss_infinity_jellyfish","Qualle der Unendlichkeit",ItemRarity.Abyss,EquipmentSlot.Amulet,new(){AttackSpeed=18,CritChance=12,DoubleAttackChance=12,Luck=12});
        Add("abyss_endless_tide_amulet","Amulett der Endlosen Flut",ItemRarity.Abyss,EquipmentSlot.Amulet,new(){BossDamage=20,HealingPower=10,MaximumHpPercent=10,PearlBonus=10});
        Add("cursed_greed_crown","Krone der Gierflut",ItemRarity.Cursed,EquipmentSlot.Helmet,new(){CritChance=28,PearlBonus=18,Luck=10,HealingPower=-12,MaximumHpPercent=-10});
        Add("cursed_whisper_chest","Panzer des Flüsterabgrunds",ItemRarity.Cursed,EquipmentSlot.Chest,new(){BossDamage=40,MaximumHp=45,BossDamageReduction=12,BossDodgeChance=-16,MovementSpeed=-10});
        Add("cursed_broken_current_claws","Krallen der Gebrochenen Strömung",ItemRarity.Cursed,EquipmentSlot.Gloves,new(){Power=36,DoubleAttackChance=18,AttackSpeed=14,ArmorPercent=-12,HealingPower=-10});
        Add("cursed_restless_echo_boots","Stiefel des Ruhelosen Echos",ItemRarity.Cursed,EquipmentSlot.Boots,new(){AttackSpeed=24,MovementSpeed=18,BossDodgeChance=12,MaximumHpPercent=-15,BossDamageReduction=-10});
        Add("cursed_last_sacrifice_ring","Ring des Letzten Opfers",ItemRarity.Cursed,EquipmentSlot.Ring1,new(){Luck=24,DiveLuck=20,FishingLuck=18,BossDamageReduction=-12,CritChance=-10});
        Add("cursed_black_mercy_amulet","Amulett der Schwarzen Gnade",ItemRarity.Cursed,EquipmentSlot.Amulet,new(){HealingPower=28,BossDamage=18,MaximumHpPercent=12,CritChance=-10,AttackSpeed=-15});
        return items;
    }
    public static List<RpgItemSet> Sets()
    {
        var all=Items();var sets=new List<RpgItemSet>();
        void Set(string id,string name,params RpgSetBonus[] bonuses)=>sets.Add(new(){Id=id,Name=name,ItemIds=all.Where(i=>i.SetId==id).Select(i=>i.ItemId).ToList(),Bonuses=bonuses.ToList()});
        RpgSetBonus B(int count,string text,RpgAttributes? a=null)=>new(){RequiredPieces=count,Description=text,Attributes=a??new()};
        Set("true_eternal_reef","Essenz des Ewigen Riffs",B(2,"+15 % Perlenfund, +10 % Luck",new(){PearlBonus=15,Luck=10}),B(4,"15 % Chance auf einen zusätzlichen Fisch bei erfolgreichem Angeln"),B(6,"20 % zusätzliche Tauchbeute; 10 % zweiter normaler Beuteroll"));
        Set("true_deep_guardian","Herz der Tiefenwächter",B(2,"+16 % Schadensreduktion",new(){BossDamageReduction=16}),B(4,"Boss-Treffer: Schild über 20 % der maximalen HP, 20 Sekunden Abklingzeit"),B(6,"+30 % Leben, +25 % Boss-Schaden",new(){MaximumHpPercent=30,BossDamage=25}));
        Set("true_ancient_current","Gesang der Uralten Strömung",B(2,"+12 % Angriffstempo, +12 % Krit-Chance",new(){AttackSpeed=12,CritChance=12}),B(4,"Krits heilen 5 % maximale HP, höchstens alle 5 Sekunden"),B(6,"Alle 20 Sekunden: nächster eigener Angriff +200 % Angriffsschaden"));
        Set("abyss_devourer","Leere des Verschlingers",B(2,"+25 % Krit-Schaden",new(){CritDamage=25}),B(4,"Krits: 12 % Chance auf Leereexplosion mit 250 % Angriffsschaden, 8 Sekunden Abklingzeit"),B(6,"+40 % Boss-Schaden; Krits ignorieren 15 % Boss-Schadensreduktion",new(){BossDamage=40}));
        Set("abyss_blood_dynasty","Blut der Tiefsee-Dynastie",B(2,"+20 % Leben, +15 % Schadensreduktion",new(){MaximumHpPercent=20,BossDamageReduction=15}),B(4,"10 % des tatsächlich verursachten direkten Schadens als HP"),B(6,"Einmal pro Boss unter 30 % HP: 45 % maximale HP heilen, 10 Sekunden +35 % Angriffsschaden"));
        Set("abyss_sunken_world","Schatten der Versunkenen Welt",B(2,"+18 % Angriffstempo, +18 % Luck",new(){AttackSpeed=18,Luck=18}),B(4,"Krits: 15 % Chance auf Schattenangriff mit 120 % Schaden, 6 Sekunden Abklingzeit"),B(6,"Erfolgreicher Doppelangriff: +5 % Schaden für 8 Sekunden, maximal 5 Stapel"));
        return sets;
    }
}
