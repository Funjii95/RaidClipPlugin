using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public class RpgPercentHealingTests
{
 [Theory] [InlineData(100,2)] [InlineData(500,10)] [InlineData(1000,20)] [InlineData(5000,100)] [InlineData(10000,200)]
 public void Passive_heals_two_percent_per_minute(int maximum,int expected)
 {
  var now=DateTimeOffset.UtcNow;var p=new RpgProfile();var c=new RpgHealthConfig{BaseHp=maximum};RpgHealthService.Refresh(p,c,now);p.Health.CurrentHp=1;
  RpgHealthService.Refresh(p,c,now.AddMinutes(1));Assert.Equal(1+expected,p.Health.CurrentHp);
  RpgHealthService.Refresh(p,c,now.AddMinutes(1));Assert.Equal(1+expected,p.Health.CurrentHp);
 }
 [Fact] public void Fractional_healing_survives_serialization_and_short_intervals()
 {
  var now=DateTimeOffset.UtcNow;var p=new RpgProfile();var c=new RpgHealthConfig{BaseHp=127,PassiveSeconds=1};RpgHealthService.Refresh(p,c,now);p.Health.CurrentHp=10;
  for(var n=1;n<=60;n++){RpgHealthService.Refresh(p,c,now.AddSeconds(n));p=System.Text.Json.JsonSerializer.Deserialize<RpgProfile>(System.Text.Json.JsonSerializer.Serialize(p))!;}
  Assert.Equal(12,p.Health.CurrentHp);Assert.InRange(p.Health.SushiRegenRemainder,0.539m,0.541m);
 }
 [Theory] [InlineData(0,0)] [InlineData(99,100)]
 public async Task Ordinary_chat_and_passive_healing_never_revive_or_overheal(int hp,int expected)
 {
  var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
  try{var now=DateTimeOffset.UtcNow;var store=new ViewerPointStore(root);var c=new RpgConfig{Enabled=true};
   await store.ApplyRpgAsync("42","Otter","seed",a=>{RpgHealthService.Refresh(a.Rpg,c.Health,now);a.Rpg.Health.CurrentHp=hp;return new(true);},default);
   await RpgHealthService.HealChatAsync(store,c,new(){UserId="42",UserName="Otter",Id="first",Text="hello"},now.AddMinutes(1),default);
   Assert.Equal(expected,(await store.GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentHp);
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
 [Theory] [InlineData(100,10)] [InlineData(500,50)] [InlineData(1000,100)] [InlineData(5000,500)] [InlineData(10000,1000)]
 public async Task Chat_uses_ten_percent_and_persistent_thirty_second_cooldown(int maximum,int expected)
 {
  var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
  try{var now=DateTimeOffset.UtcNow;var store=new ViewerPointStore(root);var c=new RpgConfig{Enabled=true};c.Health.BaseHp=maximum;c.Health.PassiveHealing=false;
   await store.ApplyRpgAsync("42","Otter","seed",a=>{RpgHealthService.Refresh(a.Rpg,c.Health,now);a.Rpg.Health.CurrentHp=1;return new(true);},default);
   async Task Chat(string id,int seconds)=>await RpgHealthService.HealChatAsync(store,c,new(){UserId="42",UserName="Otter",Id=id,Text="hello"},now.AddSeconds(seconds),default);
   await Chat("first",0);await Chat("second",29);Assert.Equal(1+expected,(await store.GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentHp);
   store=new(root);await Chat("first",30);Assert.Equal(1+expected,(await store.GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentHp);
   await Chat("third",30);Assert.Equal(1+2*expected,(await store.GetProfileAsync("42",default)).Entry.Rpg.Health.CurrentHp);
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
