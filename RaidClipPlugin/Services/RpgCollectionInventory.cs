using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// Collection instances keep their existing identities and passive bonuses. They are
// presented separately, without creating a second authoritative inventory.
public static class RpgCollectionInventory
{
    public const int Limit = 5;
    public static bool IsCollectible(RpgItem item) =>
        item.Kind == RpgItemKind.Treasure && item.Slot is null && item.Collector is not null;

    public static int SalvageOverflow(ViewerPointEntry account, RpgSalvageConfig config, long maximumBalance = long.MaxValue)
    {
        if (!config.Enabled) return 0;
        var removed = 0;
        foreach (var group in account.Rpg.Inventory.Where(IsCollectible).GroupBy(i => i.ItemId).ToArray())
        {
            // Keep protected instances first. Explicit locks/favorites always win.
            var overflow = group.OrderByDescending(i => i.IsLocked || i.IsFavorite ||
                    account.Rpg.Equipment.Values.Contains(i.InstanceId))
                .ThenBy(i => i.FoundAt).ThenBy(i => i.InstanceId, StringComparer.Ordinal)
                .Skip(Limit).ToArray();
            foreach (var item in overflow)
            {
                if (item.IsLocked || item.IsFavorite || item.Rarity == ItemRarity.Divine ||
                    account.Rpg.Equipment.Values.Contains(item.InstanceId)) continue;
                var value = RpgSalvageService.Value(item, config);
                // Never remove an item if its full salvage proceeds cannot be credited.
                if (value > maximumBalance || account.Points > maximumBalance - value) continue;
                account.Points += value;
                account.Rpg.Inventory.Remove(item);
                removed++;
            }
        }
        return removed;
    }
}
