using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgEnemyTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipEnemy-"+Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now=DateTimeOffset.UtcNow;
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    private sealed class Boundary(int encounter):IDuelRandom {private bool first=true;public int NextInclusive(int min,int max){if(first){first=false;return encounter;}return min;}}
    private sealed class Sequence(params int[] values):IDuelRandom
    {
        private readonly Queue<int> queue=new(values);
        public int NextInclusive(int min,int max){var value=queue.Dequeue();Assert.InRange(value,min,max);return value;}
    }
    private static RpgConfig Config()
    {
        var c=new RpgConfig{Enabled=true};c.Upgrades.EquipmentDropChances.Keys.ToList().ForEach(k=>c.Upgrades.EquipmentDropChances[k]=0);foreach(var z in c.Zones.Areas){z.Hp=1;z.Attack=3;z.Armor=0;}c.BossLootChance=100;
        c.DiveLoot.BaseDropChance=c.DiveLoot.MaximumDropChance=100;return c;
    }
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
    [Theory]
    [InlineData(10,1000,true)] [InlineData(10,1001,false)] [InlineData(15,1500,true)] [InlineData(15,1501,false)]
    [InlineData(20,2000,true)] [InlineData(20,2001,false)]
    [InlineData(0,1,false)] [InlineData(0,10000,false)]
    [InlineData(50,5000,true)] [InlineData(50,5001,false)]
    [InlineData(100,1,true)] [InlineData(100,10000,true)]
    public void ConfiguredChanceHasExactBoundaries(int chance,int roll,bool expected)
    {
        var c=Config();c.Enemies.EncounterChance=chance;
        Assert.Equal(expected,new RpgEnemyService(new Boundary(roll)).Encounter(new(),c,0,_now) is not null);
    }
    [Theory]
    [InlineData(0,"Schnappkrabbe",30)] [InlineData(250,"Korallenräuber",60)] [InlineData(1000,"Tiefsee-Muräne",100)]
    [InlineData(5000,"Abgrundwächter",180)] [InlineData(10000,"Leviathan-Jungtier",300)]
    public void StakeSelectsEnemyAndExperience(long stake,string name,int xp)
    {
        // Legacy stake values cannot change the actual selected area's fixed enemy level.
        var c=Config();var p=new RpgProfile();var r=new RpgEnemyService(new Low()).Encounter(p,c,stake,_now)!;
        Assert.NotEmpty(name);Assert.True(xp>0);Assert.Equal("Kleine Riffkrabbe",r.Name);Assert.Equal(10,r.Level);Assert.True(r.Victory);Assert.Equal(100,p.Experience);Assert.NotNull(r.Loot);Assert.Equal(0,r.DamageTaken);
    }
    [Fact]
    public void BetterOfTwoRollsImprovesRarityWithoutGivingTwoItems()
    {
        var c=Config().DiveLoot;c.Items=new(){new(){ItemId="c",Rarity=ItemRarity.Common},new(){ItemId="r",Rarity=ItemRarity.Rare}};
        c.RarityWeights=new(){[ItemRarity.Common]=1,[ItemRarity.Rare]=1};
        var r=new RpgLootService(new Sequence(1,1,0,1,2,0)).RollEnemy(250,0,c,_now);
        Assert.Equal(ItemRarity.Rare,r.Item!.Rarity);
    }
    [Fact]
    public void EnemyCannotBypassLegendaryStakeGate()
    {
        var c=Config();c.DiveLoot.RarityWeights=new(){[ItemRarity.Legendary]=1};var p=new RpgProfile();
        Assert.Null(new RpgEnemyService(new Low()).Encounter(p,c,10000,_now)!.Loot);
        p.SelectedDivingZone="tidal_maelstrom";
        Assert.Equal(ItemRarity.Legendary,new RpgEnemyService(new Low()).Encounter(p,c,0,_now)!.Loot!.Rarity);
    }
    [Fact]
    public void DefeatCostsHpButNoExperienceAndPreservesBossLockAndItems()
    {
        var c=Config();c.Zones.Get("sunny_lagoon").Hp=1000000;c.Zones.Get("sunny_lagoon").Attack=200;
        var p=new RpgProfile{Experience=123};p.Inventory.Add(new(){ItemId="old"});p.Health.BossId="current-boss";
        var r=new RpgEnemyService(new Low()).Encounter(p,c,0,_now)!;
        Assert.False(r.Victory);Assert.Null(r.Loot);Assert.Equal(0,p.Health.CurrentHp);Assert.True(p.Health.Defeated);
        Assert.Equal(123,p.Experience);Assert.Single(p.Inventory);
        Assert.Throws<InvalidOperationException>(()=>new RpgEnemyService(new Low()).Encounter(p,c,0,_now));
    }
    [Fact]
    public async Task EncounterIsAtomicDuplicateSafeAndDoesNotChangeDivePayout()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();var z=c.Adventures.Rpg.Zones.Get("sunny_lagoon");z.MimicChance=z.ChestChance=z.EliteChance=0;z.EnemyChance=100;
        var store=new ViewerPointStore(_dir);await store.AddPointsAsync("u","U",1000,0,default);
        var service=new RpgAdventureService(store,c,new Low(),()=>_now);var r=await service.ExecuteAsync("u","U","one",RpgActivity.Dive,250,default);
        Assert.True(r.Success);Assert.True(r.Enemy!.Victory);Assert.Equal(1020,r.Balance);Assert.Equal(20,r.Payout);
        var p=(await store.GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(100,p.Experience);Assert.Single(p.Inventory);
        r=await new RpgAdventureService(store,c,new Low(),()=>_now.AddHours(1)).ExecuteAsync("u","U","one",RpgActivity.Dive,250,default);
        Assert.False(r.Success);Assert.Null(r.Enemy);Assert.Equal(1020,await store.GetPointsAsync("u",default));
        p=(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(100,p.Experience);Assert.Single(p.Inventory);
    }
    [Fact]
    public async Task FailedSaveRollsBackCombatExperienceLootAndCurrency()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();
        var store=new ViewerPointStore(_dir);await store.AddPointsAsync("u","U",1000,0,default);
        Directory.CreateDirectory(Path.Combine(_dir,"viewer-points.json.tmp"));
        await Assert.ThrowsAnyAsync<Exception>(()=>new RpgAdventureService(store,c,new Low(),()=>_now).ExecuteAsync("u","U","bad",RpgActivity.Dive,250,default));
        var p=(await store.GetProfileAsync("u",default)).Entry;Assert.Equal(1000,p.Points);Assert.Equal(0,p.Rpg.Experience);Assert.Empty(p.Rpg.Inventory);Assert.DoesNotContain("bad",p.Rpg.CompletedActions);
    }
    [Fact]
    public void DisabledFeatureOrHpSkipsEnemiesAndBossSushiIsUntouched()
    {
        var c=Config();var p=new RpgProfile();p.Buffs.Add(new(){RecipeId="boss",Activity=RpgActivity.Boss,Attributes=new(){Damage=100},RemainingUses=3});
        var r=new RpgEnemyService(new Low()).Encounter(p,c,0,_now);Assert.NotNull(r);Assert.Equal(3,p.Buffs[0].RemainingUses);
        c.Enemies.Enabled=false;Assert.Null(new RpgEnemyService(new Low()).Encounter(p,c,0,_now));
        c.Enemies.Enabled=true;c.Health.Enabled=false;Assert.Null(new RpgEnemyService(new Low()).Encounter(p,c,0,_now));
    }
}
