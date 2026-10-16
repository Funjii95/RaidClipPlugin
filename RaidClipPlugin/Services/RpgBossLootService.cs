using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class RpgBossLootService(ViewerPointStore points, RpgConfig config, IDuelRandom random)
{
    public static ItemRarity MaximumRarity(RpgConfig config, int initialHp, int participants) =>
        config.BossLegendaryEnabled && initialHp >= config.BossLegendaryMinimumHp && participants >= config.BossLegendaryMinimumPlayers
            ? ItemRarity.Legendary : ItemRarity.Epic;

    public async Task<RpgItem?> AwardAsync(string userId, string name, string encounterId, int initialHp, int participants, DateTimeOffset now, CancellationToken token)
    {
        if (!config.Enabled || config.BossLootChance <= 0) return null;
        config.Validate(); RpgItem? item = null;
        var result = await points.ApplyRpgAsync(userId, name, "bossloot:" + encounterId, account =>
        {
            item = new RpgLootService(random).RollReward(config.DiveLoot, now, RpgActivity.Boss, config.BossLootChance,
                MaximumRarity(config, initialHp, participants)).Item;
            if (item is not null) account.Rpg.Inventory.Add(item);
            return new(true);
        }, token);
        return result.Success ? item : null;
    }
}
