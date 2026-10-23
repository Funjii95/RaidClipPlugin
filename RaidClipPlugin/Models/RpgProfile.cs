namespace RaidClipPlugin.Models;

public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary, True, Cursed, Abyss }
public enum EquipmentSlot { Weapon, Helmet, Chest, Gloves, Boots, Ring1, Ring2, Amulet }
public enum RpgActivity { Dive, Fishing, Boss }
public enum RpgItemKind { Treasure, Equipment, Consumable }
public enum SushiCategory { Offensive, Defensive }

public static class RarityPresentation
{
    public static string ColorHex(ItemRarity rarity) => rarity switch
    { ItemRarity.Common => "#9CA3AF", ItemRarity.Uncommon => "#22C55E", ItemRarity.Rare => "#3B82F6",
        ItemRarity.Epic => "#A855F7", ItemRarity.Legendary => "#EAB308", ItemRarity.True => "#9BFF76", ItemRarity.Cursed => "#DC55FF", ItemRarity.Abyss => "#E51D48", _ => "#9CA3AF" };
}

public sealed class RpgAttributes
{
    public decimal MovementSpeed { get; set; }
    public decimal BossLifeleech { get; set; }
    public decimal Armor { get; set; }
    public decimal ArmorPercent { get; set; }
    public decimal MaximumHpPercent { get; set; }
    public decimal AttackSpeed { get; set; }
    public decimal HealingPower { get; set; }
    public decimal BossDamageReduction { get; set; }
    public decimal BossDodgeChance { get; set; }
    public decimal CriticalHealing { get; set; }
    public decimal RareEquipmentLuck { get; set; }
    public decimal Power { get; set; }
    public decimal Stamina { get; set; }
    public decimal MaximumHp { get; set; }
    public decimal DoubleAttackChance { get; set; }
    public decimal TripleAttackChance { get; set; }
    public decimal ExtraLootChance { get; set; }
    public decimal SushiPreservationChance { get; set; }
    public decimal Damage { get; set; }
    public decimal CritChance { get; set; }
    public decimal CritDamage { get; set; }
    public decimal Luck { get; set; }
    public decimal BossDamage { get; set; }
    public decimal PearlBonus { get; set; }
    public decimal FishingLuck { get; set; }
    public decimal DiveLuck { get; set; }
}

public sealed class RpgItem
{
    public int UpgradeLevel { get; set; }
    public bool IsRefined { get; set; }
    public int DropWeight { get; set; } = 1;
    public RpgCollectorBonuses? Collector { get; set; }
    public int CoreStatsVersion { get; set; }
    public long? SalvageValue { get; set; }
    public bool IsLocked { get; set; }
    public bool IsFavorite { get; set; }
    public string SetId { get; set; } = "";
    public List<RpgActivity> DropActivities { get; set; } = new() { RpgActivity.Dive };
    public List<RpgItemEffect> Effects { get; set; } = new();
    // Definition-only generation rules. Cleared on generated instances; never applied on load.
    public RpgItemGeneration? Generation { get; set; }
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconId { get; set; } = "";
    public ItemRarity Rarity { get; set; }
    public RpgItemKind Kind { get; set; }
    public string ConsumableRecipeId { get; set; } = "";
    public EquipmentSlot? Slot { get; set; }
    public RpgAttributes Attributes { get; set; } = new();
    public DateTimeOffset FoundAt { get; set; }
}

// Deliberately separate from combat attributes: inventory possession cannot grant combat stats.
public sealed class RpgCollectorBonuses
{
    public decimal FishingPearls { get; set; }
    public decimal DivePearls { get; set; }
    public decimal BossPearls { get; set; }
    public decimal BossExperience { get; set; }
    public decimal TreasureChance { get; set; }
    public decimal RareTreasureChance { get; set; }
}

public sealed class RpgFishStack
{
    public string FishId { get; set; } = "";
    public int Quantity { get; set; }
    public DateTimeOffset FirstCaughtAt { get; set; }
    public DateTimeOffset LastCaughtAt { get; set; }
}

public sealed class RpgBuff
{
    public SushiCategory Category { get; set; }
    public bool EncounterBased { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
    public string LastBossId { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public decimal PassiveHealing { get; set; }
    public int ChatHealing { get; set; }
    public decimal BossDamageReduction { get; set; }
    public decimal BossLifeleech { get; set; }
    public string RecipeId { get; set; } = "";
    public RpgActivity Activity { get; set; }
    public int RemainingUses { get; set; } = 3;
    public RpgAttributes Attributes { get; set; } = new();
}

public sealed class RpgProfile
{
    public Dictionary<string, int> TalentRanks { get; set; } = new();
    public int SushiVersion { get; set; }
    public int PlayerLevel { get; set; } = 1;
    public long Experience { get; set; }
    public RpgHealthState Health { get; set; } = new();
    public int SchemaVersion { get; set; } = 1;
    public RpgMaterials Materials { get; set; } = new();
    public bool LegacyFishImported { get; set; }
    public List<RpgItem> Inventory { get; set; } = new();
    public Dictionary<EquipmentSlot, string> Equipment { get; set; } = new();
    public Dictionary<string, int> Perks { get; set; } = new();
    public Dictionary<string, RpgFishStack> Fish { get; set; } = new();
    public Dictionary<string, int> Sushi { get; set; } = new();
    public List<RpgBuff> Buffs { get; set; } = new();
    public Dictionary<string, DateTimeOffset> Cooldowns { get; set; } = new();
    // Tokens are retained: retrying an old successful request must never award twice.
    public HashSet<string> CompletedActions { get; set; } = new();
}

public sealed class RpgHealthState
{
    public int SetShieldHp { get; set; }
    public string SetShieldSource { get; set; } = "";
    // Nullable distinguishes legacy profiles from an injured player at zero HP.
    public int? CurrentHp { get; set; }
    public int AppliedMaximumHp { get; set; }
    public int? AppliedBaseMaximumHp { get; set; }
    public DateTimeOffset? LastPassiveRegen { get; set; }
    public decimal SushiRegenRemainder { get; set; }
    public DateTimeOffset? LastChatHeal { get; set; }
    public string BossId { get; set; } = "";
    public bool Defeated { get; set; }
    public long BossDamage { get; set; }
    public int BossRemainingHp { get; set; }
}
