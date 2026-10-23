using System.Text.Json;
using RaidClipPlugin.Models;
using SnapshotProtocol;

namespace RaidClipPlugin.Services;

internal sealed partial class RemoteRpgExecutor
{
    private async Task<ActionOutcome> InvokeExtendedAsync(ActionEnvelope e,ViewerPointEntry account,string request,CancellationToken ct)
    {
        string Arg(string name)=>e.Intent.Arguments.GetProperty(name).GetString()!;
        ActionOutcome Reply(bool success,string message,object? data=null)=>new(success,success?"applied":"rule_rejected",message,data is null?null:JsonSerializer.Serialize(data));
        if(e.Intent.Kind is "selectBoss" or "bossStatus" or "bossAttack"){
            if(minigames is null)return Reply(false,"Laufender Spieldienst fehlt.");
            if(e.Intent.Kind=="selectBoss"){
                var selected=await minigames.RemoteAdventures.SelectAreaBossAsync(e.UserId,account.DisplayName,request,Arg("bossId"),ct);
                return Reply(selected.Success,selected.Error,new{boss=await minigames.ReadRemoteBossAsync((await points.GetProfileAsync(e.UserId,ct)).Entry,ct)});
            }
            if(e.Intent.Kind=="bossStatus")return Reply(true,"Aktueller Bossstatus.",await minigames.ReadRemoteBossAsync(account,ct));
            var attack=await minigames.ExecuteRemoteBossAsync(e.UserId,account.DisplayName,request,ct);
            return Reply(attack.Success,attack.Error,new{attack,boss=await minigames.ReadRemoteBossAsync((await points.GetProfileAsync(e.UserId,ct)).Entry,ct)});
        }
        if(e.Intent.Kind=="community"){
            if(minigames is null)return Reply(false,"Laufender Spieldienst fehlt.");
            var result=await minigames.ExecuteRemoteCommunityAsync(e.UserId,account.DisplayName,request,Arg("game"),Arg("operation"),Arg("eventId"),Arg("stake"),Arg("target"),users,ct);
            return Reply(result.Success,result.Message,await MinigameDataAsync((await points.GetProfileAsync(e.UserId,ct)).Entry,ct));
        }
        var config=configuration();
        var salvage=new RpgSalvageService(points,config.Adventures.Rpg,maximumBalance:config.MaximumAccountEnabled?config.MaximumAccountPoints:long.MaxValue);
        if(e.Intent.Kind is "itemlock" or "favorite"){
            var result=await salvage.SetProtectionInstanceAsync(e.UserId,account.DisplayName,request,Arg("instanceId"),e.Intent.Kind=="favorite",Arg("enabled")=="true",ct);
            return Reply(result.Success,result.Error,new{result.Balance});
        }
        if(e.Intent.Kind is "salvagePreview" or "salvageBatch"){
            var preview=e.Intent.Kind=="salvagePreview"?await salvage.PreviewInstancesAsync(e.UserId,[Arg("instanceId")],ct):await salvage.PreviewBatchAsync(e.UserId,Arg("selection"),ct);
            var q=preview.Quote;
            return Reply(preview.Success,preview.Error,q is null?null:new{quote=new{token=q.Token,expiresAt=q.ExpiresAt.ToUnixTimeMilliseconds(),total=q.Total,fingerprint=q.Token,items=q.Items.Select(i=>new{id=i.InstanceId,name=i.Name,value=i.Value,warning=i.RequiresWarning})}});
        }
        if(e.Intent.Kind=="salvageCancel")return Reply(salvage.CancelQuote(e.UserId,Arg("token")),"Zerlegevorschau geschlossen.",new{cancelled=true});
        if(e.Intent.Kind=="salvageConfirm"){
            var result=await salvage.ConfirmAsync(e.UserId,account.DisplayName,request,Arg("token"),ct);
            return Reply(result.Success,result.Error,result);
        }
        return Reply(false,"Kein sicherer Aufrufweg.");
    }
}
