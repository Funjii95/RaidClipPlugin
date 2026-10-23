using System.Text.Json;
using RaidClipPlugin.Models;
using SnapshotProtocol;

namespace RaidClipPlugin.Services;

internal sealed partial class RemoteRpgExecutor
{
    private sealed class AdminRoll(bool maximum):IDuelRandom {public int NextInclusive(int min,int max)=>maximum?max:min;}
    private async Task<ActionOutcome> InvokeAdminAsync(ActionEnvelope e,CancellationToken ct)
    {
        // The queue assigns UserId from authenticated claims; never trust a supplied name or flag.
        if(e.UserId!=channel)return new(false,"admin_required","Nur der Kanalinhaber hat Adminzugriff.");
        var cfg=configuration();var rpg=cfg.Adventures.Rpg;
        if(!cfg.Enabled||!cfg.PointsEnabled||!rpg.Enabled)return new(false,"disabled","RPG ist nicht spielbereit.");
        if(e.Intent.Kind=="adminHealAll"){
            if(!rpg.Health.Enabled)return new(false,"disabled","Das HP-System ist deaktiviert.");
            var healed=await points.HealAllAsync(channel,e.Intent.ActionId.ToString("N"),rpg.Health,DateTimeOffset.UtcNow,ct);
            return new(healed.Success,healed.Success?"applied":"rule_rejected",healed.Message,JsonSerializer.Serialize(new{message=healed.Message,healed=healed.Healed,players=healed.Players}));
        }
        string Arg(string key)=>e.Intent.Arguments.GetProperty(key).GetString()!;
        if(e.Intent.Kind is "adminMaterialGrant" or "adminBalance")return await InvokeMaterialAdminAsync(e,ct);
        if(e.Intent.Kind=="adminCatalog")return new(true,"read","Adminbereich bereit.",JsonSerializer.Serialize(new{
            settings=rpg.Upgrades, materials=RpgUpgradeCatalog.Materials,
            items=rpg.DiveLoot.Items.Where(i=>i.Slot.HasValue&&i.Kind!=RpgItemKind.Consumable).OrderByDescending(i=>i.Rarity).ThenBy(i=>i.DisplayName,StringComparer.Ordinal).Select(i=>new{id=i.ItemId,name=i.DisplayName,rarity=i.Rarity.ToString(),slot=i.Slot!.Value.ToString()}),
            attributes=typeof(RpgAttributes).GetProperties().Select(p=>p.Name),events=new[]{"boss","mimic","race","heist","hotTime"}}));
        if(e.Intent.Kind=="adminStart")
        {
            if(minigames is null)return new(false,"unavailable","Verbundenen Spieldienst starten.");
            var result=await minigames.ExecuteAdminEventAsync(e.UserId,"admin:"+e.Intent.ActionId.ToString("N"),Arg("event"),long.Parse(Arg("stake")),ct);
            return new(result.Success,result.Success?"applied":"rule_rejected",result.Message,JsonSerializer.Serialize(new{message=result.Message,eventType=Arg("event")}));
        }
        if(e.Intent.Kind!="adminGrant")return new(false,"unsupported","Unbekannter Adminauftrag.");
        var bytes=await points.ReadCommittedFileAsync(ct);if(bytes is null)return new(false,"profile_missing","Keine Spielstände vorhanden.");
        using var players=JsonDocument.Parse(bytes);
        var target=Arg("targetUserId");
        if(!players.RootElement.TryGetProperty(target,out _))return new(false,"profile_missing","Zielspieler muss bereits einen lokalen Spielstand besitzen.");
        var definition=rpg.DiveLoot.Items.SingleOrDefault(i=>i.ItemId==Arg("itemId")&&i.Slot.HasValue&&i.Kind!=RpgItemKind.Consumable);
        if(definition is null)return new(false,"invalid_item","Unbekannte Ausrüstung.");
        var amount=int.Parse(Arg("count"));var roll=Arg("roll");
        using var chosen=JsonDocument.Parse(Arg("stats"));
        var properties=typeof(RpgAttributes).GetProperties().ToDictionary(p=>p.Name,StringComparer.Ordinal);
        var custom=new Dictionary<string,decimal>();
        foreach(var stat in chosen.RootElement.EnumerateObject())
        {
            if(!properties.ContainsKey(stat.Name)||!custom.TryAdd(stat.Name,stat.Value.GetDecimal()))return new(false,"invalid_stats","Attribut unbekannt oder doppelt.");
            var cap=stat.Name is "MaximumHp" or "Power" or "Stamina"?1000000:stat.Name is "Damage" or "CritDamage" or "BossDamage" or "Armor"?1000:100;
            if(custom[stat.Name]<0||custom[stat.Name]>cap)return new(false,"invalid_stats",$"{stat.Name}: erlaubter Bereich 0 bis {cap}.");
        }
        IDuelRandom rng=roll=="random"?new CryptoDuelRandom():new AdminRoll(roll=="maximum");
        var created=new List<RpgItem>();
        for(var n=0;n<amount;n++)
        {
            var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;
            item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=DateTimeOffset.UtcNow;
            item.IsLocked=false;item.IsFavorite=false;item.Kind=RpgItemKind.Equipment;
            RpgItemGenerationService.Generate(item,rpg.DiveLoot,rng);
            if(roll=="custom")
            {
                item.Attributes=new();foreach(var stat in custom)properties[stat.Key].SetValue(item.Attributes,stat.Value);
                item.Effects.Clear();item.Generation=null;item.CoreStatsVersion=1;
            }
            created.Add(item);
        }
        var account=(await points.GetProfileAsync(target,ct)).Entry;
        var resultGrant=await points.ApplyRpgAsync(target,account.DisplayName,"admin:"+e.Intent.ActionId.ToString("N"),a=>
        {
            a.Rpg.Inventory.AddRange(created);
            try{SnapshotValidation.Validate(new(1,channel,target,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(a)),channel);}
            catch(InvalidDataException){return new(false,"Items nicht erstellt: Der Spielstand würde die Cloud-Größenbegrenzung überschreiten. Zuerst Inventar aufräumen.");}
            return new(true);
        },ct);
        var message=resultGrant.Success?$"{amount} × {definition.DisplayName} für {account.DisplayName} gespeichert.":resultGrant.Error;
        return new(resultGrant.Success,resultGrant.Success?"applied":"rule_rejected",message,JsonSerializer.Serialize(new{message,targetUserId=target,itemId=definition.ItemId,roll,instanceIds=resultGrant.Success?created.Select(i=>i.InstanceId):Array.Empty<string>()}));
    }
}
