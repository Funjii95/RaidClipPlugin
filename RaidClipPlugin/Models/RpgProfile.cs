namespace RaidClipPlugin.Models;

public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }
public enum EquipmentSlot { Weapon, Helmet, Chest, Gloves, Boots, Ring1, Ring2, Amulet }
public enum RpgActivity { Dive, Fishing, Boss }
public enum RpgItemKind { Treasure, Equipment, Consumable }

public static class RarityPresentation
{
    public static string ColorHex(ItemRarity rarity) => rarity switch
    { ItemRarity.Common => "#9CA3AF", ItemRarity.Uncommon => "#22C55E", ItemRarity.Rare => "#3B82F6",
        ItemRarity.Epic => "#A855F7", ItemRarity.Legendary => "#EAB308", _ => "#9CA3AF" };
}

public sealed class RpgAttributes
{
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

public sealed class RpgFishStack
{
    public string FishId { get; set; } = "";
    public int Quantity { get; set; }
    public DateTimeOffset FirstCaughtAt { get; set; }
    public DateTimeOffset LastCaughtAt { get; set; }
}

public sealed class RpgBuff
{
    public string RecipeId { get; set; } = "";
    public RpgActivity Activity { get; set; }
    public int RemainingUses { get; set; } = 3;
    public RpgAttributes Attributes { get; set; } = new();
}

public sealed class RpgProfile
{
    public int SchemaVersion { get; set; } = 1;
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
