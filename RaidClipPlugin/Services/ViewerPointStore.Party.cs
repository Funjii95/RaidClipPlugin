using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using RaidClipPlugin.Models;
using SnapshotProtocol;
namespace RaidClipPlugin.Services;
internal sealed class RpgPartyWorld
{
 public int Version {get;set;}=1;public Dictionary<string,PartyState> Parties {get;set;}=[];public Dictionary<string,PartyInvitation> Invitations {get;set;}=[];
 public PartyState? For(string user)=>Parties.Values.SingleOrDefault(p=>p.Members.Contains(user));
 public void Validate(){if(Version!=1||Parties.Any(p=>p.Key!=p.Value.Id)||Invitations.Any(i=>i.Key!=i.Value.Id)||Parties.Count>4096||Invitations.Count>4096||Parties.SelectMany(p=>p.Value.Members).GroupBy(x=>x).Any(g=>g.Count()>1))throw new InvalidDataException("Invalid party world.");foreach(var p in Parties.Values)PartySnapshot.Validate(new(){Party=p},p.LeaderId);foreach(var i in Invitations.Values)PartySnapshot.Validate(new(){Invitations=[i]},i.TargetId);}
}
public sealed partial class ViewerPointStore
{
 private RpgPartyWorld _partyWorld=new();
 // Only query from callbacks already holding the account store lock.
 internal bool HasActivePartyCombatUnsafe(string user,DateTimeOffset now)=>_partyWorld.For(user)?.Instance is {Status:"active"} i&&i.ExpiresAt>now;
 internal bool HasGlobalBossParticipantsUnsafe(IEnumerable<string> users)=>_globalBoss is {Active:true} b&&users.Any(b.Participants.ContainsKey);
 private readonly ConcurrentDictionary<string,DateTimeOffset> _rpgPresence=new();
 internal void ObserveRpgActivity(string user,DateTimeOffset now)=>_rpgPresence[user]=now;
 private static T PartyCopy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
 private void ReadPartyWorld(JsonObject node){_partyWorld=node["$partyWorld"]?.Deserialize<RpgPartyWorld>(JsonOptions)??new();node.Remove("$partyWorld");_partyWorld.Validate();}
 internal static void RemovePartyParticipant(PartyInstance instance,string user){instance.Threat.Remove(user);instance.Shields.Remove(user);instance.Rewarded.Remove(user);instance.DamageScale.Remove(user);if(instance.LastActorId==user)instance.LastActorId="";foreach(var key in instance.Cooldowns.Keys.Where(key=>key.Split(':').Contains(user)).ToArray())instance.Cooldowns.Remove(key);}
 private void WritePartyWorld(JsonObject node)
 {
  var removed=_partyWorld.Parties.Values.SelectMany(p=>p.Members).Where(IsPrivacyBlocked).Distinct().ToArray();
  foreach(var user in removed){foreach(var p in _partyWorld.Parties.Values.Where(p=>p.Members.Contains(user)).ToArray()){p.Members.Remove(user);if(p.Instance is {} instance){instance.Status="aborted";RemovePartyParticipant(instance,user);instance.Log.Clear();}if(p.Members.Count==0)_partyWorld.Parties.Remove(p.Id);else if(p.LeaderId==user)p.LeaderId=p.Members[0];}}
  foreach(var id in _partyWorld.Invitations.Where(i=>IsPrivacyBlocked(i.Value.TargetId)||IsPrivacyBlocked(i.Value.InviterId)||!_partyWorld.Parties.ContainsKey(i.Value.PartyId)).Select(i=>i.Key).ToArray())_partyWorld.Invitations.Remove(id);
  if(removed.Length>0)foreach(var (id,account) in _entries!){account.Rpg.Party=PartyReplica(id,_partyWorld);node[id]=JsonSerializer.SerializeToNode(account,JsonOptions);}
  if(_partyWorld.Parties.Count>0||_partyWorld.Invitations.Count>0)node["$partyWorld"]=JsonSerializer.SerializeToNode(_partyWorld,JsonOptions);
 }
 private static PartySnapshot PartyReplica(string user,RpgPartyWorld world)=>new(){Party=world.For(user) is {} party?PartyCopy(party):null,Invitations=world.Invitations.Values.Where(i=>i.TargetId==user).Select(PartyCopy).ToList()};
 internal async Task<RpgTransactionResult> ApplyPartyAsync(string actor,string name,string action,Func<RpgPartyWorld,Func<string,ViewerPointEntry>,RpgMutation> mutate,CancellationToken ct)
 {
  using var operation=Admission?.EnterOperation();if(!PartySnapshot.User(actor)||string.IsNullOrWhiteSpace(action)||action.Length>200)throw new ArgumentException("Ungültige Gruppenaktion.");
  await _lock.WaitAsync(ct);try{
   await EnsureLoadedAsync(ct);RequireNotPrivacyBlocked(actor);var oldWorld=_partyWorld;var world=PartyCopy(_partyWorld);var drafts=new Dictionary<string,ViewerPointEntry>();
   ViewerPointEntry Get(string id){RequireNotPrivacyBlocked(id);if(drafts.TryGetValue(id,out var draft))return draft;if(!_entries!.TryGetValue(id,out var original))throw new InvalidOperationException("Spieler ist noch nicht im RPG registriert.");draft=PartyCopy(original);PrepareRpg(draft);drafts[id]=draft;return draft;}
   var account=Get(actor);if(account.Rpg.CompletedActions.Contains(action))return new(false,"Aktion bereits verarbeitet.",account.Points,true);
   var result=mutate(world,Get);if(!result.Success)return new(false,result.Error,account.Points);
   world.Validate();var now=DateTimeOffset.UtcNow;account.Rpg.CompletedActions.Add(action);account.DisplayName=name;
   foreach(var (id,draft) in drafts){draft.Rpg.Party=PartyReplica(id,world);PartySnapshot.Validate(draft.Rpg.Party,id);SnapshotProtocol.RpgStatistics.Validate(draft.Rpg.Statistics);if(draft.Points<0||draft.UserId!=id)throw new InvalidOperationException("Ungültiges Gruppenkonto.");draft.UpdatedAt=now;}
   var originals=drafts.Keys.ToDictionary(id=>id,id=>_entries![id]);_partyWorld=world;foreach(var (id,draft) in drafts)_entries![id]=draft;
   try{await SaveAsync(ct);}catch{_partyWorld=oldWorld;foreach(var (id,original) in originals)_entries![id]=original;throw;}
   return new(true,"",account.Points,Details:result.Details);
  }finally{_lock.Release();}
 }
 internal async Task<object> PartyViewAsync(string user,RaidClipPlugin.Config.RpgConfig config,DateTimeOffset now,CancellationToken ct)
 {
  await _lock.WaitAsync(ct);try{await EnsureLoadedAsync(ct);RequireNotPrivacyBlocked(user);var party=_partyWorld.For(user);return new{snapshot=PartyReplica(user,_partyWorld),inviters=_partyWorld.Invitations.Values.Where(i=>i.TargetId==user).Select(i=>i.InviterId).Distinct().ToDictionary(id=>id,id=>_entries!.TryGetValue(id,out var a)?a.DisplayName:id),members=party?.Members.Select(id=>{var a=_entries![id];var max=RpgHealthService.Maximum(a.Rpg,config.Health);return new{userId=id,name=a.DisplayName,level=a.Rpg.PlayerLevel,role=a.Rpg.Role.ToString(),hp=a.Rpg.Health.CurrentHp??max,maxHp=max,healthPercent=100m*(a.Rpg.Health.CurrentHp??max)/max,recentlyActive=_rpgPresence.TryGetValue(id,out var last)&&(now-last).TotalSeconds<45,leader=party.LeaderId==id};}).ToArray()};}finally{_lock.Release();}
 }
}
