using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgLootExpansionTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipExpansion-"+Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _now=DateTimeOffset.UtcNow;
    private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
    private sealed class High:IDuelRandom{public int NextInclusive(int min,int max)=>max;}
    private sealed class Seed(int seed):IDuelRandom{private readonly Random _rng=new(seed);public int NextInclusive(int min,int max)=>(int)_rng.NextInt64(min,(long)max+1);}
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
    private static RpgItem Item(string id)=>RpgLootExpansionCatalog.Items().Single(i=>i.ItemId==id);
    private static void Wear(RpgProfile p,RpgItem item){p.Inventory.Add(item);p.Equipment[item.Slot!.Value]=item.InstanceId;}
    [Fact]
    public void DefaultsHaveFiftyTwoNewDefinitionsPreserveLegacyCrownAndRarityWeights()
    {
        var c=new RpgConfig();c.Validate();Assert.Equal(52,RpgLootExpansionCatalog.Items().Count);
        Assert.Equal(10,c.DiveLoot.Items.Count(i=>i.Collector is not null));Assert.Equal(20,c.Sets.Count);
        Assert.Equal("deepsea-crown",Assert.Single(c.DiveLoot.Items,i=>i.DisplayName=="Krone des Abgrunds").ItemId);
        Assert.Equal(new[]{6000,2900,850,200,50},c.DiveLoot.RarityWeights.OrderBy(p=>p.Key).Select(p=>p.Value));
        var count=c.DiveLoot.Items.Count;RpgLootExpansionCatalog.AddMissing(c.DiveLoot);Assert.Equal(count,c.DiveLoot.Items.Count);
        Assert.Equal(10000,c.DiveLoot.LegendaryMinimumStake);
    }
    [Fact]
    public void CollectorDuplicatesDoNotStackAndLastRemovalDisablesEffectWithoutCombatStats()
    {
        var c=new RpgConfig();var p=new RpgProfile();var first=Item("wealth-pearl");p.Inventory.Add(first);p.Inventory.Add(Item("wealth-pearl"));
        p.Inventory.Add(Item("pearl-compass"));p.Inventory.Add(Item("golden-shell"));
        var b=RpgCollectorService.Calculate(p,c.DiveLoot);Assert.Equal(5,b.BossPearls);Assert.Equal(2,b.FishingPearls);Assert.Equal(5,b.RareTreasureChance);
        Assert.All(typeof(RpgAttributes).GetProperties(),prop=>Assert.Equal(0m,(decimal)prop.GetValue(CharacterStatService.Calculate(p,c,RpgActivity.Boss))!));
        Assert.Equal(100,RpgHealthService.Maximum(p,c.Health));p.Inventory.Remove(first);Assert.Equal(5,RpgCollectorService.Calculate(p,c.DiveLoot).BossPearls);
        p.Inventory.RemoveAll(i=>i.ItemId=="wealth-pearl");Assert.Equal(0,RpgCollectorService.Calculate(p,c.DiveLoot).BossPearls);
    }
    [Fact]
    public void CollectorCapsDoNotCapEquipmentAndNegativeRewardsAreNeverBoosted()
    {
        var c=new RpgConfig();var p=new RpgProfile();
        foreach(var item in RpgLootExpansionCatalog.Items().Where(i=>i.Collector is not null))p.Inventory.Add(item);
        c.DiveLoot.CollectorPearlCap=3;c.DiveLoot.CollectorChanceCap=4;c.DiveLoot.CollectorExperienceCap=2;
        var b=RpgCollectorService.Calculate(p,c.DiveLoot);Assert.Equal(3,b.FishingPearls);Assert.Equal(3,b.BossPearls);Assert.Equal(4,b.RareTreasureChance);Assert.Equal(2,b.BossExperience);
        Wear(p,Item("leviathan-wrath"));Assert.Equal(30,CharacterStatService.Calculate(p,c,RpgActivity.Boss).BossDamage);
        Assert.Equal(105,RpgCollectorService.Reward(100,5));Assert.Equal(0,RpgCollectorService.Reward(0,5));Assert.Equal(-100,RpgCollectorService.Reward(-100,5));
    }
    [Theory]
    [InlineData(2,25,0)] [InlineData(4,25,10)]
    public void GuardianSetHpAndDefenseAreDerivedWithoutHealingOrInventoryStacking(int count,int hp,int reduction)
    {
        var c=new RpgConfig();var p=new RpgProfile();RpgHealthService.Refresh(p,c.Health,_now);p.Health.CurrentHp=70;
        foreach(var item in RpgLootExpansionCatalog.Items().Where(i=>i.SetId=="depth-warden").Take(count))
        {item.Attributes=new();Wear(p,item);}
        p.Inventory.Add(Item("warden-boots"));p.Inventory.Add(Item("warden-helm"));
        RpgHealthService.Refresh(p,c.Health,_now);var s=CharacterStatService.Calculate(p,c,RpgActivity.Boss);
        Assert.Equal(100+hp,RpgHealthService.Maximum(p,c.Health));Assert.Equal(hp,s.MaximumHp);Assert.Equal(reduction,s.BossDamageReduction);Assert.Equal(70,p.Health.CurrentHp);
        p.Equipment.Clear();RpgHealthService.Refresh(p,c.Health,_now);Assert.Equal(100,RpgHealthService.Maximum(p,c.Health));Assert.Equal(70,p.Health.CurrentHp);
    }
    [Fact]
    public void OceanWrathBonusesAndDoubleSourcesUseOneCentralCap()
    {
        var c=new RpgConfig();var p=new RpgProfile();
        foreach(var item in RpgLootExpansionCatalog.Items().Where(i=>i.SetId=="ocean-wrath"))Wear(p,item);
        Wear(p,Item("leviathan-wrath"));var s=CharacterStatService.Calculate(p,c,RpgActivity.Boss);
        Assert.Equal(16,s.CritChance);Assert.Equal(40,s.BossDamage);Assert.Equal(20,s.DoubleAttackChance);
        var hit=RpgPassiveService.BossAttack(100,p,c,new Low(),_now);Assert.Equal(1,hit.ExtraHits);
        p.Equipment.Remove(EquipmentSlot.Boots);Assert.Equal(30,CharacterStatService.Calculate(p,c,RpgActivity.Boss).BossDamage);
    }
    [Fact]
    public void DefenseCombinesOnceAndDodgeUsesSingleRollOnlyForBoss()
    {
        var c=new RpgConfig();var p=new RpgProfile();Wear(p,Item("abyss-armour"));Wear(p,Item("tidal-runner"));
        p.Buffs.Add(new(){Category=SushiCategory.Defensive,BossDamageReduction=15,RemainingUses=3});
        Assert.Equal(0,RpgBossDefenseService.Counter(100,p,c,new Low(),_now));
        Assert.Equal(73,RpgBossDefenseService.Counter(100,p,c,new High(),_now));
        p.Health.CurrentHp=100;RpgHealthService.ResolveEnemy(p,c.Health,20,false,0,_now);Assert.Equal(80,p.Health.CurrentHp);
    }
    [Theory]
    [InlineData(500,25)] [InlineData(1000,50)] [InlineData(5000,250)] [InlineData(10000,500)]
    public void GearCriticalHealingUsesActualDamageNoFlatCapAndHonorsDeathLock(int damage,int expected)
    {
        var c=new RpgConfig();c.Health.BaseHp=20000;var p=new RpgProfile{Health=new(){CurrentHp=1}};Wear(p,Item("neptune-heart"));
        var percent=CharacterStatService.Calculate(p,c,RpgActivity.Boss).CriticalHealing;
        Assert.Equal(expected,RpgSushiService.HealFromBossAttack(damage,damage,p,c.Health,"boss",_now,percent));
        Assert.Equal(5,RpgSushiService.HealFromBossAttack(10000,100,p,c.Health,"boss",_now,percent));
        p.Health.CurrentHp=0;Assert.Equal(0,RpgSushiService.HealFromBossAttack(damage,damage,p,c.Health,"boss",_now,percent));
        p.Health.CurrentHp=10;p.Health.Defeated=true;p.Health.BossId="boss";
        Assert.Equal(0,RpgSushiService.HealFromBossAttack(damage,damage,p,c.Health,"boss",_now,percent));
    }
    [Fact]
    public void LootPoolBonusIsRelativeAndRarityGateIsNeverBypassed()
    {
        var c=new RpgLootConfig{BaseDropChance=100,MaximumDropChance=100,CollectorPoolChance=20};
        c.Items=RpgLootExpansionCatalog.Items();c.RarityWeights=new(){{ItemRarity.Common,1}};
        var baseline=0;var boosted=0;var a=new RpgLootService(new Seed(42));var b=new RpgLootService(new Seed(42));
        // All these definitions are fixed and consume identical RNG calls; no dynamic generation noise.
        for(var i=0;i<10000;i++)
        {
            if(a.Roll(0,0,c,_now).Item?.Collector is not null)baseline++;
            if(b.Roll(0,0,c,_now,collectors:new(){TreasureChance=10}).Item?.Collector is not null)boosted++;
        }
        Assert.InRange(baseline,1800,2200);Assert.InRange(boosted-baseline,150,250);
        c.RarityWeights=new(){{ItemRarity.Legendary,1}};
        Assert.Null(a.Roll(9999,0,c,_now,collectors:new(){TreasureChance=100}).Item);
        Assert.Equal(ItemRarity.Legendary,a.Roll(10000,0,c,_now).Item!.Rarity);
        c.CollectorPoolChance=0;c.RarityWeights=new(){{ItemRarity.Common,1}};
        Assert.Null(a.Roll(0,0,c,_now).Item!.Collector);
    }
    [Fact]
    public void ItemWeightsSelectWithinRarityAndDoNotChangeRarityWeights()
    {
        var c=new RpgLootConfig{BaseDropChance=100,MaximumDropChance=100,Items=new(){Item("coral-dagger"),Item("shell-helm")},RarityWeights=new(){{ItemRarity.Common,1}}};
        c.Items[0].DropWeight=9;var service=new RpgLootService(new Seed(123));var first=0;
        for(var i=0;i<2000;i++)if(service.Roll(0,0,c,_now).Item!.ItemId=="coral-dagger")first++;
        Assert.InRange(first,1730,1870);Assert.Equal(1,c.RarityWeights[ItemRarity.Common]);
    }
    [Fact]
    public async Task InventorySlotsRingsCollectorsAndEquipmentPersistAndWrongSlotFails()
    {
        var c=new RpgConfig{Enabled=true};var store=new ViewerPointStore(_dir);var ring=Item("leviathan-oath");
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Inventory.AddRange(new[]{ring,Item("leviathan-oath"),Item("pearl-compass")});return new(true);},default);
        var service=new RpgProgressionService(store,c);
        Assert.False((await service.EquipAsync("u","U","wrong",ring.InstanceId,EquipmentSlot.Chest,default)).Success);
        Assert.True((await service.EquipAsync("u","U","ring",ring.InstanceId,EquipmentSlot.Ring2,default)).Success);
        var p=(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry.Rpg;
        Assert.Equal(3,p.Inventory.Count);Assert.Equal(ring.InstanceId,p.Equipment[EquipmentSlot.Ring2]);Assert.Equal(2,RpgCollectorService.Calculate(p,c.DiveLoot).FishingPearls);
        var chat=new RpgChatCommand(store,c);
        var text=await chat.ExecuteAsync(new(){UserId="u",UserName="U"},"!treasures",new[]{"!treasures"},default);
        Assert.Contains("Perlenkompass",text);Assert.Contains("Fischfang",text);Assert.True(RpgChatCommand.Recognizes("!treasures"));
    }
    [Fact]
    public async Task FishingCollectorBonusUsesActualCatchAndNeverAddsToAdministrativePayments()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;
        foreach(var f in c.Adventures.Rpg.FishSpecies)if(f.Id!="vampire_squid")f.Weight=0;
        c.Adventures.Fishing.LossChance=0;c.Adventures.Fishing.SmallChance=100;c.Adventures.Fishing.LargeChance=c.Adventures.Fishing.RareChance=0;
        var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Inventory.Add(Item("pearl-compass"));return new(true);},default);
        var result=await new RpgAdventureService(store,c,new Low(),()=>_now).ExecuteAsync("u","U","fish",RpgActivity.Fishing,0,default);
        Assert.True(result.Success);Assert.Equal(306,result.Payout);
        await store.AddPointsAsync("u","U",100,0,default);
        Assert.Equal(406,(await store.GetProfileAsync("u",default)).Entry.Points);
    }
    [Fact]
    public void SerializedLegacyItemsRemainUnmodifiedAndCollectorEffectsAreDistinctRolls()
    {
        var old=JsonSerializer.Deserialize<RpgItem>("{\"InstanceId\":\"legacy\",\"ItemId\":\"old\",\"Attributes\":{\"Damage\":2}}")!;
        Assert.Null(old.Collector);Assert.Equal(1,old.DropWeight);Assert.Equal(2,old.Attributes.Damage);
        var a=Item("pearl-compass");var b=Item("pearl-compass");b.Collector!.FishingPearls=3;
        Assert.NotEqual(RpgSalvageService.RollSignature(a),RpgSalvageService.RollSignature(b));
        var config=RpgItemGenerationService.EnableForFutureDrops(new());
        Assert.Null(config.DiveLoot.Items.Single(i=>i.ItemId=="neptune-heart").Generation);
    }
    [Fact]
    public async Task RealBossDoubleAttackHealsOnceBeforeOneReducedCounterAndDuplicateDoesNothing()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};var rpg=c.Adventures.Rpg;rpg.Enabled=true;rpg.Health.PassiveHealing=false;
        rpg.Health.BaseHp=1000;c.Adventures.Boss.HitPoints=1000;c.Adventures.Boss.AttackCooldownSeconds=1;
        var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>
        {
            Wear(a.Rpg,Item("neptune-heart"));Wear(a.Rpg,Item("leviathan-claws"));Wear(a.Rpg,Item("abyss-armour"));
            RpgHealthService.Refresh(a.Rpg,rpg.Health,_now);a.Rpg.Health.CurrentHp=50;return new(true);
        },default);
        var game=new AdventureGameService(store,c,(_,_)=>Task.CompletedTask,new Low(),()=>_now);
        await game.ControlAsync("boss",0,default);var result=await game.ExecuteBossAttackAsync("u","U","attack",default);
        Assert.True(result.Success);Assert.True(result.Critical);
        var p=(await store.GetProfileAsync("u",default)).Entry.Rpg;
        var counter=(int)decimal.Ceiling(RpgHealthService.CounterDamage(1000,rpg.Health,new Low())*0.88m);
        Assert.Equal(50+(int)decimal.Floor(result.Damage*0.05m)-counter,p.Health.CurrentHp);
        Assert.Equal(result.Damage,p.Health.BossDamage);
        var hp=p.Health.CurrentHp;
        Assert.False((await game.ExecuteBossAttackAsync("u","U","attack",default)).Success);
        Assert.Equal(hp,(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
    }
    [Fact]
    public async Task SurvivingBossVictoryAppliesPearlAndExperienceCollectorsOnce()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};var rpg=c.Adventures.Rpg;rpg.Enabled=true;rpg.BossLootChance=0;
        c.Adventures.Boss.HitPoints=10;c.Adventures.Boss.RewardPool=1000;
        var store=new ViewerPointStore(_dir);
        await store.ApplyRpgAsync("u","U","seed",a=>{a.Rpg.Inventory.AddRange(new[]{Item("wealth-pearl"),Item("wealth-pearl"),Item("tidal-hourglass")});return new(true);},default);
        var game=new AdventureGameService(store,c,(_,_)=>Task.CompletedTask,new Low(),()=>_now);
        await game.ControlAsync("boss",0,default);var result=await game.ExecuteBossAttackAsync("u","U","attack",default);
        Assert.True(result.Success);Assert.True(result.Defeated);Assert.Equal(10,result.Damage);
        var p=(await new ViewerPointStore(_dir).GetProfileAsync("u",default)).Entry;
        Assert.Equal(1050,p.Points);Assert.Equal(1260,p.Rpg.Experience);
    }
}
