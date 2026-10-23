using System.Text.Json;
using System.Globalization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed partial class RemoteRpgExecutor
{
 private async Task<ActionOutcome> InvokeEternalAdminAsync(ActionEnvelope e,CancellationToken ct)
 {
  if(!StaffAllowed(e))return new(false,"admin_required","Keine Freigabe für diese Verwaltungsaktion.");
  string Arg(string key)=>e.Intent.Arguments.GetProperty(key).GetString()!;
  var c=configuration().Adventures.Rpg;
  if(e.Intent.Kind=="adminZoneDebug") {
   var zone=c.Zones.Get(Arg("zoneId"));var operation=Arg("operation");
   if(operation.EndsWith("Chance")||operation.StartsWith("eternal")) {
    if(saveZones is null)return new(false,"unavailable","Dauerhafte Gebietsspeicherung nicht verfügbar.");
    var next=JsonSerializer.Deserialize<RpgZoneConfig>(JsonSerializer.Serialize(c.Zones))!;
    var value=decimal.Parse(Arg("value"),CultureInfo.InvariantCulture);var selected=next.Get(zone.Id);
    if(operation=="chestChance")selected.ChestChance=value;else if(operation=="mimicChance")selected.MimicChance=value;else if(operation=="enemyChance")selected.EnemyChance=value;
    else {var key=operation[7..].ToLowerInvariant();if(!next.EternalChances.ContainsKey(key))return new(false,"invalid_setting","Unbekannte Dropchance.");next.EternalChances[key]=value;}
    try{next.Validate();saveZones(next);c.Zones=next;}catch(InvalidOperationException){return new(false,"invalid_setting","Ungültige Gebietsregeln: Ereignischancen zusammen höchstens 100 %. ");}
    return new(true,"applied","Gebietschancen dauerhaft gespeichert.",JsonSerializer.Serialize(new{message="Gebietschancen gespeichert.",zones=next}));
   }
   // All debug rewards are generated on a detached copy and discarded. No normal player write.
   var owner=(await points.GetProfileAsync(channel,ct)).Entry;
   var draft=e.UserId==channel?JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(owner.Rpg))!:new RpgProfile{PlayerLevel=zone.Level};
   draft.SelectedDivingZone=zone.Id;draft.Health.CurrentHp=RpgHealthService.Maximum(draft,c.Health);
   var rng=new CryptoDuelRandom();object result;
   if(operation=="chest")result=new{materials=RpgZoneService.Materials(draft,c,zone,"chest",rng),equipment=Array.Empty<string>()};
   else if(operation=="simulate"){
    var count=0;for(int n=0;n<1000;n++)if(RpgEternalLoot.Victory(draft,c,zone.Id,zone.Level,"normal",true,rng,DateTimeOffset.UtcNow)is not null)count++;
    result=new{trials=1000,eternalDrops=count,chances=c.Zones.EternalChances};
   }else result=new RpgEnemyService(rng).Fight(draft,c,zone,operation=="mimic"?"mimic":"normal",int.Parse(Arg("enemyIndex")),DateTimeOffset.UtcNow);
   return new(true,"read","Simulation: keine Spielstände oder Belohnungen gespeichert.",JsonSerializer.Serialize(new{message="Nur Simulation · keine echten Belohnungen",zone,result}));
  }
  var target=Arg("targetUserId");var op=Arg("operation");
  if(target!=channel)return new(false,"admin_required","Göttliche Ausrüstung ist ausschließlich für die autorisierte Kanalinhaber-ID bestimmt.");
  var bytes=await points.ReadCommittedFileAsync(ct);if(bytes is null)return new(false,"profile_missing","Admin-Spielstand fehlt.");
  using var players=JsonDocument.Parse(bytes);if(!players.RootElement.TryGetProperty(target,out _))return new(false,"profile_missing","Admin-Spielstand fehlt.");
  var account=(await points.GetProfileAsync(target,ct)).Entry;
  if(op=="list")return new(true,"read","Göttliche Items geladen.",JsonSerializer.Serialize(new{message="Göttliche Items geladen.",items=account.Rpg.Inventory.Where(i=>i.Rarity==ItemRarity.Divine).Select(i=>new{i.InstanceId,i.ItemId,i.DisplayName,equipped=account.Rpg.Equipment.Values.Contains(i.InstanceId)})}));
  var applied=await points.ApplyRpgAsync(target,account.DisplayName,"admin:"+e.Intent.ActionId.ToString("N"),a=>{
   if(op=="grantSet"){
    foreach(var definition in RpgEternalCatalog.Items().Where(i=>i.Rarity==ItemRarity.Divine)){
     if(a.Rpg.Inventory.Any(i=>i.ItemId==definition.ItemId))continue;
     var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;item.InstanceId=Guid.NewGuid().ToString("N");item.FoundAt=DateTimeOffset.UtcNow;a.Rpg.Inventory.Add(item);
    }
   }else if(op=="remove"){
    var ids=a.Rpg.Inventory.Where(i=>i.Rarity==ItemRarity.Divine).Select(i=>i.InstanceId).ToHashSet();
    foreach(var slot in a.Rpg.Equipment.Where(p=>ids.Contains(p.Value)).Select(p=>p.Key).ToArray())a.Rpg.Equipment.Remove(slot);
    a.Rpg.Inventory.RemoveAll(i=>ids.Contains(i.InstanceId));RpgHealthService.Refresh(a.Rpg,c.Health,DateTimeOffset.UtcNow);
   }else return new(false,"Unbekannte Divine-Aktion.");
   try{SnapshotValidation.Validate(new(1,channel,target,1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(a)),channel);}catch(InvalidDataException){return new(false,"Inventar überschreitet Cloud-Größenlimit.");}
   return new(true,op=="remove"?"Göttliche Items entfernt.":"Vollständiges göttliches Set gespeichert.");
  },ct);
  return new(applied.Success,applied.Success?"applied":"rule_rejected",applied.Error,JsonSerializer.Serialize(new{message=applied.Error}));
 }
}
