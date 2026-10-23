using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgSetService
{
    public static IEnumerable<RpgSetProgress> Progress(RpgProfile profile, RpgConfig config)
    {
        var equipped = profile.Equipment.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var set in config.Sets)
        {
            var items = profile.Inventory.Where(i => i.SetId == set.Id && set.ItemIds.Contains(i.ItemId)).ToArray();
            yield return new(set, items.Where(i => equipped.Contains(i.InstanceId)).Select(i => i.ItemId).Distinct().Count(),
                items.Select(i => i.ItemId).Distinct().ToArray());
        }
    }
    public static IEnumerable<string> Describe(RpgSetProgress progress)
    {
        yield return $"Set: {progress.Set.Name} – {progress.Equipped}/{progress.Set.ItemIds.Count} ausgerüstet, {progress.OwnedItemIds.Count} verschiedene Teile vorhanden. {progress.Set.Description}";
        foreach (var bonus in progress.Set.Bonuses.OrderBy(b => b.RequiredPieces))
            yield return $"({bonus.RequiredPieces} Teile) {(bonus.Description.Length>0?bonus.Description:RpgChatPresentation.Bonuses(bonus.Attributes))} – " +
                (progress.Equipped >= bonus.RequiredPieces ? "Aktiv" : "Noch nicht freigeschaltet");
    }
}
