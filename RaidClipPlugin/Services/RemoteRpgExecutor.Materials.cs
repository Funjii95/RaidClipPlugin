using System.Text.Json;
using System.Globalization;
using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed partial class RemoteRpgExecutor
{
    private async Task<ActionOutcome> InvokeMaterialAdminAsync(ActionEnvelope e,CancellationToken ct)
    {
        if(e.UserId!=channel)return new(false,"admin_required","Nur der Kanalinhaber hat Adminzugriff.");
        var cfg=configuration();var rpg=cfg.Adventures.Rpg;
        if(!cfg.Enabled||!cfg.PointsEnabled||!rpg.Enabled)return new(false,"disabled","RPG ist nicht spielbereit.");
        string Arg(string key)=>e.Intent.Arguments.GetProperty(key).GetString()!;
        if(e.Intent.Kind=="adminBalance")
        {
            if(saveUpgrades is null)return new(false,"unavailable","Dauerhafte Konfigurationsspeicherung nicht verfügbar.");
            var next=JsonSerializer.Deserialize<RpgUpgradeConfig>(JsonSerializer.Serialize(rpg.Upgrades))!;
            var path=Arg("path").Split(':');var value=decimal.Parse(Arg("value"),CultureInfo.InvariantCulture);
            try
            {
                if(path is ["reset","all"])next=new();
                else if(path is ["drop",var source,var material,var property]&&next.Drops.TryGetValue(source,out var pool)&&pool.TryGetValue(material,out var rule)){
                    if(property=="Chance")rule.Chance=value;else if(property=="Minimum")rule.Minimum=checked((int)value);else if(property=="Maximum")rule.Maximum=checked((int)value);else throw new ArgumentException();
                    if(property!="Chance"&&decimal.Truncate(value)!=value)throw new ArgumentException();}
                else if(path is ["mimic",var id]&&next.MimicWeights.ContainsKey(id)&&decimal.Truncate(value)==value)next.MimicWeights[id]=checked((int)value);
                else if(path is ["equipment",var rarity]&&next.EquipmentDropChances.ContainsKey(rarity))next.EquipmentDropChances[rarity]=value;
                else if(path is ["rarity",var r,var rarityStep]&&next.RarityCosts.TryGetValue(r,out var counts)&&int.TryParse(rarityStep,out var rarityLevel)&&rarityLevel is >=7 and <=9&&decimal.Truncate(value)==value)counts[rarityLevel-7]=checked((int)value);
                else if(path is ["color",var colorStep]&&int.TryParse(colorStep,out var colorLevel)&&colorLevel is >=7 and <=9&&decimal.Truncate(value)==value)next.ColoredPearlCosts[colorLevel-7]=checked((int)value);
                else if(path is ["cost",var step,var kind]&&int.TryParse(step,out var index)&&index is >=1 and <=9&&decimal.Truncate(value)==value){if(kind=="Pearls")next.PearlCosts[index-1]=checked((long)value);else if(kind=="Cores")next.CoreCosts[index-1]=checked((int)value);else throw new ArgumentException();}
                else if(path is ["refinement","Pearls"]&&decimal.Truncate(value)==value)next.RefinementPearls=checked((long)value);
                else throw new ArgumentException();
                next.Validate();saveUpgrades(next);rpg.Upgrades=next;
            }
            catch(Exception error)when(error is ArgumentException or OverflowException or InvalidOperationException){return new(false,"invalid_balance","Ungültige Einstellung. Mindest-/Höchstmengen und Poolgewichte prüfen.");}
            return new(true,"applied","Material- und Aufwertungsregeln gespeichert.",JsonSerializer.Serialize(new{settings=next,message="Einstellungen gespeichert."}));
        }
        var target=Arg("targetUserId");var bytes=await points.ReadCommittedFileAsync(ct);
        if(bytes is null)return new(false,"profile_missing","Zielspieler fehlt.");
        using var players=JsonDocument.Parse(bytes);if(!players.RootElement.TryGetProperty(target,out _))return new(false,"profile_missing","Zielspieler fehlt.");
        var materialId=Arg("materialId");if(!RpgUpgradeCatalog.Properties.ContainsKey(materialId))return new(false,"invalid_material","Material unbekannt.");
        var amount=int.Parse(Arg("count"));var account=(await points.GetProfileAsync(target,ct)).Entry;
        var result=await points.ApplyRpgAsync(target,account.DisplayName,"admin:"+e.Intent.ActionId.ToString("N"),a=>{
            if(RpgUpgradeCatalog.Count(a.Rpg.Materials,materialId)>int.MaxValue-amount)return new(false,"Materiallimit überschritten.");
            RpgUpgradeCatalog.Add(a.Rpg.Materials,materialId,amount);return new(true);},ct);
        var message=result.Success?$"{amount} × {RpgUpgradeCatalog.Materials.Single(m=>m.Id==materialId).Name} gespeichert.":result.Error;
        return new(result.Success,result.Success?"applied":"rule_rejected",message,JsonSerializer.Serialize(new{message}));
    }
}
