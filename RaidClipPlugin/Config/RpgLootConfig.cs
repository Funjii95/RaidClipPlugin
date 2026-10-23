using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin.Config;

public sealed class RpgLootConfig
{
    public RpgLootConfig()
    {
        foreach (var item in Items.Where(i => i.Slot.HasValue))
            item.Generation = RpgItemGenerationService.DefaultRules(item.Slot!.Value, item.Rarity);
        Items.AddRange(RpgEquipmentCatalog.Items());
        RpgHealthEquipmentCatalog.AddMissing(this);
        RpgLootExpansionCatalog.AddMissing(this);
        RpgTidesExpansion.AddMissing(this);
        RpgDepthsExpansion.AddMissing(this);
        RpgEternalCatalog.AddMissing(this);
    }
    public List<RpgItemEffect> Effects { get; set; } = new()
    {
        new() { Id = "double", Name = "Zorn der Strömung", Trigger = RpgEffectTrigger.DoubleAttack, Chance = 4, Activity = RpgActivity.Boss },
        new() { Id = "triple", Name = "Zorn des Leviathans", Trigger = RpgEffectTrigger.TripleAttack, Chance = 2, Activity = RpgActivity.Boss },
        new() { Id = "extra-loot", Name = "Schatzgespür", Trigger = RpgEffectTrigger.ExtraLoot, Chance = 3, Activity = RpgActivity.Dive },
        new() { Id = "sushi-save", Name = "Sparsame Pfoten", Trigger = RpgEffectTrigger.SushiPreservation, Chance = 5 }
    };
    public decimal BaseDropChance { get; set; } = 25;
    public decimal CollectorPearlCap { get; set; } = 15;
    public decimal CollectorChanceCap { get; set; } = 10;
    public decimal CollectorExperienceCap { get; set; } = 10;
    public decimal CollectorPoolChance { get; set; } = 15;
    public decimal MaximumDropChance { get; set; } = 60;
    public decimal LuckChancePerPoint { get; set; } = 0.1m;
    public decimal MaximumLuckBonus { get; set; } = 10;
    public decimal EventBonus { get; set; }
    public decimal MaximumEventBonus { get; set; } = 10;
    public long LegendaryMinimumStake { get; set; } = 10000;
    public List<RpgRiskTier> RiskTiers { get; set; } = new()
    {
        new() { MinimumStake = 0, MaximumRarity = ItemRarity.Uncommon },
        new() { MinimumStake = 250, MaximumRarity = ItemRarity.Rare, DropBonus = 4 },
        new() { MinimumStake = 1000, MaximumRarity = ItemRarity.Epic, DropBonus = 8 },
        new() { MinimumStake = 5000, MaximumRarity = ItemRarity.Epic, DropBonus = 12 },
        new() { MinimumStake = 10000, MaximumRarity = ItemRarity.Legendary, DropBonus = 15 }
    };
    public Dictionary<ItemRarity, int> RarityWeights { get; set; } = new()
    { [ItemRarity.Common] = 6000, [ItemRarity.Uncommon] = 2900, [ItemRarity.Rare] = 850,
        [ItemRarity.Epic] = 200, [ItemRarity.Legendary] = 50 };
    public List<RpgItem> Items { get; set; } = new()
    {
        new() { ItemId = "shell", DisplayName = "Alte Muschel", Rarity = ItemRarity.Common, IconId = "shell" },
        new() { ItemId = "gloves", DisplayName = "Otterhandschuhe", Rarity = ItemRarity.Common, Kind = RpgItemKind.Equipment,
            Slot = EquipmentSlot.Gloves, Attributes = new() { Damage = 1 }, IconId = "gloves" },
        new() { ItemId = "trident", DisplayName = "Dreizack der Strömung", Rarity = ItemRarity.Uncommon, Kind = RpgItemKind.Equipment,
            Slot = EquipmentSlot.Weapon, Attributes = new() { Damage = 3 }, IconId = "trident" },
        new() { ItemId = "boots", DisplayName = "Flossenschuhe", Rarity = ItemRarity.Uncommon, Kind = RpgItemKind.Equipment,
            Slot = EquipmentSlot.Boots, Attributes = new() { DiveLuck = 2 }, IconId = "boots" },
        new() { ItemId = "helmet", DisplayName = "Perlenhelm", Rarity = ItemRarity.Rare, Kind = RpgItemKind.Equipment,
            Slot = EquipmentSlot.Helmet, Attributes = new() { FishingLuck = 3 }, IconId = "helmet" },
        new() { ItemId = "chest", DisplayName = "Rüstung der Tiefe", Rarity = ItemRarity.Epic, Kind = RpgItemKind.Equipment,
            Slot = EquipmentSlot.Chest, Attributes = new() { BossDamage = 5 }, IconId = "chest" },
        new() { ItemId = "amulet", DisplayName = "Amulett der Gezeiten", Rarity = ItemRarity.Legendary, Kind = RpgItemKind.Equipment,
            Slot = EquipmentSlot.Amulet, Attributes = new() { PearlBonus = 5, Luck = 5 }, IconId = "amulet" },
        new() { ItemId = "sushi-fighter", DisplayName = "Sushi des Kämpfers", Rarity = ItemRarity.Rare, Kind = RpgItemKind.Consumable,
            ConsumableRecipeId = "fighter", IconId = "sushi-fighter" },
        new() { ItemId = "royal-treasure", DisplayName = "Versunkener Königsschatz", Rarity = ItemRarity.Epic, IconId = "royal-treasure" },
        new() { ItemId = "current-ring", DisplayName = "Ring der Strömung", Rarity = ItemRarity.Uncommon, Slot = EquipmentSlot.Ring1,
            Attributes = new() { CritChance = 1 }, IconId = "current-ring" },
        new() { ItemId = "ocean-ring", DisplayName = "Ring der kleinen Strömung", Rarity = ItemRarity.Rare, Slot = EquipmentSlot.Ring1,
            Attributes = new() { CritChance = 2 }, IconId = "ocean-ring" },
        new() { ItemId = "deep-ring", DisplayName = "Ring des Tiefenmeeres", Rarity = ItemRarity.Epic, Slot = EquipmentSlot.Ring1,
            Attributes = new() { CritChance = 4, Luck = 5 }, IconId = "deep-ring" },
        new() { ItemId = "leviathan-seal", DisplayName = "Leviathans Siegel", Rarity = ItemRarity.Legendary, Slot = EquipmentSlot.Ring1,
            Attributes = new() { CritChance = 6, CritDamage = 15, BossDamage = 5 }, IconId = "leviathan-seal" }
    };

