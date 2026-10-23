using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;

namespace RaidClipPlugin.Services;

internal sealed record LocalActionRecord(string Hash,string UserId,ActionOutcome? Outcome);
internal sealed partial class RemoteRpgExecutor(ViewerPointStore points,Func<MinigameConfig> configuration,
    StreamAdmission admission,Guid epoch,string channel,IReadOnlySet<string> users,string journal,
    Func<string,string,bool> permitted,ChatMinigameService? minigames=null,Func<string,bool>? enrolled=null,Action<RpgUpgradeConfig>? saveUpgrades=null)
{
    private readonly SemaphoreSlim gate=new(1,1);
    internal static string CommandFor(string kind)=>kind switch {"upgradeItem" or "refineItem"=>"!equip","learnTalent"=>"!talent","openShells"=>"!muscheln","status"=>"!stats","inventory"=>"!inventory","sets"=>"!sets","perks"=>"!perks","equip"=>"!equip","unequip"=>"!unequip","buyPerk"=>"!perk","craft"=>"!craft","eat"=>"!sushi","fish"=>"!fish","dive"=>"!dive","bossAttack"=>"!attack","bossStatus"=>"!stats","itemlock"=>"!itemlock","favorite"=>"!favorite","salvagePreview" or "salvageBatch" or "salvageConfirm" or "salvageCancel"=>"!salvage",_=>""};
    internal async Task<ActionOutcome> ExecuteAsync(ActionEnvelope envelope,CancellationToken ct,TimeSpan? remainingWindow=null)
    {
        var hash=RpgIntents.Hash(envelope.Intent);
        if(envelope.Intent.Kind=="openShells")return new(false,"chat_only","Muscheln sind derzeit ausschließlich im Chat verfügbar: !muscheln [1–100].");
        if(envelope.ChannelId!=channel || !(users.Contains(envelope.UserId)||enrolled?.Invoke(envelope.UserId)==true) || envelope.IntentHash!=hash || envelope.SessionId==Guid.Empty)
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
            if(JsonSerializer.SerializeToUtf8Bytes(result).Length>ActionResultLimits.MaximumOutcomeBytes)result=result with{DataJson=null,Message="Ergebnis gespeichert. Vollständigen Snapshot aktualisieren."};
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
        if(RpgIntents.IsAdmin(e.Intent.Kind))return await InvokeAdminAsync(e,ct);
        var bytes=await points.ReadCommittedFileAsync(ct);
        if(bytes is null)return new(false,"profile_missing","Kein lokales Testprofil vorhanden.");
        using var source=JsonDocument.Parse(bytes);
        if(!source.RootElement.TryGetProperty(e.UserId,out _))return new(false,"profile_missing","Kein lokales Testprofil vorhanden.");
        var account=(await points.GetProfileAsync(e.UserId,ct)).Entry;
        var config=configuration();var rpg=config.Adventures.Rpg;
        var command=e.Intent.Kind=="casino"?"!"+e.Intent.Arguments.GetProperty("game").GetString():e.Intent.Kind=="community"?minigames?.RemoteCommand(e.Intent.Arguments.GetProperty("game").GetString()!,e.Intent.Arguments.GetProperty("operation").GetString()!)??"":e.Intent.Kind is "preview" or "minigames"?"!stats":CommandFor(e.Intent.Kind);
        if(!config.Enabled || !config.PointsEnabled || !rpg.Enabled || !permitted(command,account.DisplayName) ||
            config.PointsBlacklist.Any(n=>n.Equals(account.DisplayName,StringComparison.OrdinalIgnoreCase)))return new(false,"command_disabled","Aktion ist im Plugin nicht freigegeben.");
        string Arg(string name)=>e.Intent.Arguments.GetProperty(name).GetString()!;
        var progression=new RpgProgressionService(points,rpg);
        var request="extension:"+e.Intent.ActionId.ToString("N");
        RpgTransactionResult? mutation=null;
        if(e.Intent.Kind is "bossStatus" or "bossAttack" or "itemlock" or "favorite" or "salvagePreview" or "salvageBatch" or "salvageConfirm" or "salvageCancel" or "community")
            return await InvokeExtendedAsync(e,account,request,ct);
        if(e.Intent.Kind=="preview")return new(true,"read","Aktuelle primäre Ausrüstungs-Lootchance.",JsonSerializer.Serialize(new{profile=JsonDocument.Parse(SnapshotValidation.Validate(new(1,channel,e.UserId,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),channel).AccountJson!).RootElement.Clone(),dive=AdventurePreview(account,"dive",long.Parse(Arg("stake"))),fish=AdventurePreview(account,"fish",long.Parse(Arg("fishingStake")))}));
        if(e.Intent.Kind=="minigames")return new(true,"read","Spielhallenstatus.",JsonSerializer.Serialize(await MinigameDataAsync(account,ct)));
        if(e.Intent.Kind=="casino"){
            if(minigames is null)return new(false,"unavailable","Laufender Chat-Spieldienst fehlt.");
            var result=await minigames.ExecuteRemoteCasinoAsync(e.UserId,account.DisplayName,e.Intent.ActionId,Arg("game"),Arg("stake"),Arg("choice"),ct);
            if(result.Success)_casinoResults[(e.UserId,Arg("game"))]=(e.Intent.ActionId,result);
            var current=(await points.GetProfileAsync(e.UserId,ct)).Entry;
            return new(result.Success,result.Success?"applied":"rule_rejected",result.Message,JsonSerializer.Serialize(await MinigameDataAsync(current,ct)));
        }
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
        if(e.Intent.Kind is "upgradeItem" or "refineItem")mutation=await new RpgUpgradeService(points,rpg).ApplyAsync(e.UserId,account.DisplayName,request,Arg("instanceId"),int.Parse(Arg("expectedLevel")),e.Intent.Kind=="refineItem",ct);
        if(e.Intent.Kind=="openShells"){var opened=await new RpgShellService(points,rpg).OpenAsync(e.UserId,account.DisplayName,request,int.Parse(Arg("count")),ct);return new(opened.Success,opened.Success?"applied":"rule_rejected",opened.Message);}
        if(e.Intent.Kind=="learnTalent")mutation=await RpgTalentService.LearnAsync(points,rpg,e.UserId,account.DisplayName,request,Arg("talentId"),int.Parse(Arg("expectedRank")),int.Parse(Arg("catalogVersion")),ct);
        if(e.Intent.Kind=="buyPerk")mutation=await progression.BuyPerkAsync(e.UserId,account.DisplayName,request,Arg("perkId"),ct);
        if(e.Intent.Kind=="craft")mutation=await progression.CraftAsync(e.UserId,account.DisplayName,request,Arg("recipeId"),ct);
        if(e.Intent.Kind=="eat")mutation=await progression.ActivateSushiAsync(e.UserId,account.DisplayName,request,Arg("recipeId"),ct);
        if(e.Intent.Kind is "dive" or "fish"){
            var adventure=await new RpgAdventureService(points,config).ExecuteAsync(e.UserId,account.DisplayName,request,e.Intent.Kind=="fish"?RpgActivity.Fishing:RpgActivity.Dive,long.Parse(Arg("stake"),System.Globalization.CultureInfo.InvariantCulture),ct);
            if(adventure.BossDiscovered && minigames is not null)await minigames.StartDiscoveredBossAsync(account.DisplayName,ct);
            return new(adventure.Success,adventure.Success?"applied":"rule_rejected",adventure.Error,JsonSerializer.Serialize(adventure));
        }
        if(e.Intent.Kind is "buyPerk" or "learnTalent" or "upgradeItem" or "refineItem" && mutation?.Success==true){
            // Read the committed authoritative state; the viewer need not wait for
            // the independent Firestore delivery before seeing the purchased level.
            var committed=(await points.GetProfileAsync(e.UserId,ct)).Entry;
            var projection=SnapshotValidation.Validate(new(1,channel,e.UserId,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(committed)),channel).AccountJson!;
            return new(true,"applied",mutation.Error,JsonSerializer.Serialize(new{mutation.Balance,mutation.Details,
                profile=JsonDocument.Parse(projection).RootElement.Clone(),stats=CharacterStatService.Calculate(committed.Rpg,rpg,RpgActivity.Boss)}));
        }
        if(mutation is null)return new(false,"unsupported","Kein sicherer Aufrufweg.");
        return new(mutation.Success,mutation.Success?"applied":mutation.Duplicate?"already_committed":"rule_rejected",mutation.Error,JsonSerializer.Serialize(new{mutation.Balance,mutation.Details}));
    }
    internal string CatalogJson()
    {
        var config=configuration();var rpg=config.Adventures.Rpg;
        return JsonSerializer.Serialize(new{supported=RpgIntents.Supported.Where(kind=>kind!="openShells").Concat(new[]{"adminCatalog","adminStart","adminGrant","adminMaterialGrant","adminBalance","adminHealAll"}),
            materialCatalog=RpgUpgradeCatalog.Materials, itemUpgrade=new{maximumLevel=9,bonuses=RpgEquipmentUpgradeRules.Bonuses,costsByRarity=Enum.GetValues<ItemRarity>().ToDictionary(r=>r.ToString(),r=>Enumerable.Range(0,9).Select(level=>RpgEquipmentUpgradeRules.Cost(level,r,rpg.Upgrades))),refinement=new{TargetLevel=9,Pearls=rpg.Upgrades.RefinementPearls,Materials=new Dictionary<string,int>{{"eternal_depth_core",1}}}},
            talentCatalogVersion=TalentRules.CatalogVersion,talents=TalentRules.Catalog,
            perks=rpg.Perks.Select(p=>new{p.Id,Name=RpgChatPresentation.PerkName(p),p.MaximumLevel,p.BonusPerLevel,Presentation=RpgPerkCatalog.Find(p.Id),Costs=Enumerable.Range(0,p.MaximumLevel).Select(level=>p.Cost(level).ToString(System.Globalization.CultureInfo.InvariantCulture))}),
            recipes=rpg.Recipes.Select(r=>new{r.Id,Name=RpgChatPresentation.RecipeName(r),r.PearlCost,r.Category,r.Activity,BossUses=RpgSushiService.Uses(r,rpg),r.Attributes,r.PassiveHealing,r.ChatHealing,r.BossDamageReduction,r.BossLifeleech,Ingredients=RpgSushiService.Ingredients(r),Effects=RpgSushiService.Effects(r)}),
            fish=rpg.FishSpecies.Select(f=>new{f.Id,f.Name,f.Rarity}),sets=rpg.Sets,blocked=new[]{"bossStart","heistStart","moderatorCommands","give","reset"}},new JsonSerializerOptions{Converters={new CatalogAttributesConverter()}});
    }
    private sealed class CatalogAttributesConverter:System.Text.Json.Serialization.JsonConverter<RpgAttributes>
    {
        public override RpgAttributes Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer,RpgAttributes value,JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach(var property in typeof(RpgAttributes).GetProperties())
                if((decimal)property.GetValue(value)! is var number && number!=0)writer.WriteNumber(property.Name,number);
            writer.WriteEndObject();
        }
    }
}
