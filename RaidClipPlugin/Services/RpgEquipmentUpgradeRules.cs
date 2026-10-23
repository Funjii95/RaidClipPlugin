using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
internal enum EquipmentUpgradeMaterial { WhitePearl, BluePearl, RedPearl }
internal sealed record EquipmentUpgradeRequirement(int TargetLevel,long PearlCost,EquipmentUpgradeMaterial? Material,int MaterialCount);
public sealed record RpgUpgradeCost(int TargetLevel,long Pearls,IReadOnlyDictionary<string,int> Materials);
internal static class RpgEquipmentUpgradeRules
{
    internal const int MaximumLevel=9;
    internal const decimal BonusPerLevel=0.03m;
    internal static readonly decimal[] Bonuses=[0,.03m,.06m,.09m,.12m,.17m,.22m,.28m,.35m,.45m];
    internal static decimal Multiplier(int level)=>level is >=0 and <=9?1+Bonuses[level]:throw new ArgumentOutOfRangeException(nameof(level));
    internal static decimal AttributeValue(decimal value,int level)=>value>0?decimal.Round(value*Multiplier(level),4,MidpointRounding.AwayFromZero):value;
    internal static EquipmentUpgradeRequirement NextRequirement(int level)
    {
        if(level==9)throw new InvalidOperationException("Maximale Aufwertungsstufe erreicht.");
        var c=Cost(level,ItemRarity.Common,new());
        EquipmentUpgradeMaterial? material=c.TargetLevel switch{7=>EquipmentUpgradeMaterial.WhitePearl,8=>EquipmentUpgradeMaterial.BluePearl,9=>EquipmentUpgradeMaterial.RedPearl,_=>null};
        return new(c.TargetLevel,c.Pearls,material,material.HasValue?1:0);
    }
    public static RpgUpgradeCost Cost(int level,ItemRarity rarity,RpgUpgradeConfig config)
    {
        if(level is <0 or >=9)throw new ArgumentOutOfRangeException(nameof(level));
        config.Validate();var target=level+1;var materials=new Dictionary<string,int>();
        if(config.CoreCosts[level]>0)materials["reinforcement_core"]=config.CoreCosts[level];
        if(target>=7){materials[new[]{"white_pearl","blue_pearl","red_pearl"}[target-7]]=config.ColoredPearlCosts[target-7];
            var id=rarity switch{ItemRarity.Epic=>"luminous_scale",ItemRarity.Legendary=>"rune_shell",ItemRarity.True=>"true_coral_core",ItemRarity.Cursed=>"cursed_silk",ItemRarity.Abyss=>"abyss_shard",_=>"pearl_dust"};
            materials[id]=config.RarityCosts[rarity.ToString()][target-7];}
        return new(target,config.PearlCosts[level],materials);
    }
    public static RpgAttributes Effective(RpgItem item)
    {
        if(item.UpgradeLevel is <0 or >9||item.IsRefined&&item.UpgradeLevel!=9)throw new InvalidDataException("Ungültige Aufwertung.");
        var result=new RpgAttributes();var multiplier=item.IsRefined?1.5m:Multiplier(item.UpgradeLevel);
        foreach(var property in typeof(RpgAttributes).GetProperties()){
            var value=(decimal)property.GetValue(item.Attributes)!;
            property.SetValue(result,value>0&&multiplier>1?decimal.Round(value*multiplier,4,MidpointRounding.AwayFromZero):value);}
        return result;
    }
}
