namespace RaidClipPlugin.Models;

public enum RpgActionKind { Craft, ActivateSushi, BuyPerk, Equip }

// Detached facts captured inside the successful account transaction, not inferred from balance.
public sealed record RpgActionDetails(RpgActionKind Kind, string Name, long Cost = 0,
    string FishName = "", int FishQuantity = 0, int Quantity = 0, int Level = 0,
    RpgAttributes? Bonuses = null, RpgActivity? Activity = null, int RemainingUses = 0,
    EquipmentSlot? Slot = null, long? NextCost = null, int MaximumLevel = 0,
    IReadOnlyList<RpgItemEffect>? Effects = null, string SetName = "", string IngredientsDescription="",string EffectDescription="",bool EncounterBased=false,int DurationSeconds=0);
