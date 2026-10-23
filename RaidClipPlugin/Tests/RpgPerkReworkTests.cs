using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;
public sealed class RpgPerkReworkTests:IDisposable
{
    private readonly string dir=Path.Combine(Path.GetTempPath(),"PerkRework-"+Guid.NewGuid().ToString("N"));
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    private sealed class Fixed(int value):IDuelRandom {public int NextInclusive(int min,int max)=>Math.Clamp(value,min,max);}
    private static MinigameConfig Config(){var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;c.Adventures.Rpg.Upgrades.EquipmentDropChances.Keys.ToList().ForEach(k=>c.Adventures.Rpg.Upgrades.EquipmentDropChances[k]=0);c.Adventures.Rpg.Enemies.Enabled=false;return c;}
    public void Dispose(){if(Directory.Exists(dir))Directory.Delete(dir,true);}
    [Fact] public async Task SixthPurchaseIsRejectedAfterRestartWithoutSpendingOrChangingTalents()
    {
        var config=Config();var store=new ViewerPointStore(dir);
        await store.AddPointsAsync("u","U",100000,0,default);
        var service=new RpgProgressionService(store,config.Adventures.Rpg);
        for(var i=1;i<=5;i++)Assert.True((await service.BuyPerkAsync("u","U","buy"+i,"abyss-sense",default)).Success);
        store=new(dir);var before=(await store.GetProfileAsync("u",default)).Entry;
        Assert.False((await new RpgProgressionService(store,config.Adventures.Rpg).BuyPerkAsync("u","U","six","abyss-sense",default)).Success);
        var after=(await store.GetProfileAsync("u",default)).Entry;
        Assert.Equal(before.Points,after.Points);Assert.Equal(5,after.Rpg.Perks["abyss-sense"]);Assert.Empty(after.Rpg.TalentRanks);
        Assert.All(config.Adventures.Rpg.Perks,p=>Assert.Equal(5,p.MaximumLevel));
        Assert.Equal(5,new RpgPerkDefinition{MaximumLevel=100}.MaximumLevel);
    }
    [Theory] [InlineData(0,true)] [InlineData(99,true)] [InlineData(100,false)] [InlineData(9999,false)]
    public void MimicDiscoveryHasOnePercentAtRankFiveAndHalfPercentBaseline(int roll,bool expected)
    {
        var p=new RpgProfile();p.Perks["abyss-sense"]=5;
        Assert.Equal(.5m,RpgPerkCatalog.Bonus(p,"bossDiscovery"));
        var c=Config().Adventures.Rpg;Assert.Equal(1,RpgPerkCatalog.MimicChance(p,c));
        Assert.Equal(expected,RpgPerkCatalog.DiscoverMimic(p,c,new Fixed(roll)));
        p.Perks.Clear();Assert.Equal(.5m,RpgPerkCatalog.MimicChance(p,c));Assert.True(RpgPerkCatalog.DiscoverMimic(p,c,new Fixed(49)));Assert.False(RpgPerkCatalog.DiscoverMimic(p,c,new Fixed(50)));
        c.Mimic.Enabled=false;Assert.False(RpgPerkCatalog.DiscoverMimic(p,c,new Low()));
    }
    [Theory] [InlineData(false,499,true)] [InlineData(false,500,false)] [InlineData(true,4999,true)] [InlineData(true,5000,false)]
    public void LegendaryRollUsesExactBossChanceAndOnlyLegendaryBossItems(bool mimic,int roll,bool expected)
    {
        var config=Config().Adventures.Rpg;var result=RpgBossLootService.RollLegendary(config,new Fixed(roll),DateTimeOffset.UtcNow,mimic);
        Assert.Equal(expected,result is not null);
        if(result is not null){Assert.Equal(ItemRarity.Legendary,result.Rarity);Assert.Contains(RpgActivity.Boss,result.DropActivities);Assert.NotEmpty(result.InstanceId);}
    }
    [Fact] public async Task DiscoveryOnlyComesFromCommittedDivesAndCannotBeReplayed()
    {
        var config=Config();var store=new ViewerPointStore(dir);var now=DateTimeOffset.UtcNow;
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Points=1000;a.Rpg.Perks["abyss-sense"]=5;return new(true);},default);
        var game=new RpgAdventureService(store,config,new Low(),()=>now);
        Assert.False((await game.ExecuteAsync("u","U","bad",RpgActivity.Dive,-1,default)).BossDiscovered);
        Assert.False((await game.ExecuteAsync("u","U","fish",RpgActivity.Fishing,0,default)).BossDiscovered);
        var dive=await game.ExecuteAsync("u","U","dive",RpgActivity.Dive,100,default);Assert.True(dive.Success);Assert.True(dive.BossDiscovered);
        now=now.AddHours(1);var replay=await game.ExecuteAsync("u","U","dive",RpgActivity.Dive,100,default);Assert.False(replay.Success);Assert.False(replay.BossDiscovered);
    }
    [Fact] public async Task SharedMimicSurvivesRestartCannotReplaceBossAndPaysOnce()
    {
        var config=Config();config.Adventures.Boss.HitPoints=10;config.Adventures.Rpg.Mimic.HitPoints=10;config.Adventures.Rpg.Mimic.RewardPool=100;config.Adventures.Boss.RewardPool=100;config.Adventures.Rpg.BossLootChance=0;
        var store=new ViewerPointStore(dir);var messages=new List<string>();var now=DateTimeOffset.UtcNow;
        AdventureGameService Make(ViewerPointStore s)=>new(s,config,(text,_)=>{messages.Add(text);return Task.CompletedTask;},new Low(),()=>now);
        var game=Make(store);Assert.True(await game.StartDiscoveredBossAsync("Finder",default));
        var id=game.Status.BossEventId;Assert.True(game.Status.BossIsMimic);Assert.Contains("Mimic",game.Status.BossName);
        Assert.False(await game.StartDiscoveredBossAsync("Other",default));Assert.Equal(id,game.Status.BossEventId);
        game=Make(new ViewerPointStore(dir));await game.TickAsync(default);Assert.True(game.Status.BossIsMimic);Assert.Equal(id,game.Status.BossEventId);
        Assert.True((await game.ExecuteBossAttackAsync("u","U","hit",default)).Success);
        var won=(await store.GetProfileAsync("u",default)).Entry;Assert.Single(won.Rpg.Inventory,i=>i.Rarity==ItemRarity.Legendary);Assert.Equal(100,won.Points);
        Assert.False((await game.ExecuteBossAttackAsync("u","U","hit",default)).Success);
        Assert.False(await game.StartDiscoveredBossAsync("Other",default));
        var saved=(await new ViewerPointStore(dir).GetProfileAsync("u",default)).Entry;Assert.Equal(won.Points,saved.Points);Assert.Single(saved.Rpg.Inventory);
    }
    [Fact] public void PersonalPerksApplyWithTalentBonusesAndStayBounded()
    {
        var config=Config().Adventures.Rpg;var p=new RpgProfile{PlayerLevel=100};
        p.Perks=new(){{"iron-skin",5},{"battle-rush",5},{"life-steal",5},{"quick-reel",5},{"safe-ascent",5},{"deep-diver",5},{"protective-bubble",5}};
        Assert.Equal(357,RpgHealthService.Maximum(p,config.Health));
        Assert.Equal(85,RpgPerkCatalog.Cooldown(p,100,"attackCooldown"));Assert.Equal(85,RpgPerkCatalog.Cooldown(p,100,"fishCooldown"));
        Assert.Equal(5,RpgPerkCatalog.Combined(p,"lifeleech"));Assert.Equal(20,RpgPerkCatalog.Combined(p,"underwaterDodge"));
        Assert.Equal(15,CharacterStatService.Calculate(p,config,RpgActivity.Dive).ExtraLootChance);
        Assert.Equal(10,CharacterStatService.Calculate(p,config,RpgActivity.Boss).BossDamageReduction);
        p.Perks["life-steal"]=100;Assert.Equal(5,RpgPerkCatalog.Combined(p,"lifeleech"));
    }
    [Fact] public void SettingsControlDiscoveryAndLegendaryChancesAndRejectInvalidValues()
    {
        var c=Config().Adventures.Rpg;var p=new RpgProfile();c.Mimic.BaseSpawnChance=2;
        Assert.Equal(2,RpgPerkCatalog.MimicChance(p,c));c.Mimic.LegendaryChance=0;c.Mimic.NormalBossLegendaryChance=100;
        Assert.Null(RpgBossLootService.RollLegendary(c,new Low(),DateTimeOffset.UtcNow,true));
        Assert.NotNull(RpgBossLootService.RollLegendary(c,new Fixed(9999),DateTimeOffset.UtcNow));
        c.Mimic.BaseSpawnChance=-1;Assert.Throws<InvalidOperationException>(()=>c.Validate());
    }
    [Fact] public void OtterRageExpiresAfterSixtySecondsAndDoesNotRefreshOnRead()
    {
        var c=Config().Adventures.Rpg;var p=new RpgProfile();p.Perks["otter-rage"]=5;var now=DateTimeOffset.UtcNow;
        Assert.Equal(0,CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now).Damage);
        RpgPerkCatalog.Victory(p,now);Assert.Equal(15,CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now.AddSeconds(59)).Damage);
        Assert.Equal(0,CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:now.AddSeconds(60)).Damage);
    }
    [Fact] public async Task SushiMasterScalesTheStoredBuffAndFishingPerksChangeRealWeights()
    {
        var config=Config().Adventures.Rpg;var store=new ViewerPointStore(dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Sushi["fighter"]=1;a.Rpg.Perks["sushi-master"]=5;return new(true);},default);
        Assert.True((await new RpgProgressionService(store,config).ActivateSushiAsync("u","U","eat","fighter",default)).Success);
        var buff=Assert.Single((await store.GetProfileAsync("u",default)).Entry.Rpg.Buffs);
        Assert.Equal(12.5m,buff.Attributes.Damage);
        var p=new RpgProfile{Perks=new(){{"bait-expert",5},{"deep-hook",5},{"legendary-catch",5}}};
        var weights=RpgFishingService.Weights(config,new(),0,p);
        Assert.Equal(1000,weights.Single(w=>w.Fish.Id=="crystal-carp").Weight);
        Assert.Equal(15,weights.Single(w=>w.Fish.Id=="leviathan").Weight);
    }
}
