namespace RaidClipPlugin.Services;
public static class RpgCombatDropRules
{
 public const decimal AdditiveBonus=5;
 public static decimal Chance(decimal original)=>original<=0?0:Math.Clamp(original+AdditiveBonus,0,100);
 public static object PreviewBosses(IEnumerable<RaidClipPlugin.Config.RpgAreaBossDefinition> bosses,bool mimic=false)=>bosses.Select(b=>new{b.Id,b.Name,b.AreaId,b.Level,b.BaseHp,b.Attack,b.Experience,b.Pearls,ItemChance=mimic?RpgRegionalMimics.EquipmentChance:Chance(b.ItemChance),b.Rarity,b.Materials,b.Ability,b.Enabled}).ToArray();
}
