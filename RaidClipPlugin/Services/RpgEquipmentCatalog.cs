using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgEquipmentCatalog
{
    public static List<RpgItemSet> Sets() => new()
    {
        new() { Id = "deepsea-guardian", Name = "Wächter der Tiefsee", Description = "Präzision und Stärke gegen Meeresbosse.",
            ItemIds = new() { "deepsea-crown", "deepsea-chest", "deepsea-grip", "deepsea-boots" },
            Bonuses = new() { new() { RequiredPieces = 2, Attributes = new() { BossDamage = 5 } },
                new() { RequiredPieces = 3, Attributes = new() { CritChance = 3 } }, new() { RequiredPieces = 4, Attributes = new() { DoubleAttackChance = 5 } } } },
        new() { Id = "ocean-treasure-hunter", Name = "Schatzjäger des Ozeans", Description = "Glück bei Abenteuern und geeigneten Belohnungen.",
            ItemIds = new() { "ocean-mask", "ocean-garment", "ocean-fingers", "ocean-flippers" },
            Bonuses = new() { new() { RequiredPieces = 2, Attributes = new() { DiveLuck = 5 } },
                new() { RequiredPieces = 3, Attributes = new() { FishingLuck = 5 } }, new() { RequiredPieces = 4, Attributes = new() { PearlBonus = 5 } } } }
    };
    public static List<RpgItem> Items()
    {
        RpgAttributeRange A(string attribute, int min, int max) => new() { Attribute = attribute, Minimum = min, Maximum = max };
        RpgItem Part(string id, string name, EquipmentSlot slot, string set, string description, params RpgAttributeRange[] ranges) => new()
        {
            ItemId = id, DisplayName = name, Slot = slot, Kind = RpgItemKind.Equipment, Rarity = ItemRarity.Epic, SetId = set, Description = description,
            DropActivities = set == "deepsea-guardian" ? new() { RpgActivity.Dive, RpgActivity.Boss } : new() { RpgActivity.Dive, RpgActivity.Fishing },
            Generation = new() { AttributeCount = ranges.Length, GenerateEffect = false, Attributes = ranges.ToList() }
        };
        RpgItem Legendary(string id, string name, EquipmentSlot slot, RpgAttributes attributes, RpgEffectTrigger trigger, decimal chance, decimal strength = 0.5m, bool critical = false) => new()
        {
            ItemId = id, DisplayName = name, Slot = slot, Kind = RpgItemKind.Equipment, Rarity = ItemRarity.Legendary, Attributes = attributes,
            DropActivities = new() { RpgActivity.Dive, RpgActivity.Boss },
            Effects = new() { new() { Id = id + "-effect", Name = name, Trigger = trigger, Chance = chance, Strength = strength, RequiresCritical = critical,
                Activity = trigger == RpgEffectTrigger.ExtraLoot ? RpgActivity.Dive : trigger == RpgEffectTrigger.SushiPreservation ? null : RpgActivity.Boss } }
        };
        return new()
        {
            Part("deepsea-crown", "Krone des Abgrunds", EquipmentSlot.Helmet, "deepsea-guardian", "Die Krone verstärkt deine Präzision und erhöht deinen Schaden gegen mächtige Meereskreaturen.", A("CritChance",3,5), A("BossDamage",4,6), A("Luck",2,5)),
            Part("deepsea-chest", "Panzer des Leviathans", EquipmentSlot.Chest, "deepsea-guardian", "Eine mächtige Rüstung, die deinen Angriffsschaden und deine Stärke gegen Bosse erhöht.", A("Damage",6,10), A("BossDamage",5,8), A("CritDamage",10,15)),
            Part("deepsea-grip", "Griff der Tiefsee", EquipmentSlot.Gloves, "deepsea-guardian", "Diese Handschuhe ermöglichen zusätzliche Treffer während eines Bosskampfes.", A("CritChance",3,5), A("DoubleAttackChance",4,7), A("Damage",3,5)),
            Part("deepsea-boots", "Schritte des Abgrunds", EquipmentSlot.Boots, "deepsea-guardian", "Die Schuhe unterstützen präzise Angriffe und erleichtern gefährliche Tauchgänge.", A("BossDamage",3,5), A("CritChance",2,4), A("DiveLuck",2,4)),
            Part("ocean-mask", "Maske des Perlensuchers", EquipmentSlot.Helmet, "ocean-treasure-hunter", "Spürt verborgene Schätze auf.", A("Luck",4,7), A("DiveLuck",3,5), A("ExtraLootChance",2,3)),
            Part("ocean-garment", "Gewand des Schatzsuchers", EquipmentSlot.Chest, "ocean-treasure-hunter", "Glück und reichere Abenteuerbeute.", A("Luck",3,5), A("FishingLuck",4,6), A("PearlBonus",2,4)),
            Part("ocean-fingers", "Finger des Glücks", EquipmentSlot.Gloves, "ocean-treasure-hunter", "Geschick bei Fang und Schatzsuche.", A("DiveLuck",4,6), A("FishingLuck",3,5), A("PearlBonus",2,4)),
            Part("ocean-flippers", "Flossen des Entdeckers", EquipmentSlot.Boots, "ocean-treasure-hunter", "Begleiter für weite Tauchgänge.", A("DiveLuck",5,8), A("FishingLuck",3,5), A("Luck",2,4)),
            Legendary("leviathan-grip", "Leviathans Griff", EquipmentSlot.Gloves, new() { Damage=8, CritChance=4, CritDamage=15, BossDamage=5 }, RpgEffectTrigger.DoubleAttack,10),
            Legendary("tidal-seal", "Siegel der Gezeiten", EquipmentSlot.Ring1, new() { CritChance=5, CritDamage=10, Luck=5 }, RpgEffectTrigger.DoubleAttack,3,critical:true),
            Legendary("deepsea-heart", "Herz der Tiefsee", EquipmentSlot.Amulet, new() { Luck=7, DiveLuck=5, FishingLuck=5 }, RpgEffectTrigger.SushiPreservation,8),
            Legendary("sunken-king-crown", "Krone des versunkenen Königs", EquipmentSlot.Helmet, new() { Luck=8, DiveLuck=7, FishingLuck=5 }, RpgEffectTrigger.ExtraLoot,5),
            Legendary("leviathan-trident", "Dreizack des Leviathans", EquipmentSlot.Weapon, new() { Damage=10, CritChance=5, CritDamage=20, BossDamage=8 }, RpgEffectTrigger.TripleAttack,2,0.35m)
        };
    }
    public static RpgConfig Prepare(RpgConfig current)
    {
        var copy = JsonSerializer.Deserialize<RpgConfig>(JsonSerializer.Serialize(current))!;
        foreach (var set in Sets()) if (!copy.Sets.Any(s => s.Id == set.Id)) copy.Sets.Add(set);
        foreach (var item in Items()) if (!copy.DiveLoot.Items.Any(i => i.ItemId == item.ItemId)) copy.DiveLoot.Items.Add(item);
        copy.Validate(); return copy;
    }
}
