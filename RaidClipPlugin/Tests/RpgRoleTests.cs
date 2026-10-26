using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using System.Text.Json;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgRoleTests
{
 [Fact] public void Roles_have_real_distinct_combat_bonuses_without_reducing_solo_damage()
 {
  var c=new RpgConfig();var p=new RpgProfile();var normal=CharacterStatService.Calculate(p,c,RpgActivity.Dive);Assert.Equal(0,normal.Damage);
  p.Role=RpgRole.Damage;Assert.Equal(15,CharacterStatService.Calculate(p,c,RpgActivity.Dive).Damage);
  p.Role=RpgRole.Tank;var tank=CharacterStatService.Calculate(p,c,RpgActivity.Dive);Assert.Equal(20,tank.ArmorPercent);Assert.Equal(15,tank.BossDamageReduction);Assert.Equal(normal.Damage,tank.Damage);Assert.Equal(300,RpgRoleService.Threat(p,c,100,0));
  p.Role=RpgRole.Healer;var heal=CharacterStatService.Calculate(p,c,RpgActivity.Dive);Assert.Equal(20,heal.HealingPower);Assert.Equal(5,heal.Damage);
  c.Health.BaseHp=1000;var now=DateTimeOffset.UtcNow;RpgHealthService.Refresh(p,c.Health,now);p.Health.CurrentHp=1;Assert.Equal(120,RpgSetCombatService.Heal(p,c.Health,100,now));
 }
 [Fact] public async Task Role_selection_is_atomic_persistent_idempotent_and_locked_during_an_active_event()
 {
  var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
  try{var c=new RpgConfig{Enabled=true};var s=new ViewerPointStore(root);
   Assert.True((await s.SelectRoleAsync("42","Otter","role-first",RpgRole.Tank,c,default)).Success);
   Assert.True((await s.SelectRoleAsync("42","Otter","role-first",RpgRole.Tank,c,default)).Duplicate);
   s=new(root);Assert.Equal(RpgRole.Tank,(await s.GetProfileAsync("42",default)).Entry.Rpg.Role);
   await s.SaveGlobalBossAsync(new(){Id="round",Active=true,Remaining=1,Participants=new(){["42"]=new(){UserId="42",Name="Otter"}}},default);
   Assert.False((await s.SelectRoleAsync("42","Otter","role-blocked",RpgRole.Healer,c,default)).Success);
   Assert.Equal(RpgRole.Tank,(await s.GetProfileAsync("42",default)).Entry.Rpg.Role);
   var round=(await s.ReadGlobalBossAsync(default))!;round.Active=false;await s.SaveGlobalBossAsync(round,default);
   Assert.True((await s.SelectRoleAsync("42","Otter","role-after",RpgRole.Healer,c,default)).Success);
   var account=(await s.GetProfileAsync("42",default)).Entry;var projection=SnapshotValidation.Validate(new(1,"42","42",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),"42");
   Assert.Contains("\"Role\":2",projection.AccountJson);
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 [Theory] [InlineData("admin")] [InlineData("Tank")] [InlineData("4")]
 public void Unrecognised_roles_are_rejected_by_the_shared_wire_contract(string role)
 {Assert.Throws<ArgumentException>(()=>RpgIntents.Canonical(new(Guid.NewGuid(),"selectRole",JsonSerializer.SerializeToElement(new{role}))));}
}
