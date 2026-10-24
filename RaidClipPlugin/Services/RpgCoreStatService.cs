using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Versioned equipment migration; never rerolls existing secondary stats.</summary>
public static class RpgCoreStatService
{
    public static (int Power,int Stamina) Baseline(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common=>(1,1),ItemRarity.Uncommon=>(2,2),ItemRarity.Rare=>(3,4),
        ItemRarity.Epic=>(5,7),ItemRarity.Legendary or ItemRarity.True or ItemRarity.Cursed or ItemRarity.Abyss=>(8,10),ItemRarity.Eternal=>(12,16),ItemRarity.Divine=>(50,50),_=>throw new InvalidOperationException("Unbekannte Seltenheit.")
    };
    public static bool Apply(RpgItem item)
    {
        if(item.Slot is null || item.Kind==RpgItemKind.Consumable)return false;
        var armor=item.Slot is EquipmentSlot.Helmet or EquipmentSlot.Chest or EquipmentSlot.Gloves or EquipmentSlot.Boots;
        if(item.CoreStatsVersion>=3)return false;
        var baseline=Baseline(item.Rarity);
        if(item.Attributes.Power<=0)item.Attributes.Power=baseline.Power;
        if(item.Attributes.Stamina<=0)item.Attributes.Stamina=baseline.Stamina;
        // Version 2 added fifty percentage points by mistake. Remove only that
        // known addition; legitimate percentage rolls remain unchanged.
        if(armor&&item.CoreStatsVersion==2)item.Attributes.ArmorPercent-=50;
        if(armor&&item.Attributes.Armor<=0)item.Attributes.Armor=baseline.Stamina*2;
        if(item.Attributes.Armor>0)item.Attributes.Armor*=1.5m;
        item.Attributes.Power*=1.5m;item.Attributes.Stamina*=1.5m;
        item.CoreStatsVersion=3;
        return true;
    }
    public static void Upgrade(RpgProfile profile)
    {foreach(var item in profile.Inventory)Apply(item);}
}