    public void Validate()
    {
        if (CollectorPearlCap is <0 or >100 || CollectorChanceCap is <0 or >100 || CollectorExperienceCap is <0 or >100 || CollectorPoolChance is <0 or >100 ||
            Items.Any(i=>i.DropWeight is <1 or >1000000 || i.Collector is not null && (i.Kind!=RpgItemKind.Treasure || i.Slot.HasValue ||
                typeof(RpgCollectorBonuses).GetProperties().Any(p=>(decimal)p.GetValue(i.Collector)! is <0 or >100))))
            throw new InvalidOperationException("Ungültige Sammlerschatz-/Poolkonfiguration.");
        RpgItemGenerationService.Validate(this);
        if (BaseDropChance is < 0 or > 100 || MaximumDropChance is < 0 or > 100 ||
            LuckChancePerPoint is < 0 or > 10 || MaximumLuckBonus is < 0 or > 100 ||
            MaximumEventBonus is < 0 or > 100 || LegendaryMinimumStake < 1 ||
            RiskTiers.Count == 0 || !RiskTiers.Any(t => t.MinimumStake == 0) ||
            RiskTiers.Select(t => t.MinimumStake).Distinct().Count() != RiskTiers.Count ||
            RiskTiers.Any(t => t.MinimumStake < 0 || !Enum.IsDefined(t.MaximumRarity) || t.DropBonus is < 0 or > 100) ||
            RarityWeights.Any(p => p.Value is < 0 or > 1000000 || !Enum.IsDefined(p.Key)) ||
            Items.Select(i => i.ItemId).Distinct().Count() != Items.Count ||
            Items.Any(i => string.IsNullOrWhiteSpace(i.ItemId) || !Enum.IsDefined(i.Rarity) || !Enum.IsDefined(i.Kind) || i.Slot.HasValue && !Enum.IsDefined(i.Slot.Value)))
            throw new InvalidOperationException("Ungültige RPG-Lootkonfiguration.");
    }
}

public sealed class RpgRiskTier
{
    public long MinimumStake { get; set; }
    public ItemRarity MaximumRarity { get; set; }
    public decimal DropBonus { get; set; }
}
