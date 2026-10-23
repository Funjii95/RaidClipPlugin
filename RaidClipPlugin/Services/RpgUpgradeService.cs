using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public sealed class RpgUpgradeService(ViewerPointStore points,RpgConfig config)
{
    public Task<RpgTransactionResult> ApplyAsync(string user,string name,string request,string instance,int expectedLevel,bool refine,CancellationToken ct)=>points.ApplyRpgAsync(user,name,request,a=>
    {
        if(!config.Enabled)return new(false,"RPG ist deaktiviert.");
        var item=a.Rpg.Inventory.SingleOrDefault(i=>i.InstanceId==instance);
        if(item?.Slot is null)return new(false,"Dieser Gegenstand gehört dir nicht oder ist nicht ausrüstbar.");
        if(item.UpgradeLevel!=expectedLevel)return new(false,"Die Aufwertungsstufe hat sich geändert. Bitte neu laden.");
        if(item.IsRefined||refine&&item.UpgradeLevel!=9||!refine&&item.UpgradeLevel>=9)return new(false,"Diese Aufwertung ist nicht möglich.");
        config.Upgrades.Validate();
        var cost=refine?new RpgUpgradeCost(9,config.Upgrades.RefinementPearls,new Dictionary<string,int>{["eternal_depth_core"]=1}):RpgEquipmentUpgradeRules.Cost(item.UpgradeLevel,item.Rarity,config.Upgrades);
        if(a.Points<cost.Pearls)return new(false,"Nicht genügend normale Perlen.");
        if(cost.Materials.Any(m=>RpgUpgradeCatalog.Count(a.Rpg.Materials,m.Key)<m.Value))return new(false,"Nicht genügend Aufwertungsmaterialien.");
        foreach(var pair in cost.Materials)RpgUpgradeCatalog.Add(a.Rpg.Materials,pair.Key,-pair.Value);
        a.Points-=cost.Pearls;
        if(refine)item.IsRefined=true;else item.UpgradeLevel++;
        if(config.Health.Enabled)RpgHealthService.Refresh(a.Rpg,config.Health,DateTimeOffset.UtcNow);
        return new(true,refine?"Gegenstand veredelt: +50 % positive Grundwerte.":$"Garantiert auf +{item.UpgradeLevel} aufgewertet.");
    },ct);
}
