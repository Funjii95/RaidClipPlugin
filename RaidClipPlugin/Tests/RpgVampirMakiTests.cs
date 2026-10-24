using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgVampirMakiTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipVampir-"+Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now=DateTimeOffset.UtcNow;
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
    private RpgProfile Profile(int hp=100)=>new(){Health=new(){CurrentHp=hp},Buffs=new(){new(){RecipeId="vampir",Category=SushiCategory.Defensive,BossLifeleech=5,RemainingUses=3,EncounterBased=true,ActivatedAt=_now}}};
    [Theory]
    [InlineData(500,25)] [InlineData(1000,50)] [InlineData(5000,250)] [InlineData(10000,500)]
    public void HealingScalesWithoutFixedCap(int damage,int expected)
    {
        var p=Profile();var hp=new RpgHealthConfig{BaseHp=20000};
        var healed=RpgSushiService.HealFromBossAttack(damage,20000,p,hp,"boss",_now);
        Assert.Equal(expected,healed);Assert.Equal(100+expected,p.Health.CurrentHp);
    }
    [Fact]
    public void CriticalDamageCountsOverkillDoesNotAndMaximumHpStillApplies()
    {
        var p=Profile();var hp=new RpgHealthConfig{BaseHp=2000};
        var damage=CharacterStatService.BossDamage(1000,new(){CritChance=100},0,out var critical,2);
        Assert.True(critical);Assert.Equal(2000,damage);
        Assert.Equal(100,RpgSushiService.HealFromBossAttack(damage,2000,p,hp,"boss",_now));
        Assert.Equal(25,RpgSushiService.HealFromBossAttack(10000,500,p,hp,"boss",_now));
        p.Health.CurrentHp=1999;Assert.Equal(1,RpgSushiService.HealFromBossAttack(10000,10000,p,hp,"boss",_now));
        Assert.Equal(2000,p.Health.CurrentHp);
        Assert.Equal(0,RpgSushiService.HealFromBossAttack(0,1000,p,hp,"boss",_now));
    }
    [Fact]
    public void CannotResurrectOrBypassEncounterDeathLockAndDoesNotHealEnemyFights()
    {
        var p=Profile(0);Assert.Equal(0,RpgSushiService.HealFromBossAttack(10000,10000,p,new(),"boss",_now));
        p.Health.CurrentHp=50;p.Health.Defeated=true;p.Health.BossId="boss";
        Assert.Equal(0,RpgSushiService.HealFromBossAttack(10000,10000,p,new(),"boss",_now));Assert.Equal(50,p.Health.CurrentHp);
        p.Health.Defeated=false;
        RpgHealthService.ResolveEnemy(p,new(),10,true,0,_now);Assert.Equal(40,p.Health.CurrentHp);
    }
    [Fact]
    public void ThirdEncounterKeepsLifeleechUntilEndAndOffensiveSlotDoesNotGrantIt()
    {
        var p=Profile();var hp=new RpgHealthConfig{BaseHp=20000};
        for(int i=1;i<=3;i++)
        {
            var clock=_now.AddMinutes(i*3);var end=clock.AddMinutes(1);
            RpgSushiService.Participate(p,"boss"+i,end,clock);RpgSushiService.Participate(p,"boss"+i,end,clock);
            Assert.Equal(3-i,p.Buffs[0].RemainingUses);
            Assert.Equal(500,RpgSushiService.HealFromBossAttack(10000,10000,p,hp,"boss"+i,clock.AddSeconds(1)));
            RpgSushiService.Close(p,"boss"+i,end,end,hp);
        }
        Assert.Empty(p.Buffs);
        p=Profile();p.Buffs[0].Category=SushiCategory.Offensive;
        Assert.Equal(0,RpgSushiService.HealFromBossAttack(10000,10000,p,hp,"boss",_now));
    }
    [Theory]
    [InlineData("vampire_squid",300)] [InlineData("tuna",150)]
    public async Task BothRecipeIngredientsAreActuallyCatchableAndPersistent(string id,int reward)
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;
        foreach(var f in c.Adventures.Rpg.FishSpecies)if(f.Id!=id)f.Weight=0;
        c.Adventures.Fishing.LossChance=0;c.Adventures.Fishing.SmallChance=100;c.Adventures.Fishing.LargeChance=c.Adventures.Fishing.RareChance=0;
        var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","biome",a=>{a.Rpg.SelectedFishingZone=c.Adventures.Rpg.FishSpecies.Single(f=>f.Id==id).Zone;return new(true);},default);
        var result=await new RpgAdventureService(store,c,new Low(),()=>_now).ExecuteAsync("u","U","catch",RpgActivity.Fishing,0,default);
        Assert.True(result.Success);Assert.Equal(id,result.Item!.ItemId);Assert.Equal(reward,result.Balance);
        Assert.Equal(1,(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry.Rpg.Fish[id].Quantity);
    }
    [Fact]
    public async Task CraftAndActivationPreserveOffensiveSlotSnapshotAndStockAfterRestart()
    {
        var c=new RpgConfig{Enabled=true};var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Points=1000;a.Rpg.Fish=new(){{"vampire_squid",new(){FishId="vampire_squid",Quantity=2}},{"tuna",new(){FishId="tuna",Quantity=1}}};a.Rpg.Sushi["fighter"]=1;a.Rpg.Sushi["kappa"]=1;return new(true);},default);
        var service=new RpgProgressionService(store,c,()=>_now);
        Assert.True((await service.CraftAsync("u","U","craft","vampir",default)).Success);
        Assert.False((await service.CraftAsync("u","U","craft","vampir",default)).Success);
        Assert.True((await service.ActivateSushiAsync("u","U","off","fighter",default)).Success);
        Assert.True((await service.ActivateSushiAsync("u","U","vamp","vampir",default)).Success);
        Assert.False((await service.ActivateSushiAsync("u","U","blocked","kappa",default)).Success);
        c.Recipes.Single(r=>r.Id=="vampir").BossLifeleech=20;
        var p=(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry;
        Assert.Equal(250,p.Points);Assert.All(p.Rpg.Fish.Values,f=>Assert.Equal(0,f.Quantity));Assert.Equal(2,p.Rpg.Buffs.Count);
        var b=Assert.Single(p.Rpg.Buffs,b=>b.Category==SushiCategory.Defensive);Assert.Equal(5,b.BossLifeleech);Assert.Equal(3,b.RemainingUses);
        Assert.Equal(1,p.Rpg.Sushi["kappa"]);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealAttackHealsBeforeCounterattackAndDuplicateOrFailedSaveCannotHealTwice(bool failSave)
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;c.Adventures.Rpg.Health.PassiveHealing=false;
        c.Adventures.Rpg.Recipes.Single(r=>r.Id=="fighter").Attributes.Damage=100;
        c.Adventures.Boss.HitPoints=100;c.Adventures.Boss.AttackCooldownSeconds=1;
        var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{RpgHealthService.Refresh(a.Rpg,c.Adventures.Rpg.Health,_now);a.Rpg.Health.CurrentHp=10;a.Rpg.Sushi=new(){{"fighter",1},{"vampir",1}};return new(true);},default);
        var progression=new RpgProgressionService(store,c.Adventures.Rpg,()=>_now);
        await progression.ActivateSushiAsync("u","U","off","fighter",default);await progression.ActivateSushiAsync("u","U","def","vampir",default);
        var messages=new List<string>();var game=new AdventureGameService(store,c,(s,_)=>{messages.Add(s);return Task.CompletedTask;},new Low(),()=>_now);
        await game.ControlAsync("boss",0,default);
        if(failSave)
        {
            Directory.CreateDirectory(Path.Combine(_dir,"viewer-points.json.tmp"));
            await Assert.ThrowsAnyAsync<Exception>(()=>game.ExecuteBossAttackAsync("u","U","attack",default));
            Assert.Equal(10,(await store.GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);Assert.Equal(100,game.Status.BossHp);return;
        }
        var attack=await game.ExecuteBossAttackAsync("u","U","attack",default);Assert.True(attack.Success);Assert.Equal(20,attack.Damage);
        var p=(await store.GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(1,p.Health.CurrentHp);Assert.False(p.Health.Defeated);
        Assert.Contains(messages,s=>s.Contains("Lifeleech: +1 HP"));
        _now=_now.AddSeconds(2);Assert.False((await game.ExecuteBossAttackAsync("u","U","attack",default)).Success);
        Assert.Equal(1,(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
    }
}
