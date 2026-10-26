using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgPartyTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));private DateTimeOffset now=DateTimeOffset.UtcNow;
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 private async Task<(ViewerPointStore,RpgPartyService)> Seed(){var points=new ViewerPointStore(root);foreach(var id in new[]{"1","2","3","4","5","6"})await points.AddPointsAsync(id,"Otter"+id,100,0,default);var config=new RpgConfig{Enabled=true};return(points,new(points,config,()=>now));}
 private Task<RpgTransactionResult> Act(RpgPartyService service,string who,string op,string target="",string invite="",string? request=null)=>service.ApplyAsync(who,"Otter"+who,request??Guid.NewGuid().ToString("N"),op,target,invite,default);
 private static async Task<string> Invite(ViewerPointStore points,RpgPartyService service,string leader,string target){Assert.True((await service.ApplyAsync(leader,"Otter"+leader,Guid.NewGuid().ToString("N"),"invite",target,"",default)).Success);return (await points.GetProfileAsync(target,default)).Entry.Rpg.Party.Invitations.Single(i=>i.InviterId==leader).Id;}
 [Fact] public async Task Invitations_are_authorized_expire_and_group_limit_is_atomic()
 {
  var (points,service)=await Seed();Assert.True((await Act(service,"1","create")).Success);
  var first=await Invite(points,service,"1","2");Assert.False((await Act(service,"3","accept",invite:first)).Success);Assert.True((await Act(service,"2","accept",invite:first)).Success);
  Assert.False((await Act(service,"2","invite","3")).Success);
  var third=await Invite(points,service,"1","3");var fourth=await Invite(points,service,"1","4");var fifth=await Invite(points,service,"1","5");
  var accepted=await Task.WhenAll(Act(service,"3","accept",invite:third),Act(service,"4","accept",invite:fourth),Act(service,"5","accept",invite:fifth));Assert.Equal(2,accepted.Count(x=>x.Success));
  var profile=(await new ViewerPointStore(root).GetProfileAsync("1",default)).Entry;Assert.Equal(4,profile.Rpg.Party.Party!.Members.Count);
  var projection=SnapshotValidation.Validate(new(1,"1","1",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(profile)),"1");Assert.Contains("\"Members\"",projection.AccountJson);
 }
 [Fact] public async Task Player_cannot_join_two_groups_and_old_invites_cannot_be_reused()
 {
  var (points,service)=await Seed();await Act(service,"1","create");await Act(service,"3","create");var one=await Invite(points,service,"1","2");var two=await Invite(points,service,"3","2");
  var results=await Task.WhenAll(Act(service,"2","accept",invite:one),Act(service,"2","accept",invite:two));Assert.Single(results,r=>r.Success);
  var actor=(await points.GetProfileAsync("2",default)).Entry.Rpg;Assert.NotNull(actor.Party.Party);Assert.Empty(actor.Party.Invitations);
 }
 [Fact] public async Task Expiry_transfer_kick_disband_and_duplicate_requests_preserve_memberships()
 {
  var (points,service)=await Seed();await Act(service,"1","create",request:"create-once");Assert.True((await Act(service,"1","create",request:"create-once")).Duplicate);
  var id=await Invite(points,service,"1","2");now=now.AddMinutes(3);Assert.False((await Act(service,"2","accept",invite:id)).Success);
  // A new invitation after the expired entry has been pruned.
  id=await Invite(points,service,"1","2");await Act(service,"2","accept",invite:id);Assert.False((await Act(service,"2","disband")).Success);
  await Act(service,"1","transfer","2");Assert.Equal("2",(await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party!.LeaderId);
  Assert.True((await Act(service,"2","kick","1")).Success);Assert.Null((await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party);
  Assert.True((await Act(service,"2","disband")).Success);Assert.Null((await new ViewerPointStore(root).GetProfileAsync("2",default)).Entry.Rpg.Party.Party);
 }
 [Fact] public async Task Failed_multi_account_save_rolls_back_every_member_and_invitation()
 {
  var (points,service)=await Seed();await Act(service,"1","create");var id=await Invite(points,service,"1","2");Directory.CreateDirectory(Path.Combine(root,"viewer-points.json.tmp"));
  await Assert.ThrowsAnyAsync<Exception>(()=>Act(service,"2","accept",invite:id));Directory.Delete(Path.Combine(root,"viewer-points.json.tmp"));
  points=new(root);Assert.Null((await points.GetProfileAsync("2",default)).Entry.Rpg.Party.Party);Assert.Single((await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party!.Members);
 }
}

