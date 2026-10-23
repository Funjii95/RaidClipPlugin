using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgSushiTwoTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipSushi2-"+Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now=DateTimeOffset.UtcNow;
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    private RpgConfig Config()=>new(){Enabled=true};
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
    [Theory]
    [InlineData("sea_cucumber",40)] [InlineData("shrimp",100)] [InlineData("giant_shrimp",250)] [InlineData("pufferfish",350)] [InlineData("golden_pufferfish",800)]
    public async Task EveryNewSpeciesIsActuallyCatchableWithExactlyOneReward(string id,int reward)
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();
        foreach(var f in c.Adventures.Rpg.FishSpecies)if(f.Id!=id)f.Weight=0;
        c.Adventures.Fishing.LossChance=0;c.Adventures.Fishing.SmallChance=100;c.Adventures.Fishing.LargeChance=c.Adventures.Fishing.RareChance=0;
        var store=new ViewerPointStore(_dir);var result=await new RpgAdventureService(store,c,new Low(),()=>_now).ExecuteAsync("u","U","catch",RpgActivity.Fishing,0,default);
        Assert.True(result.Success);Assert.Equal(id,result.Item!.ItemId);Assert.Equal(reward,result.Payout);Assert.Equal(reward,result.Balance);
        var p=(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry;
        Assert.Equal(1,p.Rpg.Fish[id].Quantity);Assert.Equal(1,p.FishCollection[result.Item.DisplayName]);
        var again=await new RpgAdventureService(store,c,new Low(),()=>_now.AddHours(1)).ExecuteAsync("u","U","catch",RpgActivity.Fishing,0,default);
        Assert.False(again.Success);Assert.Equal(reward,await store.GetPointsAsync("u",default));
    }
    [Fact]
    public void WeightsAreRelativeRarePerkAppliesAndOldSpeciesRemain()
    {
        var c=Config();c.Validate();Assert.Equal(10233,RpgFishingService.Weights(c,new(),0).Sum(p=>p.Weight));
        Assert.Equal(5,c.FishSpecies.Count(f=>new[]{"sardine","perch","crystal-carp","eel","leviathan"}.Contains(f.Id)));
        var weights=RpgFishingService.Weights(c,new(){FishingLuck=100},0);
        Assert.Equal(24,weights.Single(p=>p.Fish.Id=="giant_shrimp").Weight);Assert.Equal(50,weights.Single(p=>p.Fish.Id=="sea_cucumber").Weight);
        c.FishSpecies.Single(f=>f.Id=="golden_pufferfish").MinimumStake=5000;
        Assert.DoesNotContain(RpgFishingService.Weights(c,new(),4999),p=>p.Fish.Id=="golden_pufferfish");
    }
    [Theory]
    [InlineData("kappa",300)] [InlineData("tempura",450)] [InlineData("kugelfisch",700)]
    public async Task CraftUsesEveryIngredientAndCurrencyAtomically(string recipe,int price)
    {
        var c=Config();var store=new ViewerPointStore(_dir);var r=c.Recipes.Single(r=>r.Id==recipe);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Points=2000;foreach(var i in RpgSushiService.Ingredients(r))a.Rpg.Fish[i.Key]=new(){FishId=i.Key,Quantity=i.Value};return new(true);},default);
        var service=new RpgProgressionService(store,c,()=>_now);
        var results=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>service.CraftAsync("u","U","same",recipe,default)));
        Assert.Single(results,r=>r.Success);var p=(await store.GetProfileAsync("u",default)).Entry;
        Assert.Equal(2000-price,p.Points);Assert.Equal(1,p.Rpg.Sushi[recipe]);Assert.All(p.Rpg.Fish.Values,f=>Assert.Equal(0,f.Quantity));
        Assert.False((await service.CraftAsync("u","U","missing",recipe,default)).Success);Assert.Equal(2000-price,await store.GetPointsAsync("u",default));
    }
    [Fact]
    public async Task TwoIndependentSlotsCannotBeOverwrittenAndSurviveRestart()
    {
        var c=Config();var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Sushi=new(){{"fighter",1},{"kappa",1},{"tempura",1}};return new(true);},default);
        var service=new RpgProgressionService(store,c,()=>_now);
        Assert.True((await service.ActivateSushiAsync("u","U","off","fighter",default)).Success);
        Assert.True((await service.ActivateSushiAsync("u","U","def","kappa",default)).Success);
        Assert.False((await service.ActivateSushiAsync("u","U","blocked","tempura",default)).Success);
        var p=(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry.Rpg;
        Assert.Equal(2,p.Buffs.Count);Assert.Equal(1,p.Sushi["tempura"]);Assert.All(p.Buffs,b=>Assert.Equal(3,b.RemainingUses));
    }
    [Fact]
    public void ThirdBossRetainsFullBuffUntilEndAndMultipleAttacksOnlyConsumeOnce()
    {
        var p=new RpgProfile();var c=Config();p.Buffs.Add(new(){EncounterBased=true,Activity=RpgActivity.Boss,Attributes=new(){Damage=10},RemainingUses=3,ActivatedAt=_now});
        for(int i=1;i<=3;i++)
        {
            var start=_now.AddMinutes(i);var end=start.AddMinutes(1);
            RpgSushiService.Participate(p,"boss"+i,end,start);RpgSushiService.Participate(p,"boss"+i,end,start.AddSeconds(1));
            Assert.Equal(3-i,p.Buffs[0].RemainingUses);Assert.Equal(10,CharacterStatService.Calculate(p,c,RpgActivity.Boss,now:start.AddSeconds(20)).Damage);
            RpgSushiService.Close(p,"boss"+i,end,end,c.Health);
        }
        Assert.Empty(p.Buffs);
    }
    [Fact]
    public async Task PassiveBonusIsNotRetroactiveAndExpirySplitsElapsedTime()
    {
        var c=Config();var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{RpgHealthService.Refresh(a.Rpg,c.Health,_now);a.Rpg.Health.CurrentHp=50;a.Rpg.Sushi["kappa"]=1;return new(true);},default);
        _now=_now.AddMinutes(5);var service=new RpgProgressionService(store,c,()=>_now);
        Assert.True((await service.ActivateSushiAsync("u","U","eat","kappa",default)).Success);
        var p=(await store.GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(60,p.Health.CurrentHp);
        RpgHealthService.Refresh(p,c.Health,_now.AddMinutes(5));Assert.Equal(85,p.Health.CurrentHp);
        p.Health.CurrentHp=40;p.Health.LastPassiveRegen=_now;
        p.Buffs[0].RemainingUses=0;p.Buffs[0].ExpiresAt=_now.AddMinutes(2);
        RpgHealthService.Refresh(p,c.Health,_now.AddMinutes(5));Assert.Equal(56,p.Health.CurrentHp); // 10 base +6 buff.
    }
    [Fact]
    public void FrequentRegenReadsPreserveFractionalSushiHealing()
    {
        var p=new RpgProfile();var c=new RpgHealthConfig{PassiveSeconds=1,PassiveHp=0};
        RpgHealthService.Refresh(p,c,_now);p.Health.CurrentHp=50;
        p.Buffs.Add(new(){Category=SushiCategory.Defensive,PassiveHealing=3,ActivatedAt=_now,RemainingUses=3});
        for(int i=1;i<=60;i++)RpgHealthService.Refresh(p,c,_now.AddSeconds(i));
        Assert.Equal(53,p.Health.CurrentHp);
    }
    [Fact]
    public async Task ChatBonusRespectsCommandsCooldownAndBossDeathLock()
    {
        var c=Config();var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{RpgHealthService.Refresh(a.Rpg,c.Health,_now);a.Rpg.Health.CurrentHp=20;a.Rpg.Health.Defeated=true;a.Rpg.Buffs.Add(new(){Category=SushiCategory.Defensive,ChatHealing=5,RemainingUses=3,ActivatedAt=_now});return new(true);},default);
        await RpgHealthService.HealChatAsync(store,c,new(){UserId="u",Id="command",Text="!unknown"},_now,default);
        await RpgHealthService.HealChatAsync(store,c,new(){UserId="u",Id="chat",Text="hello"},_now,default);
        await RpgHealthService.HealChatAsync(store,c,new(){UserId="u",Id="cool",Text="again"},_now.AddSeconds(15),default);
        var p=(await store.GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(35,p.Health.CurrentHp);Assert.True(p.Health.Defeated);
    }
    [Fact]
    public void DamageReductionUsesCeilingAndDoesNotAffectEnemyResolver()
    {
        var p=new RpgProfile();p.Buffs.Add(new(){Category=SushiCategory.Defensive,BossDamageReduction=15,RemainingUses=3});
        Assert.Equal(34,RpgSushiService.BossDamage(40,p,_now));Assert.Equal(9,RpgSushiService.BossDamage(10,p,_now));
        RpgHealthService.ResolveEnemy(p,new(),40,false,0,_now);Assert.Equal(60,p.Health.CurrentHp);
    }
    [Fact]
    public async Task MissingSecondTempuraIngredientAndMissingCurrencyNeverConsumeAnything()
    {
        var c=Config();var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Points=1000;a.Rpg.Fish["shrimp"]=new(){FishId="shrimp",Quantity=2};return new(true);},default);
        var service=new RpgProgressionService(store,c);
        Assert.False((await service.CraftAsync("u","U","missing","tempura",default)).Success);
        var p=(await store.GetProfileAsync("u",default)).Entry;Assert.Equal(1000,p.Points);Assert.Equal(2,p.Rpg.Fish["shrimp"].Quantity);Assert.Empty(p.Rpg.Sushi);
        await store.ApplyRpgAsync("u","U","prepare",a=>{a.Points=0;a.Rpg.Fish["giant_shrimp"]=new(){FishId="giant_shrimp",Quantity=1};return new(true);},default);
        Assert.False((await service.CraftAsync("u","U","poor","tempura",default)).Success);
        p=(await store.GetProfileAsync("u",default)).Entry;Assert.Equal(2,p.Rpg.Fish["shrimp"].Quantity);Assert.Equal(1,p.Rpg.Fish["giant_shrimp"].Quantity);
    }
    [Fact]
    public async Task ActualBossAttacksUseDefensiveReductionAndSpendOnlyOneChargePerBoss()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();c.Adventures.Boss.HitPoints=100;
        c.Adventures.Boss.AttackCooldownSeconds=1;c.Adventures.Boss.RoundCooldownSeconds=0;c.Adventures.Rpg.Health.PassiveHealing=false;
        var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Sushi["kugelfisch"]=1;return new(true);},default);
        Assert.True((await new RpgProgressionService(store,c.Adventures.Rpg,()=>_now).ActivateSushiAsync("u","U","eat","kugelfisch",default)).Success);
        var game=new AdventureGameService(store,c,(_,_)=>Task.CompletedTask,new Low(),()=>_now,"b");
        for(int round=1;round<=3;round++)
        {
            await game.ControlAsync("boss",0,default);
            Assert.True((await game.ExecuteBossAttackAsync("u","U","a"+round,default)).Success);
            _now=_now.AddSeconds(2);
            Assert.True((await game.ExecuteBossAttackAsync("u","U","b"+round,default)).Success);
            var p=(await store.GetProfileAsync("u",default)).Entry.Rpg;
            Assert.Equal(100-18*round,p.Health.CurrentHp);Assert.Equal(3,Assert.Single(p.Buffs).RemainingUses);
            await game.ControlAsync("cancelboss",0,default);_now=_now.AddMinutes(1);
        }
        _now=_now.AddHours(1);
        Assert.False(RpgSushiService.Active(Assert.Single((await store.GetProfileAsync("u",default)).Entry.Rpg.Buffs),_now));
    }
    [Fact]
    public async Task FishWithoutStakeRoutesThroughChatAndFailedStorageRollsBackNewReward()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg=Config();
        foreach(var fish in c.Adventures.Rpg.FishSpecies)if(fish.Id!="sea_cucumber")fish.Weight=0;
        c.Adventures.Fishing.LossChance=0;c.Adventures.Fishing.SmallChance=100;c.Adventures.Fishing.LargeChance=c.Adventures.Fishing.RareChance=0;
        var store=new ViewerPointStore(_dir);var messages=new List<string>();
        var game=new AdventureGameService(store,c,(s,_)=>{messages.Add(s);return Task.CompletedTask;},new Low(),()=>_now);
        await game.HandleAsync(new(){UserId="u",UserName="U",Id="free",Text="!fish"},"!fish",new[]{"!fish"},default);
        Assert.Equal(40,await store.GetPointsAsync("u",default));Assert.Contains(messages,s=>s.Contains("Seegurke"));
        Directory.CreateDirectory(Path.Combine(_dir,"viewer-points.json.tmp"));
        await Assert.ThrowsAnyAsync<Exception>(()=>new RpgAdventureService(store,c,new Low(),()=>_now.AddHours(1)).ExecuteAsync("u","U","failed",RpgActivity.Fishing,0,default));
        var p=(await store.GetProfileAsync("u",default)).Entry;Assert.Equal(40,p.Points);Assert.Equal(1,p.Rpg.Fish["sea_cucumber"].Quantity);
    }
    [Fact]
    public void MigrationPreservesMultipleLegacyBuffsAndStockWithoutResettingCharges()
    {
        var p=new RpgProfile{Buffs=new(){new(){RecipeId="fighter",RemainingUses=2},new(){RecipeId="critical",RemainingUses=1}},Sushi=new(){{"fighter",7}}};
        RpgSushiService.Upgrade(p);RpgSushiService.Upgrade(p);Assert.Equal(new[]{2,1},p.Buffs.Select(b=>b.RemainingUses));Assert.Equal(7,p.Sushi["fighter"]);Assert.All(p.Buffs,b=>{Assert.False(b.EncounterBased);Assert.Equal(TimeSpan.FromHours(1),b.ExpiresAt-b.ActivatedAt);});
        Assert.True(RpgChatCommand.Recognizes("!cook"));Assert.True(RpgChatCommand.Recognizes("!eat"));Assert.True(RpgChatCommand.Recognizes("!fishbag"));
    }
}
