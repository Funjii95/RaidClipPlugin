using System.Text.Json;
using System.Globalization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed partial class RemoteRpgExecutor
{
 private bool StaffAllowed(ActionEnvelope e)=>e.UserId==channel||configuration().Adventures.Rpg.AdminAccess.Moderators.Contains(e.UserId)&&AdminPermissions.ModeratorMay(e.Intent);
 private AppConfig ReadAdmin()=>JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(readAdminConfiguration?.Invoke()??new AppConfig{Minigame=configuration()}))!;
 private static ActionOutcome Reply(object data,string message="Verwaltung geladen.")=>new(true,"read",message,JsonSerializer.Serialize(data));
 private async Task<ActionOutcome> InvokeControlCenterAsync(ActionEnvelope e,CancellationToken ct)
 {
  string Arg(string key)=>e.Intent.Arguments.GetProperty(key).GetString()!;
  var owner=e.UserId==channel;var cfg=configuration();var rpg=cfg.Adventures.Rpg;
  if(e.Intent.Kind=="adminDashboard"){
   var file=await points.ReadCommittedFileAsync(ct);using var players=JsonDocument.Parse(file??"{}"u8.ToArray());
   var audit=new List<JsonElement>();
   if(owner&&File.Exists(Path.Combine(journal,"admin-audit.jsonl")))foreach(var line in File.ReadLines(Path.Combine(journal,"admin-audit.jsonl")).TakeLast(100).Reverse()){
    try{audit.Add(JsonSerializer.Deserialize<JsonElement>(line));}catch(JsonException){/* Ignore an interrupted final log line. */}
   }
   return Reply(new{role=owner?"owner":"moderator",pluginVersion=typeof(RemoteRpgExecutor).Assembly.GetName().Version!.ToString(),connected=admission.CanPlay(epoch),lastCheck=DateTimeOffset.UtcNow,registeredPlayers=players.RootElement.EnumerateObject().Count(),onlinePlayers=(int?)null,boss=minigames?.AdventureStatus,areaBoss=minigames is null?null:await minigames.RemoteAdventures.ReadAreaBossAsync(e.UserId,ct),bossDefinitions=rpg.AreaBosses.Bosses,hotTime=points.HotTime.Status,community=minigames is null?null:await minigames.ReadRemoteCommunityAsync(e.UserId,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),ct),moderators=owner?rpg.AdminAccess.Moderators:[],sections=RpgAdminConfiguration.Sections,audit});
  }
  if(e.Intent.Kind=="adminConfigRead"){
   var app=ReadAdmin();var section=Arg("section");var filter=e.Intent.Arguments.TryGetProperty("filter",out var f)?f.GetString()!:"-";var page=e.Intent.Arguments.TryGetProperty("page",out var pg)?int.Parse(pg.GetString()!):0;
   var fields=RpgAdminConfiguration.Fields(app,section).Where(x=>filter=="-"||x.Label.Contains(filter,StringComparison.OrdinalIgnoreCase)||x.Path.Contains(filter,StringComparison.OrdinalIgnoreCase)).ToArray();
   return Reply(new{section,version=RpgAdminConfiguration.Version(app,section),fields=fields.Skip(Math.Max(0,page)*80).Take(80),total=fields.Length,page,editable=saveAdminConfiguration is not null,talents=section=="talents"?TalentRules.Catalog:null});
  }
  if(e.Intent.Kind=="adminConfigSave"){
   if(saveAdminConfiguration is null)return new(false,"unavailable","Dauerhafte Konfigurationsspeicherung nicht verfügbar.");
   var section=Arg("section");var before=ReadAdmin();AppConfig next;AdminChange[] changes;
   try{changes=JsonSerializer.Deserialize<AdminChange[]>(Arg("changes"))!;next=RpgAdminConfiguration.Apply(before,section,Arg("version"),changes);}
   catch(Exception ex)when(ex is InvalidOperationException or ArgumentException or JsonException or FormatException or OverflowException){return new(false,"invalid_configuration",ex.Message);}
   var old=RpgAdminConfiguration.Fields(before,section).ToDictionary(x=>x.Path);saveAdminConfiguration(next);
   return new(true,"applied","Konfiguration dauerhaft gespeichert. Änderungen an Spielrunden gelten für nachfolgende Starts.",JsonSerializer.Serialize(new{message="Gespeichert.",audit=changes.Select(c=>new{path=c.Path,oldValue=old[c.Path].Value,newValue=c.Value})}));
  }
  if(e.Intent.Kind=="adminRoles"){
   if(saveAdminConfiguration is null)return new(false,"unavailable","Dauerhafte Rollenverwaltung nicht verfügbar.");
   var next=ReadAdmin();var target=Arg("targetUserId");if(target==channel)return new(false,"invalid_role","Die Kanalinhaberrolle wird nicht geändert.");
   var previous=next.Minigame.Adventures.Rpg.AdminAccess.Moderators.Contains(target);next.Minigame.Adventures.Rpg.AdminAccess.Moderators.RemoveAll(x=>x==target);if(Arg("enabled")=="true")next.Minigame.Adventures.Rpg.AdminAccess.Moderators.Add(target);next.Minigame.Adventures.Rpg.AdminAccess.Validate();saveAdminConfiguration(next);
   return new(true,"applied","Moderatorfreigabe gespeichert.",JsonSerializer.Serialize(new{message="Moderatorfreigabe gespeichert.",audit=new{oldValue=previous,newValue=Arg("enabled")=="true"}}));
  }
  if(e.Intent.Kind=="adminControl"){
   var op=Arg("operation");if(op=="reload"){if(minigames is null)return new(false,"unavailable","Spieldienst fehlt.");minigames.UpdateConfiguration(ReadAdmin());}
   else if(op=="hotTimeStop")points.HotTime.End();
   else {if(minigames is null)return new(false,"unavailable","Spieldienst fehlt.");await minigames.ControlAdministrativeAsync(op,ct);}
   return new(true,"applied","Liveaktion vom bestehenden Spieldienst ausgeführt.",JsonSerializer.Serialize(new{message="Liveaktion ausgeführt."}));
  }
  var bytes=await points.ReadCommittedFileAsync(ct);using var source=JsonDocument.Parse(bytes??"{}"u8.ToArray());
  if(e.Intent.Kind=="adminPlayers"){
   var query=Arg("query");var entries=source.RootElement.EnumerateObject().Where(p=>query=="-"||p.Name.Contains(query)||p.Value.GetProperty("DisplayName").GetString()!.Contains(query,StringComparison.OrdinalIgnoreCase)).Take(50).Select(p=>new{id=p.Name,name=p.Value.GetProperty("DisplayName").GetString(),level=p.Value.TryGetProperty("Rpg",out var rp)&&rp.TryGetProperty("PlayerLevel",out var lv)?lv.GetInt32():1});
   return Reply(new{players=entries});
  }
  var targetId=Arg("targetUserId");if(!source.RootElement.TryGetProperty(targetId,out _))return new(false,"profile_missing","Spieler hat noch keinen Spielstand.");
  var account=(await points.GetProfileAsync(targetId,ct)).Entry;var operation=Arg("operation");
  if(operation=="profile"){
   var view=JsonSerializer.Deserialize<ViewerPointEntry>(JsonSerializer.Serialize(account))!;
   if(!owner){var forbidden=view.Rpg.Inventory.Where(i=>i.Rarity==ItemRarity.Divine).Select(i=>i.InstanceId).ToHashSet();view.Rpg.Inventory.RemoveAll(i=>i.Rarity==ItemRarity.Divine);foreach(var slot in view.Rpg.Equipment.Where(s=>forbidden.Contains(s.Value)).Select(s=>s.Key).ToArray())view.Rpg.Equipment.Remove(slot);}
   var projection=SnapshotValidation.Validate(new(1,channel,targetId,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(view)),channel);
   return Reply(new{profile=JsonDocument.Parse(projection.AccountJson!).RootElement.Clone(),statistics=new{account.RaceWins,account.BossDefeats,account.GamesPlayed,account.Wins,account.Losses,account.BiggestWin,account.WatchMinutes,fish=account.FishCollection.Sum(f=>f.Value)},divineHidden=!owner});
  }
  if(!owner)return new(false,"admin_required","Spielstandänderungen sind dem Kanalinhaber vorbehalten.");
  long value=0;if(operation is "pearls" or "experience" or "material")if(!long.TryParse(Arg("value"),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)||value is <-1000000000 or >1000000000)return new(false,"invalid_value","Ungültige Menge.");
  object? auditValue=null;
  var result=await points.ApplyRpgAsync(targetId,account.DisplayName,"admin:"+e.Intent.ActionId.ToString("N"),a=>{
   if(operation=="pearls"){var total=a.Points+value;if(total<0||cfg.MaximumAccountEnabled&&total>cfg.MaximumAccountPoints)return new(false,"Kontolimit oder Perlenbestand überschritten.");auditValue=new{oldValue=a.Points,newValue=total};a.Points=total;}
   else if(operation=="experience"){
    var xp=a.Rpg.Experience+value;if(xp<0)return new(false,"EXP können nicht negativ werden.");var level=1;while(level<rpg.Health.MaximumLevel&&xp>=(long)rpg.Health.ExperiencePerLevel*level*(level+1)/2)level++;
    try{TalentRules.Validate(level,a.Rpg.TalentRanks);}catch(InvalidDataException){return new(false,"Levelsenkung würde vorhandene Talentpunkte ungültig machen.");}
    auditValue=new{oldValue=a.Rpg.Experience,newValue=xp};a.Rpg.Experience=xp;a.Rpg.PlayerLevel=level;RpgHealthService.Refresh(a.Rpg,rpg.Health,DateTimeOffset.UtcNow);
   }else if(operation=="material"){
    var id=Arg("instanceId");if(!RpgUpgradeCatalog.Materials.Any(m=>m.Id==id)||value<0||value>1000000)return new(false,"Ungültiger Materialbestand.");var previous=RpgUpgradeCatalog.Count(a.Rpg.Materials,id);RpgUpgradeCatalog.Add(a.Rpg.Materials,id,(int)value-previous);auditValue=new{material=id,oldValue=previous,newValue=value};
   }else if(operation=="removeItem"){
    var item=a.Rpg.Inventory.SingleOrDefault(i=>i.InstanceId==Arg("instanceId"));if(item is null)return new(false,"Instanz fehlt.");if(item.Rarity==ItemRarity.Divine&&targetId!=channel)return new(false,"Göttliche Items sind geschützt.");auditValue=new{oldValue=item.InstanceId,newValue="entfernt"};a.Rpg.Inventory.Remove(item);foreach(var slot in a.Rpg.Equipment.Where(s=>s.Value==item.InstanceId).Select(s=>s.Key).ToArray())a.Rpg.Equipment.Remove(slot);RpgHealthService.Refresh(a.Rpg,rpg.Health,DateTimeOffset.UtcNow);
   }else if(operation=="resetTest"){
    if(targetId==channel||!rpg.AdminAccess.TestPlayers.Contains(targetId)||Arg("value")!="RESET:"+targetId)return new(false,"Zurücksetzen erfordert einen ausdrücklich registrierten Testspieler und seine Bestätigung.");
    auditValue=new{oldValue="Testspielstand",newValue="zurückgesetzt"};a.Rpg=new();a.Points=0;
   }else return new(false,"Unbekannte Spieleraktion.");
   return new(true,"Spieleränderung gespeichert.");
  },ct);
  return new(result.Success,result.Success?"applied":"rule_rejected",result.Error,JsonSerializer.Serialize(new{message=result.Error,audit=auditValue}));
 }
 private async Task AuditAdminAsync(ActionEnvelope e,ActionOutcome outcome,CancellationToken ct)
 {
  if(!RpgIntents.IsAdmin(e.Intent.Kind)||e.Intent.Kind is "adminDashboard" or "adminConfigRead" or "adminPlayers")return;
  JsonElement? details=null;try{if(outcome.DataJson is not null){using var d=JsonDocument.Parse(outcome.DataJson);if(d.RootElement.TryGetProperty("audit",out var a))details=a.Clone();}}catch(JsonException){}
  var target=e.Intent.Arguments.TryGetProperty("targetUserId",out var id)?id.GetString():null;
  var entry=JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,actor=e.UserId,action=e.Intent.Kind,target,success=outcome.Success,code=outcome.Code,details});
  await using var file=new FileStream(Path.Combine(journal,"admin-audit.jsonl"),FileMode.Append,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);var data=System.Text.Encoding.UTF8.GetBytes(entry+"\n");await file.WriteAsync(data,ct);await file.FlushAsync(ct);file.Flush(true);
 }
}
