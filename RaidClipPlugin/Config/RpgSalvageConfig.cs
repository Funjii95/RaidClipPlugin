using RaidClipPlugin.Models;

namespace RaidClipPlugin.Config;

public sealed class RpgSalvageRange
{
    public long Minimum { get; set; }
    public long Maximum { get; set; }
}
public sealed class RpgSalvageConfig
{
    public bool Enabled { get; set; } = true;
    public int ConfirmationSeconds { get; set; } = 60;
    public int MaximumBatchSize { get; set; } = 100;
    public Dictionary<ItemRarity, RpgSalvageRange> Values { get; set; } = new()
    {
        [ItemRarity.Common] = new() { Minimum=25, Maximum=50 },
        [ItemRarity.Uncommon] = new() { Minimum=75, Maximum=150 },
        [ItemRarity.Rare] = new() { Minimum=200, Maximum=400 },
        [ItemRarity.Epic] = new() { Minimum=600, Maximum=1200 },
        [ItemRarity.Legendary] = new() { Minimum=2000, Maximum=4000 },
        [ItemRarity.True] = new() { Minimum=2500, Maximum=5000 },
        [ItemRarity.Cursed] = new() { Minimum=2500, Maximum=5000 },
        [ItemRarity.Abyss] = new() { Minimum=4000, Maximum=8000 }
    };
    public void Validate()
    {
        if (ConfirmationSeconds is < 10 or > 300 || MaximumBatchSize is < 1 or > 100 ||
            Enum.GetValues<ItemRarity>().Any(r => !Values.TryGetValue(r,out var v) || v.Minimum < 0 || v.Maximum < v.Minimum || v.Maximum > 1000000000))
            throw new InvalidOperationException("Ungültige Zerlegewerte oder Bestätigungsgrenzen.");
    }
}
