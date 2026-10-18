using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;

namespace RaidClipPlugin.Services;

internal sealed record LocalActionRecord(string Hash,string UserId,ActionOutcome? Outcome);
internal sealed class RemoteRpgExecutor(ViewerPointStore points,Func<MinigameConfig> configuration,
    StreamAdmission admission,Guid epoch,string channel,IReadOnlySet<string> users,string journal,
    Func<string,string,bool> permitted)
{
    private readonly SemaphoreSlim gate=new(1,1);
    internal static string CommandFor(string kind)=>kind switch {"status"=>"!stats","inventory"=>"!inventory","sets"=>"!sets","perks"=>"!perks","equip"=>"!equip","unequip"=>"!unequip","buyPerk"=>"!perk","craft"=>"!craft","eat"=>"!sushi","fish"=>"!fish","dive"=>"!dive",_=>""};
    internal async Task<ActionOutcome> ExecuteAsync(ActionEnvelope envelope,CancellationToken ct,TimeSpan? remainingWindow=null)
    {
        var hash=RpgIntents.Hash(envelope.Intent);
        if(envelope.ChannelId!=channel || !users.Contains(envelope.UserId) || envelope.IntentHash!=hash || envelope.SessionId==Guid.Empty)
            return new(false,"scope_denied","Aktionszuordnung ungültig.");
        await gate.WaitAsync(ct);
        try {
            Directory.CreateDirectory(journal);
            var file=Path.Combine(journal,envelope.Intent.ActionId.ToString("N")+".json");
            if(File.Exists(file)){
                var saved=JsonSerializer.Deserialize<LocalActionRecord>(await File.ReadAllTextAsync(file,ct))!;
                if(saved.Hash!=hash || saved.UserId!=envelope.UserId)return new(false,"action_id_conflict","Aktions-ID ist bereits anders belegt.");
                return saved.Outcome??Unknown();
            }
            if(!admission.CanPlay(epoch) || envelope.ExecuteBefore<=DateTimeOffset.UtcNow || envelope.ExecuteBefore>DateTimeOffset.UtcNow.AddSeconds(10))
                return new(false,"not_ready","Plugin/Sitzung nicht frisch spielbereit.");
            var window=remainingWindow??(envelope.ExecuteBefore-DateTimeOffset.UtcNow);
            if(window<=TimeSpan.Zero || window>TimeSpan.FromSeconds(8))return new(false,"expired","Aktionsfenster abgelaufen.");
            using var scope=admission.EnterRemote(epoch,envelope.ExecuteBefore,window);
            // Fsync before calling any gameplay. A crash thereafter is never blindly retried.
            await SaveAsync(file,new(hash,envelope.UserId,null));
            ActionOutcome result;
            try {result=await InvokeAsync(envelope,ct);}
            catch(GameplayUnavailableException){result=new(false,"not_ready","Speicherung wegen abgelaufener Plugin-Sitzung abgelehnt.");}
            catch{result=Unknown();}
            result=result with{Message=result.Message.Length>1000?result.Message[..1000]:result.Message};
            if(JsonSerializer.SerializeToUtf8Bytes(result).Length>48000)result=result with{DataJson=null,Message="Ergebnis gespeichert. Vollständigen Snapshot aktualisieren."};
            await SaveAsync(file,new(hash,envelope.UserId,result));
            return result;
        } finally{gate.Release();}
    }
    private static ActionOutcome Unknown()=>new(null,"outcome_unknown","Ergebnis unklar. Snapshot prüfen; Aktion nicht mit neuer ID wiederholen.");
    private static async Task SaveAsync(string file,LocalActionRecord record)
    {
        var temp=file+".tmp";
        await using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){
            await JsonSerializer.SerializeAsync(stream,record);await stream.FlushAsync();stream.Flush(true);
        }
        File.Move(temp,file,true);
    }
    private async Task<ActionOutcome> InvokeAsync(ActionEnvelope e,CancellationToken ct)
    {
        var bytes=await points.ReadCommittedFileAsync(ct);
        if(bytes is null)return new(false,"profile_missing","Kein lokales Testprofil vorhanden.");
        using var source=JsonDocument.Parse(bytes);
        if(!source.RootElement.TryGetProperty(e.UserId,out _))return new(false,"profile_missing","Kein lokales Testprofil vorhanden.");
        var account=(await points.GetProfileAsync(e.UserId,ct)).Entry;
        var config=configuration();var rpg=config.Adventures.Rpg;
        if(!config.Enabled || !config.PointsEnabled || !rpg.Enabled || !permitted(CommandFor(e.Intent.Kind),account.DisplayName) ||
            config.PointsBlacklist.Any(n=>n.Equals(account.DisplayName,StringComparison.OrdinalIgnoreCase)))return new(false,"command_disabled","Aktion ist im Plugin nicht freigegeben.");
        string Arg(string name)=>e.Intent.Arguments.GetProperty(name).GetString()!;
        var progression=new RpgProgressionService(points,rpg);
        var request="extension:"+e.Intent.ActionId.ToString("N");
        RpgTransactionResult? mutation=null;
        if(e.Intent.Kind is "status" or "inventory" or "sets" or "perks"){
            var projection=SnapshotValidation.Validate(new(1,channel,e.UserId,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),channel).AccountJson!;
            return new(true,"read","Aktueller Plugin-Stand.",JsonSerializer.Serialize(new{profile=JsonDocument.Parse(projection).RootElement.Clone(),stats=CharacterStatService.Calculate(account.Rpg,rpg,RpgActivity.Boss),sets=RpgSetService.Progress(account.Rpg,rpg)}));
        }
        if(e.Intent.Kind=="equip"){
            var instance=Arg("instanceId");
            // Reject numeric chat-view positions: only stable instance IDs cross this boundary.
            if(long.TryParse(instance,out _))return new(false,"invalid_item_id","Vollständige Item-Instanz-ID erforderlich.");
            EquipmentSlot? slot=null;
            if(e.Intent.Arguments.TryGetProperty("slot",out var selected)){
                if(!Enum.TryParse<EquipmentSlot>(selected.GetString(),false,out var parsed) || !Enum.IsDefined(parsed))return new(false,"invalid_slot","Ungültiger Slot.");slot=parsed;
            }
            mutation=await progression.EquipSelectionAsync(e.UserId,account.DisplayName,request,instance,slot,ct);
        }
        if(e.Intent.Kind=="unequip"){
            if(!Enum.TryParse<EquipmentSlot>(Arg("slot"),false,out var slot) || !Enum.IsDefined(slot))return new(false,"invalid_slot","Ungültiger Slot.");
            mutation=await progression.UnequipAsync(e.UserId,account.DisplayName,request,slot,ct);
        }
        if(e.Intent.Kind=="buyPerk")mutation=await progression.BuyPerkAsync(e.UserId,account.DisplayName,request,Arg("perkId"),ct);
        if(e.Intent.Kind=="craft")mutation=await progression.CraftAsync(e.UserId,account.DisplayName,request,Arg("recipeId"),ct);
        if(e.Intent.Kind=="eat")mutation=await progression.ActivateSushiAsync(e.UserId,account.DisplayName,request,Arg("recipeId"),ct);
        if(e.Intent.Kind is "dive" or "fish"){
            var adventure=await new RpgAdventureService(points,config).ExecuteAsync(e.UserId,account.DisplayName,request,e.Intent.Kind=="fish"?RpgActivity.Fishing:RpgActivity.Dive,long.Parse(Arg("stake"),System.Globalization.CultureInfo.InvariantCulture),ct);
            return new(adventure.Success,adventure.Success?"applied":"rule_rejected",adventure.Error,JsonSerializer.Serialize(adventure));
        }
        if(mutation is null)return new(false,"unsupported","Kein sicherer Aufrufweg.");
        return new(mutation.Success,mutation.Success?"applied":mutation.Duplicate?"already_committed":"rule_rejected",mutation.Error,JsonSerializer.Serialize(new{mutation.Balance,mutation.Details}));
    }
    internal string CatalogJson()
    {
        var config=configuration();var rpg=config.Adventures.Rpg;
        return JsonSerializer.Serialize(new{supported=RpgIntents.Supported,
            perks=rpg.Perks.Select(p=>new{p.Id,Name=RpgChatPresentation.PerkName(p),p.MaximumLevel}),
            recipes=rpg.Recipes.Select(r=>new{r.Id,Name=RpgChatPresentation.RecipeName(r),r.PearlCost,Ingredients=RpgSushiService.Ingredients(r),Effects=RpgSushiService.Effects(r)}),
            sets=rpg.Sets,blocked=new[]{"bossAttack","salvage","itemlock","favorite"}});
    }
}
