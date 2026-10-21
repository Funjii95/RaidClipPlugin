using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgCollectorService
{
    public static RpgCollectorBonuses Calculate(RpgProfile profile, RpgLootConfig config)
    {
        var result = new RpgCollectorBonuses();
        foreach (var item in profile.Inventory.Where(i => i.Kind == RpgItemKind.Treasure && i.Slot is null && i.Collector is not null).DistinctBy(i => i.ItemId))
        {
            var b = item.Collector!;
            result.FishingPearls += Math.Max(0,b.FishingPearls); result.DivePearls += Math.Max(0,b.DivePearls);
            result.BossPearls += Math.Max(0,b.BossPearls); result.BossExperience += Math.Max(0,b.BossExperience);
            result.TreasureChance += Math.Max(0,b.TreasureChance); result.RareTreasureChance += Math.Max(0,b.RareTreasureChance);
        }
        result.FishingPearls=Math.Min(result.FishingPearls,config.CollectorPearlCap);
        result.DivePearls=Math.Min(result.DivePearls,config.CollectorPearlCap);
        result.BossPearls=Math.Min(result.BossPearls,config.CollectorPearlCap);
        result.BossExperience=Math.Min(result.BossExperience,config.CollectorExperienceCap);
        result.TreasureChance=Math.Min(result.TreasureChance+RpgPerkCatalog.Combined(profile,"treasure"),config.CollectorChanceCap);
        result.RareTreasureChance=Math.Min(result.RareTreasureChance+RpgPerkCatalog.Bonus(profile,"rareTreasure"),config.CollectorChanceCap);
        return result;
    }
    public static long Reward(long amount, decimal percent) => amount <= 0 ? amount : checked(amount+(long)decimal.Floor(amount*Math.Clamp(percent,0,100)/100));
    public static IEnumerable<string> Describe(RpgCollectorBonuses b)
    {
        if(b.FishingPearls>0)yield return $"+{RpgChatPresentation.Number(b.FishingPearls)} % Fischfang-Perlen";
        if(b.DivePearls>0)yield return $"+{RpgChatPresentation.Number(b.DivePearls)} % positive Dive-Belohnung (ohne Einsatz)";
        if(b.BossPearls>0)yield return $"+{RpgChatPresentation.Number(b.BossPearls)} % Boss-Siegperlen";
        if(b.BossExperience>0)yield return $"+{RpgChatPresentation.Number(b.BossExperience)} % Überlebens-Boss-EXP";
        if(b.TreasureChance>0)yield return $"+{RpgChatPresentation.Number(b.TreasureChance)} % relative Sammlerschatzchance";
        if(b.RareTreasureChance>0)yield return $"+{RpgChatPresentation.Number(b.RareTreasureChance)} % relative seltene Sammlerschatzchance";
    }
}
