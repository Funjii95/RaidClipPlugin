using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
public sealed class RpgPartyService(ViewerPointStore points,RpgConfig config,Func<DateTimeOffset>? clock=null)
{
 private DateTimeOffset Now=>clock?.Invoke()??DateTimeOffset.UtcNow;
 public Task<RpgTransactionResult> ApplyAsync(string actor,string name,string request,string operation,string target,string invitation,CancellationToken ct)=>points.ApplyPartyAsync(actor,name,request,(world,get)=>{
  if(!config.Enabled||!config.Multiplayer.Enabled)return new(false,"Gruppen sind nicht freigegeben.");var now=Now;
  foreach(var id in world.Invitations.Where(i=>i.Value.ExpiresAt<=now).Select(i=>i.Key).ToArray())world.Invitations.Remove(id);
  var party=world.For(actor);
  if(operation=="create"){
   if(party is not null)return new(false,"Du gehörst bereits einer Gruppe an.");if(world.Parties.Count>=4096)return new(false,"Derzeit sind keine weiteren Gruppen möglich.");
   var created=new PartyState{Id=Guid.NewGuid().ToString("N"),LeaderId=actor,Members=[actor],CreatedAt=now};world.Parties[created.Id]=created;return new(true);
  }
  if(operation is "accept" or "decline"){
   if(!world.Invitations.TryGetValue(invitation,out var invite)||invite.TargetId!=actor||invite.ExpiresAt<=now)return new(false,"Einladung ist nicht mehr gültig.");
   if(operation=="decline"){world.Invitations.Remove(invitation);return new(true);}
   if(party is not null)return new(false,"Du gehörst bereits einer Gruppe an.");
   if(!world.Parties.TryGetValue(invite.PartyId,out var invited)||invited.Members.Count>=4)return new(false,"Die Gruppe ist voll oder wurde aufgelöst.");
   if(invited.Instance is {Status:"active"} active&&active.ExpiresAt>now)return new(false,"Beitritt erst nach dem laufenden Kampf.");
   foreach(var member in invited.Members)get(member);invited.Members.Add(actor);invited.Revision++;
   foreach(var id in world.Invitations.Where(i=>i.Value.TargetId==actor).Select(i=>i.Key).ToArray())world.Invitations.Remove(id);
   return new(true);
  }
  if(party is null)return new(false,"Du gehörst keiner Gruppe an.");foreach(var member in party.Members)get(member);
  if(operation=="invite"){
   if(party.LeaderId!=actor)return new(false,"Nur die Gruppenleitung kann einladen.");if(party.Members.Count>=4)return new(false,"Die Gruppe hat bereits vier Mitglieder.");
   if(!PartySnapshot.User(target)||target==actor||world.For(target) is not null)return new(false,"Ziel ist ungültig oder bereits in einer Gruppe.");
   if(world.Invitations.Values.Count(i=>i.TargetId==target)>=16||world.Invitations.Count>=4096)return new(false,"Für dieses Ziel sind bereits zu viele Einladungen offen.");
   if(world.Invitations.Values.Any(i=>i.PartyId==party.Id&&i.TargetId==target))return new(false,"Diese Einladung ist bereits offen.");
   try{get(target);}catch(InvalidOperationException){return new(false,"Der Zielspieler benötigt zuerst einen eigenen RPG-Spielstand.");}
   var invite=new PartyInvitation{Id=Guid.NewGuid().ToString("N"),PartyId=party.Id,InviterId=actor,TargetId=target,ExpiresAt=now.AddSeconds(config.Multiplayer.InvitationSeconds)};world.Invitations[invite.Id]=invite;return new(true);
  }
  if(operation is "kick" or "transfer" or "disband"&&party.LeaderId!=actor)return new(false,"Nur die Gruppenleitung darf das.");
  if(operation is "kick" or "transfer"&&(!party.Members.Contains(target)||target==actor))return new(false,"Wähle ein anderes Gruppenmitglied.");
  if(operation=="transfer"){party.LeaderId=target;party.Revision++;foreach(var id in world.Invitations.Where(i=>i.Value.PartyId==party.Id).Select(i=>i.Key).ToArray())world.Invitations.Remove(id);return new(true);}
  if(operation is "leave" or "kick" or "disband"){
   // Removing a participant aborts the shared instance for everyone, without new rewards.
   if(party.Instance is {Status:"active"} instance){instance.Status="aborted";instance.Log.Add(new(){Turn=instance.Turn,ActorId=actor,Action="party-aborted"});if(instance.Log.Count>60)instance.Log.RemoveAt(0);}
   if(operation=="disband"){world.Parties.Remove(party.Id);foreach(var id in world.Invitations.Where(i=>i.Value.PartyId==party.Id).Select(i=>i.Key).ToArray())world.Invitations.Remove(id);}
   else{var removed=operation=="leave"?actor:target;party.Members.Remove(removed);if(party.Instance is {} i){ViewerPointStore.RemovePartyParticipant(i,removed);}if(party.Members.Count==0)world.Parties.Remove(party.Id);else if(party.LeaderId==removed)party.LeaderId=party.Members[0];party.Revision++;}
   return new(true);
  }
  return new(false,"Unbekannte Gruppenaktion.");
 },ct);
}
