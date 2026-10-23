using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin.Config;

public sealed class RpgConfig
{
    public RpgUpgradeConfig Upgrades {get;set;}=new();
    public RpgConfig() { RpgSushiService.AddMissing(this); RpgPerkCatalog.AddMissing(this); }
    public long FreeFishingReward { get; set; } = 40;
    public decimal PowerDamagePerPoint { get; set; } = 1;
    private RpgHealthConfig _health = new();
    public RpgHealthConfig Health { get { _health.EquipmentSets = Sets; return _health; } set => _health=value; }
    public RpgMimicConfig Mimic {get;set;}=new();
    public RpgEnemyConfig Enemies { get; set; } = new();
    public RpgHotTimeConfig HotTime { get; set; } = new();
    public RpgSalvageConfig Salvage { get; set; } = new();
    public List<RpgItemSet> Sets { get; set; } = Services.RpgEquipmentCatalog.Sets().Concat(RpgLootExpansionCatalog.Sets()).Concat(RpgTidesExpansion.Sets()).Concat(RpgDepthsExpansion.Sets()).ToList();
    public decimal BossLootChance { get; set; } = 2;
    public bool BossLegendaryEnabled { get; set; }
    public int BossLegendaryMinimumHp { get; set; } = 1000;
    public int BossLegendaryMinimumPlayers { get; set; } = 3;
    public decimal FishingEquipmentChance { get; set; } = 0.5m;
    public void Validate()
    {
        Upgrades.Validate();
        if(PowerDamagePerPoint is <0 or >10)throw new InvalidOperationException("Kraft-Schadensbonus muss zwischen 0 und 10 Prozent je Punkt liegen.");
        Health.Validate();
        Enemies.Validate();
        Mimic.Validate();
        RpgSushiService.Validate(this);
        DiveLoot.Validate();
        Salvage.Validate();
        HotTime.Validate();
        if (BossLootChance is < 0 or > 100 || FishingEquipmentChance is < 0 or > 100 || BossLegendaryMinimumHp < 1 || BossLegendaryMinimumPlayers < 1 ||
            Sets.Select(s => s.Id).Distinct().Count() != Sets.Count || Sets.Any(s => string.IsNullOrWhiteSpace(s.Id) || s.ItemIds.Count == 0 ||
                s.ItemIds.Distinct().Count() != s.ItemIds.Count || s.Bonuses.Any(b => b.RequiredPieces < 1 || b.RequiredPieces > s.ItemIds.Count)))
            throw new InvalidOperationException("Ungültige Sets oder Belohnungs-Lootregeln.");
        if (DoubleAttackChanceCap is < 0 or > 20 || TripleAttackChanceCap is < 0 or > 5 || ExtraLootChanceCap is < 0 or > 10 ||
            SushiPreservationChanceCap is < 0 or > 20 || PearlBonusCap is < 0 or > 15)
            throw new InvalidOperationException("Ungültige RPG-Effektcaps.");
        if (BaseCritMultiplier is < 1 or > 10 || CritChanceCap is < 0 or > 100 || CritDamageCap is < 0 or > 1000 || LuckCap is < 0 or > 1000 ||
            DamageBonusCap is < 0 or > 1000 || BuffUses is < 1 or > 100 || MaximumDifferentBuffs is < 1 or > 10 ||
            Perks.Select(p => p.Id).Distinct().Count() != Perks.Count ||
            Recipes.Select(p => p.Id).Distinct().Count() != Recipes.Count ||
            FishSpecies.Select(p => p.Id).Distinct().Count() != FishSpecies.Count ||
            FishSpecies.Count is < 1 or > 100 || FishSpecies.Any(f => string.IsNullOrWhiteSpace(f.Id) || f.Weight is < 0 or > 1000000 || !Enum.IsDefined(f.Rarity)) ||
            !FishSpecies.Any(f => f.Weight > 0) ||
            Recipes.Any(r => string.IsNullOrWhiteSpace(r.Id) || r.FishQuantity < 1 || r.PearlCost < 0 || !Enum.IsDefined(r.Activity) || !FishSpecies.Any(f => f.Id == r.FishId)) ||
            DiveLoot.Items.Any(i => i.Kind == RpgItemKind.Consumable && !Recipes.Any(r => r.Id == i.ConsumableRecipeId)))
            throw new InvalidOperationException("Ungültige RPG-Konfiguration.");
        foreach (var perk in Perks)
        {
            if (string.IsNullOrWhiteSpace(perk.Id)) throw new InvalidOperationException("Perk-ID fehlt.");
            _ = perk.Cost(0);
            _ = perk.Cost(perk.MaximumLevel - 1);
        }
    }
    // Opt-in until all gameplay adapters and administrative controls are integrated.
    public bool Enabled { get; set; }
    public decimal DoubleAttackChanceCap { get; set; } = 20;
    public decimal TripleAttackChanceCap { get; set; } = 5;
    public decimal ExtraLootChanceCap { get; set; } = 10;
    public decimal SushiPreservationChanceCap { get; set; } = 20;
    public decimal PearlBonusCap { get; set; } = 15;
    public RpgLootConfig DiveLoot { get; set; } = new();
    public string LegendaryMessage { get; set; } = "🦦💎 LEGENDÄR! @{user} hat {item} gefunden!";
    public List<RpgFishDefinition> FishSpecies { get; set; } = new()
    {
        new() { Id = "sardine", Name = "Kleine Sardine", Weight = 6500 },
        new() { Id = "perch", Name = "Silberbarsch", Rarity = ItemRarity.Uncommon, Weight = 2500 },
        new() { Id = "crystal-carp", Name = "Kristallkarpfen", Rarity = ItemRarity.Rare, Weight = 800 },
        new() { Id = "eel", Name = "Tiefsee-Aal", Rarity = ItemRarity.Epic, Weight = 190 },
        new() { Id = "leviathan", Name = "Goldener Leviathan", Rarity = ItemRarity.Legendary, Weight = 10 }
    };
    public decimal CritChanceCap { get; set; } = 50;
    public decimal BaseCritMultiplier { get; set; } = 1.5m;
    public decimal CritDamageCap { get; set; } = 300;
    public decimal LuckCap { get; set; } = 100;
    public decimal DamageBonusCap { get; set; } = 200;
    public int BuffUses { get; set; } = 3;
    public int MaximumDifferentBuffs { get; set; } = 3;
    public List<RpgPerkDefinition> Perks { get; set; } = new()
    {
        new() { Id = "damage", BonusPerLevel = new() { Damage = 2 } },
        new() { Id = "crit", BonusPerLevel = new() { CritChance = 1 } },
        new() { Id = "critdamage", BonusPerLevel = new() { CritDamage = 5 } },
        new() { Id = "luck", BonusPerLevel = new() { Luck = 2 } },
        new() { Id = "boss", BonusPerLevel = new() { BossDamage = 2 } },
        new() { Id = "dive", BonusPerLevel = new() { DiveLuck = 2 } },
        new() { Id = "fishing", BonusPerLevel = new() { FishingLuck = 2 } }
    };
    public List<RpgRecipe> Recipes { get; set; } = new()
    {
        new() { Id = "fishing", FishId = "sardine", PearlCost = 250, Activity = RpgActivity.Fishing, Attributes = new() { FishingLuck = 5 } },
        new() { Id = "fighter", FishId = "perch", PearlCost = 500, Activity = RpgActivity.Boss, Attributes = new() { Damage = 10 } },
        new() { Id = "critical", FishId = "crystal-carp", PearlCost = 1000, Activity = RpgActivity.Boss, Attributes = new() { CritChance = 5 } },
        new() { Id = "dive", FishId = "eel", PearlCost = 1500, Activity = RpgActivity.Dive, Attributes = new() { DiveLuck = 5 } }
    };
}

