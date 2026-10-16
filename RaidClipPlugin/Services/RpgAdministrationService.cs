using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

/// <summary>Local administrator operations, deliberately not exposed as viewer commands.</summary>
public sealed class RpgAdministrationService(ViewerPointStore points)
{
    public Task<RpgTransactionResult> DeleteFishAsync(string userId, string displayName, string actionId,
        string fishId, int quantity, CancellationToken token) => points.ApplyRpgAsync(userId, displayName, actionId, account =>
        {
            if (quantity < 1 || !account.Rpg.Fish.TryGetValue(fishId, out var fish) || quantity > fish.Quantity)
                return new(false, "Ungültige Fischmenge oder Fischart nicht vorhanden.");
            fish.Quantity -= quantity;
            if (fish.Quantity == 0) account.Rpg.Fish.Remove(fishId);
            // Historical collection remains intact; LegacyFishImported prevents resurrection.
            return new(true);
        }, token, backupBeforeCommit: true);

    public Task<RpgTransactionResult> DeleteEquipmentAsync(string userId, string displayName, string actionId,
        string instanceId, CancellationToken token) => points.ApplyRpgAsync(userId, displayName, actionId, account =>
        {
            var item = account.Rpg.Inventory.SingleOrDefault(i => i.InstanceId == instanceId);
            if (item?.Slot is null) return new(false, "Dieses Equipmentstück ist nicht vorhanden.");
            account.Rpg.Inventory.Remove(item);
            foreach (var slot in account.Rpg.Equipment.Where(p => p.Value == instanceId).Select(p => p.Key).ToArray())
                account.Rpg.Equipment.Remove(slot);
            return new(true);
        }, token, backupBeforeCommit: true);
}
