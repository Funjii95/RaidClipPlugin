using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Additive, deterministic equipment upgrade; never rerolls existing secondary stats.</summary>
public static class RpgCoreStatService
{
    public static (int Power,int Stamina) Baseline(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common=>(1,1),ItemRarity.Uncommon=>(2,2),ItemRarity.Rare=>(3,4),
        ItemRarity.Epic=>(5,7),ItemRarity.Legendary or ItemRarity.True or ItemRarity.Cursed or ItemRarity.Abyss=>(8,10),_=>throw new InvalidOperationException("Unbekannte Seltenheit.")
    };
    public static bool Apply(RpgItem item)
    {
        if(item.Slot is null || item.Kind==RpgItemKind.Consumable || item.CoreStatsVersion>=1)return false;
        var baseline=Baseline(item.Rarity);
        if(item.Attributes.Power<=0)item.Attributes.Power=baseline.Power;
        if(item.Attributes.Stamina<=0)item.Attributes.Stamina=baseline.Stamina;
        item.CoreStatsVersion=1;return true;
    }
    public static void Upgrade(RpgProfile profile)
    {foreach(var item in profile.Inventory)Apply(item);}
}