public sealed class RpgFishDefinition
{
    public long? PearlReward { get; set; }
    public long MinimumStake { get; set; }
    public string Zone { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Meer";
    public string IconId { get; set; } = "";
    public ItemRarity Rarity { get; set; }
    public int Weight { get; set; }
}

public sealed class RpgPerkDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    private int _maximumLevel = 5;
    public int MaximumLevel { get => Math.Clamp(_maximumLevel,1,5); set => _maximumLevel=value; }
    public long BaseCost { get; set; } = 500;
    public decimal CostGrowth { get; set; } = 2;
    public RpgAttributes BonusPerLevel { get; set; } = new();
    public long Cost(int currentLevel)
    {
        if (MaximumLevel is < 1 or > 100 || currentLevel < 0 || currentLevel >= MaximumLevel || BaseCost < 1 || CostGrowth is <= 1 or > 10)
            throw new InvalidOperationException("Ungültige Perk-Stufe oder Kostenkonfiguration.");
        decimal cost = BaseCost;
        for (var i = 0; i < currentLevel; i++) cost = checked(cost * CostGrowth);
        return checked((long)decimal.Ceiling(cost));
    }
}

public sealed class RpgRecipe
{
    public SushiCategory Category { get; set; }
    public Dictionary<string,int> Ingredients { get; set; } = new();
    public int BossUses { get; set; }
    public decimal PassiveHealing { get; set; }
    public int ChatHealing { get; set; }
    public decimal BossDamageReduction { get; set; }
    public decimal BossLifeleech { get; set; }
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string FishId { get; set; } = "";
    public int FishQuantity { get; set; } = 1;
    public long PearlCost { get; set; } = 100;
    public RpgActivity Activity { get; set; }
    public RpgAttributes Attributes { get; set; } = new();
}
