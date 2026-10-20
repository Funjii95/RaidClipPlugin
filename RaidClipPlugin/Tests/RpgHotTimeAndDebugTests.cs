using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgHotTimeAndDebugTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipHotDebug-"+Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now=DateTimeOffset.Parse("2026-10-16T10:00:00Z");
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
    private RpgHotTimeService Hot()=>new(_dir,()=>_now);
    private static MinigameConfig Config(){var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;return c;}

    [Fact]
    public void ExactlyFiveMinutesNoStackingRestartAndAtMostOnceAnnouncements()
    {
        var c=new RpgHotTimeConfig();var hot=Hot();Assert.False(hot.StartAfterBoss("test",c,false));
        Assert.True(hot.StartAfterBoss("real",c,true));Assert.Equal(300,hot.Status.RemainingSeconds);
        Assert.Single(hot.PollMessages(c));Assert.Empty(hot.PollMessages(c));
        _now=_now.AddMinutes(2);Assert.False(hot.StartAfterBoss("another",c,true));
        hot=Hot();Assert.Equal(180,hot.Status.RemainingSeconds);Assert.Empty(hot.PollMessages(c));
        _now=_now.AddMinutes(3);Assert.False(hot.Status.Active);Assert.Single(hot.PollMessages(c));Assert.Empty(hot.PollMessages(c));
        Assert.False(hot.StartAfterBoss("real",c,true));
    }
    [Fact]
    public void ExpiredOfflineEventDoesNotReplayMessagesOrRestart()
    {
        var c=new RpgHotTimeConfig();var hot=Hot();hot.StartAfterBoss("real",c,true);hot.PollMessages(c);
        _now=_now.AddHours(1);hot=Hot();Assert.False(hot.Status.Active);Assert.Empty(hot.PollMessages(c));
    }
    [Fact]
    public void RelativeBaseBonusRespectsCapsAndLegendaryStakeGate()
    {
        var config=new RpgLootConfig();Assert.Equal(37.5m,RpgLootService.Chances(0,0,config,hotTimePercent:50).Total);
        Assert.Equal(60,RpgLootService.Chances(10000,1000,config,1000,1000,200).Total);
        config.RarityWeights=new(){[ItemRarity.Legendary]=1};
        Assert.Null(new RpgLootService(new Low()).Roll(9999,1000,config,_now,hotTimePercent:200).Item);
        Assert.Equal(ItemRarity.Epic,RpgLootService.Chances(9999,1000,config,hotTimePercent:200).MaximumRarity);
    }
    [Theory]
    [InlineData(RpgRewardSource.Activity,200)] [InlineData(RpgRewardSource.Fishing,200)] [InlineData(RpgRewardSource.Boss,200)]
    [InlineData(RpgRewardSource.Administrative,100)] [InlineData(RpgRewardSource.Salvage,100)] [InlineData(RpgRewardSource.Transfer,100)]
    [InlineData(RpgRewardSource.StakeRefund,100)] [InlineData(RpgRewardSource.PerkRefund,100)] [InlineData(RpgRewardSource.AlreadyBoosted,100)]
    public void RewardSourcesAndRepeatApplicationCannotDoubleBoost(RpgRewardSource source,long expected)
    {
        var c=new RpgHotTimeConfig();var hot=Hot();hot.StartAfterBoss("real",c,true);
        var reward=hot.Apply(new(100,source),c);Assert.Equal(expected,reward.Amount);Assert.Equal(reward,hot.Apply(reward,c));
    }
    [Fact]
    public async Task CasinoNetWinsBoostButStakeRefundAndAdministrativeCreditsDoNot()
    {
        var c=Config();var store=new ViewerPointStore(_dir,()=>_now);store.ConfigureHotTime(c.Adventures.Rpg);store.HotTime.StartAfterBoss("real",c.Adventures.Rpg.HotTime,true);
        await store.AddPointsAsync("42","TEST",1000,0,default);Assert.Equal(1000,await store.GetPointsAsync("42",default));
        var win=await store.ApplyCasinoAsync("42","TEST","Test",100,200,0,long.MaxValue,0,0,0,0,false,0,10,default);
        Assert.True(win.Success);Assert.Equal(300,win.ActualPayout);Assert.Equal(1200,win.Balance);
        var refund=await store.ApplyCasinoAsync("42","TEST","Test",100,100,0,long.MaxValue,0,0,0,0,false,0,10,default);
        Assert.Equal(1200,refund.Balance);
        await store.ApplyRpgAsync("42","TEST","salvage-seed",a=>{a.Rpg.Inventory.Add(new(){InstanceId="scrap",DisplayName="Testitem",SalvageValue=30});return new(true);},default);
        var salvage=new RpgSalvageService(store,c.Adventures.Rpg);var quote=await salvage.PreviewInstancesAsync("42",new[]{"scrap"},default);
        var salvaged=await salvage.ConfirmAsync("42","TEST","salvage-confirm",quote.Quote!.Token,default);
        Assert.True(salvaged.Success);Assert.Equal(30,salvaged.Award);Assert.Equal(1230,salvaged.Balance);
    }
    [Fact]
    public async Task RealBossVictoryStartsHotTimeButAdministrativeAttackDoesNot()
    {
        var c=Config();c.Adventures.Boss.HitPoints=10;c.Adventures.Rpg.BossLootChance=0;
        var store=new ViewerPointStore(_dir,()=>_now);var messages=new List<string>();
        var service=new AdventureGameService(store,c,(s,_)=>{messages.Add(s);return Task.CompletedTask;},new Low(),()=>_now,"b");
        await service.HandleAsync(new(){UserId="b",IsBroadcaster=true},"!start boss",new[]{"!start boss"},default);
        await service.ExecuteBossAttackAsync("42","TEST","admin",default);Assert.False(store.HotTime.Status.Active);
        _now=_now.AddMinutes(10);
        await service.HandleAsync(new(){UserId="b",IsBroadcaster=true},"!start boss",new[]{"!start boss"},default);
        await service.HandleAsync(new(){UserId="42",UserName="TEST",Id="chat"},"!attack",new[]{"!attack"},default);
        Assert.True(store.HotTime.Status.Active);Assert.Single(messages,s=>s.Contains("🔥 HOT TIME"));
    }
    [Fact]
    public void AtomicValidationBackupAndRestoreKeepLastValidData()
    {
        var path=Path.Combine(_dir,"test.json");RpgAtomicFile.Save(path,new RpgHotTimeConfig(),c=>c.Validate());
        RpgAtomicFile.Save(path,new RpgHotTimeConfig{DurationMinutes=10},c=>c.Validate());var bytes=File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(()=>RpgAtomicFile.Save(path,new RpgHotTimeConfig{DurationMinutes=-1},c=>c.Validate()));Assert.Equal(bytes,File.ReadAllBytes(path));
        File.WriteAllText(path,"broken json");Assert.Throws<JsonException>(()=>RpgAtomicFile.Save(path,new RpgHotTimeConfig(),c=>c.Validate()));
        RpgAtomicFile.Restore<RpgHotTimeConfig>(path,c=>c.Validate());Assert.Equal(5,RpgAtomicFile.Load<RpgHotTimeConfig>(path,c=>c.Validate()).DurationMinutes);
    }
    [Fact]
    public async Task DebugDisabledAndUnauthorizedAccountsCannotOperate()
    {
        var debug=new RpgDebugSession(_dir,()=>false);Assert.Throws<UnauthorizedAccessException>(()=>debug.Activate(Config()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>debug.RollLootAsync());
    }
    [Fact]
    public async Task ForcedLegendaryGenerationAndProgressAreIsolatedAndPersistInDebugOnly()
    {
        var source=Config();var before=JsonSerializer.Serialize(source);var production=new ViewerPointStore(Path.Combine(_dir,"production"));await production.AddPointsAsync("42","REAL",100,0,default);
        var debug=new RpgDebugSession(_dir,()=>true);debug.Activate(source);
        debug.Options.OnlyRarity=ItemRarity.Legendary;debug.Options.LootMode=RpgDebugLootMode.Garantiert;debug.Options.Stake=10000;
        await debug.RollLootAsync();Assert.Equal(ItemRarity.Legendary,Assert.Single((await debug.ProfileAsync()).Rpg.Inventory).Rarity);
        await debug.GenerateAsync("deepsea-crown",RpgDebugRollMode.Maximum);
        await debug.ProgressAsync("Perlen ändern",value:10000);await debug.ProgressAsync("Fische erzeugen","sardine",5);
        await debug.ProgressAsync("Sushi herstellen","fishing");await debug.ProgressAsync("Sushi aktivieren","fishing");
        await debug.ProgressAsync("Perk setzen","damage",2);await debug.ProgressAsync("Set testen","deepsea-guardian");
        Assert.Equal(4,RpgSetService.Progress((await debug.ProfileAsync()).Rpg,debug.Config.Adventures.Rpg).First().Equipped);
        Assert.Equal(100,await production.GetPointsAsync("42",default));Assert.Equal(before,JsonSerializer.Serialize(source));
        Assert.False(production.HotTime.Status.Active);debug.Deactivate();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>debug.ProfileAsync());
        debug.Activate(source);Assert.NotEmpty((await debug.ProfileAsync()).Rpg.Inventory);
    }
    [Fact]
    public async Task CustomGeneratorRejectsInvalidValuesAndSeededSimulationDoesNotAlterTestAccount()
    {
        var debug=new RpgDebugSession(_dir,()=>true);debug.Activate(Config());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>debug.GenerateAsync("deepsea-crown",RpgDebugRollMode.Benutzerdefiniert,new Dictionary<string,int>{{"CritChance",999}}));
        await debug.GenerateAsync("deepsea-crown",RpgDebugRollMode.Benutzerdefiniert,new Dictionary<string,int>{{"CritChance",4}});
        var before=JsonSerializer.Serialize(await debug.ProfileAsync());var first=await debug.SimulateAsync(100);var second=await debug.SimulateAsync(100);
        Assert.Equal(JsonSerializer.Serialize(first),JsonSerializer.Serialize(second));Assert.Equal(before,JsonSerializer.Serialize(await debug.ProfileAsync()));
        var bad=debug.Options.BaseChance;debug.Options.BaseChance=-1;Assert.Throws<InvalidOperationException>(()=>debug.SaveSettings());debug.Options.BaseChance=bad;
    }
    [Fact]
    public async Task ForcedCombatUsesRealEffectsAndCannotTriggerProductiveHotTime()
    {
        var debug=new RpgDebugSession(_dir,()=>true);debug.Activate(Config());debug.Options.ForceTriple=true;debug.Options.ForceCritical=true;
        var result=JsonSerializer.Serialize(await debug.CombatAsync());Assert.Contains("\"ExtraHits\":2",result);Assert.Contains("\"Critical\":true",result);
        var profile=await debug.ProfileAsync();Assert.DoesNotContain(profile.Rpg.Buffs,b=>b.RecipeId=="debug-force-only");
        Assert.DoesNotContain(profile.Rpg.Inventory,i=>i.InstanceId=="debug-force-only");
        result=JsonSerializer.Serialize(await debug.CombatAsync(true));Assert.Contains("\"HotTimeTriggered\":false",result);
    }
}
