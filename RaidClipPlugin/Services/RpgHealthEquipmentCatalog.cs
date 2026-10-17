using RaidClipPlugin.Models;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public static class RpgHealthEquipmentCatalog
{
    public static IReadOnlyList<RpgItem> Items() => new RpgItem[]
    {
        Item("hp-kelp-boots","Seetangstiefel",ItemRarity.Common,EquipmentSlot.Boots,5),
        Item("hp-coral-helmet","Korallenhelm",ItemRarity.Uncommon,EquipmentSlot.Helmet,10),
        Item("hp-shell-chest","Muschelpanzer",ItemRarity.Rare,EquipmentSlot.Chest,20),
        Item("hp-abyss-chest","Abgrundrüstung",ItemRarity.Epic,EquipmentSlot.Chest,35),
        Item("hp-leviathan-amulet","Leviathans Herz",ItemRarity.Legendary,EquipmentSlot.Amulet,50)
    };
    private static RpgItem Item(string id,string name,ItemRarity rarity,EquipmentSlot slot,int hp) => new()
    {
        ItemId=id,DisplayName=name,Rarity=rarity,Slot=slot,Kind=RpgItemKind.Equipment,
        Attributes=new(){MaximumHp=hp},Description="Erhöht angelegt die maximalen Lebenspunkte. Anlegen heilt nicht.",
        DropActivities=new(){RpgActivity.Dive,RpgActivity.Fishing,RpgActivity.Boss}
    };
    public static void AddMissing(RpgLootConfig config)
    {
        foreach(var item in Items()) if(!config.Items.Any(i=>i.ItemId==item.ItemId)) config.Items.Add(item);
    }
}
