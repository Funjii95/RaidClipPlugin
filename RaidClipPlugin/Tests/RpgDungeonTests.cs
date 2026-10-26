using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgDungeonTests:IDisposable
{
 private readonly string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));private DateTimeOffset now=DateTimeOffset.UtcNow;
 private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
 private sealed class LowRandom:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
 public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
 private async Task<ViewerPointStore> Seed(int level=99){config.Adventures.Rpg.Enabled=true;config.Adventures.Rpg.Health.BaseHp=5000;config.Adventures.Rpg.AreaBosses.Enabled=true;var points=new ViewerPointStore(root);foreach(var id in new[]{"1","2"})await points.ApplyRpgAsync(id,"Otter"+id,"seed",a=>{a.Points=100;a.Rpg.PlayerLevel=level;a.Rpg.Role=RpgRole.Damage;RpgHealthService.Refresh(a.Rpg,config.Adventures.Rpg.Health,now);return new(true);},default);return points;}
 private async Task<RpgTransactionResult> Act(ViewerPointStore points,string op,string actor="1",string difficulty="normal",string target="",string? request=null){now=now.AddSeconds(2);return await new RpgPartyCombatService(points,config,new LowRandom(),()=>now).ExecuteAsync(actor,"Otter"+actor,request??Guid.NewGuid().ToString("N"),op,"sunny_lagoon","runen_halls",difficulty,target,default);}
 [Theory][InlineData("normal")][InlineData("heroic")][InlineData("mythic")]
 public async Task Solo_run_has_seven_encounters_and_rewards_only_once_after_restart(string difficulty){
  var points=await Seed();Assert.True((await Act(points,"startDungeon",difficulty:difficulty)).Success);var stages=new HashSet<int>();string final="";
  for(var turn=0;turn<700;turn++){
   var p=(await points.GetProfileAsync("1",default)).Entry.Rpg;var i=p.Party.Party!.Instance!;stages.Add(i.Stage);if(i.Status!="active"){Assert.Equal("won",i.Status);break;}
   Assert.True(p.Health.CurrentHp>0,$"Died at stage {i.Stage}");
   if(i.WaveCleared){Assert.True((await Act(points,"recover")).Success);Assert.True((await Act(points,"advance")).Success);continue;}
   var op=i.PendingAbility.Length>0?"interrupt":"attack";if(op=="interrupt"&&i.Cooldowns.GetValueOrDefault("1:interrupt")>i.Turn)op="attack";final=Guid.NewGuid().ToString("N");Assert.True((await Act(points,op,request:final)).Success);
  }
  var won=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal("won",won.Rpg.Party.Party!.Instance!.Status);Assert.Equal(7,stages.Count);Assert.Single(won.Rpg.Party.Party.Instance.Rewarded);Assert.Equal(1,won.Rpg.Statistics.Dungeons[difficulty]);
  points=new(root);Assert.True((await Act(points,"attack",request:final)).Duplicate);Assert.False((await Act(points,"attack")).Success);var after=(await points.GetProfileAsync("1",default)).Entry;Assert.Equal(won.Points,after.Points);Assert.Equal(won.Rpg.Inventory.Count,after.Rpg.Inventory.Count);
  SnapshotValidation.Validate(new(1,"1","1",1,Guid.NewGuid(),now,SnapshotKind.Upsert,JsonSerializer.Serialize(after)),"1");
 }
 [Fact] public async Task Shared_hp_role_locks_healing_and_abandon_use_one_persisted_instance(){
  var points=await Seed();var party=new RpgPartyService(points,config.Adventures.Rpg,()=>now);Assert.True((await party.ApplyAsync("1","Otter1","create","create","","",default)).Success);await party.ApplyAsync("1","Otter1","invite","invite","2","",default);var invite=(await points.GetProfileAsync("2",default)).Entry.Rpg.Party.Invitations.Single().Id;Assert.True((await party.ApplyAsync("2","Otter2","join","accept","",invite,default)).Success);
  await points.SelectRoleAsync("2","Otter2","healer",RpgRole.Healer,config.Adventures.Rpg,default);
  Assert.True((await Act(points,"startDungeon")).Success);Assert.False((await Act(points,"startDungeon",actor:"2")).Success);Assert.False((await points.SelectRoleAsync("2","Otter2","switch",RpgRole.Tank,config.Adventures.Rpg,default)).Success);
  var before=(await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party!.Instance!.Hp;Assert.True((await Act(points,"attack")).Success);var one=(await points.GetProfileAsync("1",default)).Entry.Rpg;var two=(await points.GetProfileAsync("2",default)).Entry.Rpg;Assert.True(one.Party.Party!.Instance!.Hp<before);Assert.Equal(one.Party.Party.Instance.Hp,two.Party.Party!.Instance!.Hp);
  await points.ApplyRpgAsync("1","Otter1","wound",a=>{a.Rpg.Health.CurrentHp-=500;return new(true);},default);one=(await points.GetProfileAsync("1",default)).Entry.Rpg;
  Assert.True((await Act(points,"heal",actor:"2",target:"1")).Success);var healed=(await points.GetProfileAsync("1",default)).Entry.Rpg;Assert.True(healed.Health.CurrentHp>one.Health.CurrentHp);Assert.True((await points.GetProfileAsync("2",default)).Entry.Rpg.Statistics.Counters.GetValueOrDefault("groupHealing")>0);
  Assert.True((await party.ApplyAsync("2","Otter2","leave-instance","leave","","",default)).Success);points=new(root);Assert.Single((await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party!.Members);Assert.False((await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party!.Instance!.DamageScale.ContainsKey("2"));Assert.False((await Act(points,"attack")).Success);Assert.Empty((await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party!.Instance!.Rewarded);
 }
 [Fact] public async Task Minimum_level_and_combat_contract_reject_forged_actions(){
  var points=await Seed(1);Assert.False((await Act(points,"startDungeon")).Success);Assert.Null((await points.GetProfileAsync("1",default)).Entry.Rpg.Party.Party);
  foreach(var args in new[]{new{operation="startDungeon",dungeonId="fake",difficulty="normal"},new{operation="startDungeon",dungeonId="runen_halls",difficulty="admin"}})Assert.Throws<ArgumentException>(()=>RpgIntents.Canonical(new(Guid.NewGuid(),"partyCombat",JsonSerializer.SerializeToElement(args))));
  Assert.Throws<ArgumentException>(()=>RpgIntents.Canonical(new(Guid.NewGuid(),"partyCombat",JsonSerializer.SerializeToElement(new{operation="attack",damage="999999"}))));
  Assert.NotEmpty(RpgIntents.Canonical(new(Guid.NewGuid(),"partyCombat",JsonSerializer.SerializeToElement(new{operation="heal",targetUserId="auto"}))));
 }
}
