using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgAdventureIntegrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipRpgAdventure-" + Guid.NewGuid().ToString("N"));
    private static readonly CancellationToken Token = CancellationToken.None;
    private sealed class Lowest : IDuelRandom { public int NextInclusive(int min, int max) => min; }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private static MinigameConfig Configuration()
    {
        var config = new MinigameConfig { Enabled = true, PointsEnabled = true };
        config.Adventures.Rpg.Enabled = true;config.Adventures.Rpg.Upgrades.EquipmentDropChances.Keys.ToList().ForEach(k=>config.Adventures.Rpg.Upgrades.EquipmentDropChances[k]=0);
        config.Adventures.Treasure.MaximumBet = 10000;
        var area=config.Adventures.Rpg.Zones.Get("sunny_lagoon");area.MimicChance=area.EnemyChance=0;area.ChestChance=100;
        return config;
    }

    [Fact]
    public async Task ExactlyThreeValidDivesConsumeBuffAndCooldownSurvivesRestart()
    {
        var config = Configuration();
        var now = DateTimeOffset.UtcNow;
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", account =>
        {
            account.Points = 1000;
            account.Rpg.Buffs.Add(new() { RecipeId = "dive", Activity = RpgActivity.Dive, RemainingUses = 3 });
            return new(true);
        }, Token);
        var service = new RpgAdventureService(store, config, new Lowest(), () => now);
        Assert.False((await service.ExecuteAsync("42", "Otter", "bad", RpgActivity.Dive, -1, Token)).Success);
        // Excursions have no affordability gate; legacy stake is ignored by the engine.
        Assert.True((await service.ExecuteAsync("42", "Otter", "first", RpgActivity.Dive, 100, Token)).Success);
        store = new ViewerPointStore(_directory);
        service = new(store, config, new Lowest(), () => now);
        var cooling = await service.ExecuteAsync("42", "Otter", "cooldown", RpgActivity.Dive, 100, Token);
        Assert.False(cooling.Success);
        Assert.True(cooling.RemainingCooldown > 0);
        Assert.Equal(2, Assert.Single((await store.GetProfileAsync("42", Token)).Entry.Rpg.Buffs).RemainingUses);
        now = now.AddSeconds(31);
        Assert.True((await service.ExecuteAsync("42", "Otter", "second", RpgActivity.Dive, 100, Token)).Success);
        now = now.AddSeconds(31);
        Assert.True((await service.ExecuteAsync("42", "Otter", "third", RpgActivity.Dive, 100, Token)).Success);
        Assert.Empty((await store.GetProfileAsync("42", Token)).Entry.Rpg.Buffs);
        Assert.Equal(1030, await store.GetPointsAsync("42", Token));
    }

    [Fact]
    public async Task ParallelDivesCannotBypassCooldownAndLegendaryCannotBeRetried()
    {
        var config=Configuration();var store=new ViewerPointStore(_directory);await store.AddPointsAsync("42","Otter",20000,0,Token);
        var service=new RpgAdventureService(store,config,new Lowest());var results=await Task.WhenAll(Enumerable.Range(0,20).Select(i=>service.ExecuteAsync("42","Otter","dive"+i,RpgActivity.Dive,10000,Token)));
        var success=Assert.Single(results,r=>r.Success);Assert.Equal("chest",success.EventType);Assert.Null(success.Item);Assert.All(success.AdditionalItems,i=>Assert.Null(i.Slot));
        Assert.Equal(20010,await store.GetPointsAsync("42",Token));Assert.DoesNotContain((await store.GetProfileAsync("42",Token)).Entry.Rpg.Inventory,i=>i.Slot.HasValue);
    }

    [Fact]
    public async Task FishingPayoutStillUsesExistingConfiguredMultiplier()
    {
        var config = Configuration();
        config.Adventures.Fishing.LossChance = 0;
        config.Adventures.Fishing.SmallChance = 100;
        config.Adventures.Fishing.LargeChance = config.Adventures.Fishing.RareChance = 0;
        config.Adventures.Fishing.SmallMultiplier = 2;
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        var result = await new RpgAdventureService(store, config, new Lowest()).ExecuteAsync("42", "Otter", "fish1", RpgActivity.Fishing, 100, Token);
        Assert.True(result.Success);
        Assert.Equal(40, result.Payout);
        var profile = (await store.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(1040, profile.Points);
        Assert.Equal(1, profile.FishCollection[config.Adventures.Rpg.FishSpecies.Single(f=>f.Id=="sardine").Name]);
        Assert.Equal(1, profile.Rpg.Fish["sardine"].Quantity);
    }

    [Fact]
    public async Task FailedSoloSaveDoesNotConsumeCurrencyBuffOrLoot()
    {
        var config = Configuration();
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", account =>
        { account.Points = 1000; account.Rpg.Buffs.Add(new() { Activity = RpgActivity.Dive }); return new(true); }, Token);
        Directory.CreateDirectory(Path.Combine(_directory, "viewer-points.json.tmp"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new RpgAdventureService(store, config, new Lowest()).ExecuteAsync("42", "Otter", "dive", RpgActivity.Dive, 100, Token));
        var account = (await store.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(1000, account.Points);
        Assert.Equal(3, Assert.Single(account.Rpg.Buffs).RemainingUses);
        Assert.Empty(account.Rpg.Cooldowns);
        Assert.Empty(account.Rpg.Inventory);
    }

    [Fact]
    public async Task BossUsesRingCritAndConsumesBuffOnlyOnAcceptedAttacks()
    {
        var config = Configuration();
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", account =>
        {
            account.Rpg.Inventory.Add(new() { InstanceId = "ring", Slot = EquipmentSlot.Ring1, Attributes = new() { CritChance = 5, CritDamage = 10 } });
            account.Rpg.Equipment[EquipmentSlot.Ring1] = "ring";
            account.Rpg.Buffs.Add(new() { Activity = RpgActivity.Boss, Attributes = new() { Damage = 10 } });
            return new(true);
        }, Token);
        config.Adventures.Boss.HitPoints = 200;
        var service = new AdventureGameService(store, config, (_, _) => Task.CompletedTask, new Lowest(), broadcasterId: "b");
        await service.HandleAsync(new() { UserId = "b", IsBroadcaster = true }, "!start boss", new[] { "!start boss" }, Token);
        var attack = new ChatMessage { Id = "attack1", UserId = "42", UserName = "Otter" };
        var accepted = await service.ExecuteBossAttackAsync("42", "Otter", attack.Id, Token);
        Assert.True(accepted.Success);
        Assert.True(accepted.Critical);
        Assert.Equal(17, accepted.Damage);
        Assert.Equal(183, service.Status.BossHp);
        await service.HandleAsync(attack, "!attack", new[] { "!attack" }, Token);
        Assert.Equal(183, service.Status.BossHp);
        Assert.Equal(2, Assert.Single((await store.GetProfileAsync("42", Token)).Entry.Rpg.Buffs).RemainingUses);
        Assert.Equal(15, config.Adventures.Boss.AutoStartMinutes);
    }

    [Fact]
    public async Task LegacyFishImportRunsOnceAndDoesNotRemoveOldCollection()
    {
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        await store.ApplyCasinoAsync("42", "Otter", "Fischen", 0, 0, 0, long.MaxValue, 0, 0, 0, 0, false, 0, 10, Token, "Hecht", 1000);
        await store.ApplyRpgAsync("42", "Otter", "migrate", _ => new(true), Token);
        await new ViewerPointStore(_directory).ApplyRpgAsync("42", "Otter", "migrate-again", _ => new(true), Token);
        var account = (await new ViewerPointStore(_directory).GetProfileAsync("42", Token)).Entry;
        Assert.Equal(1, account.FishCollection["Hecht"]);
        Assert.Equal(1, account.Rpg.Fish["perch"].Quantity);
    }

    [Fact]
    public async Task LegendaryAnnouncementIsNotRepeatedWhenSameMessageIsRetried()
    {
        var config=Configuration();var c=config.Adventures.Rpg;var area=c.Zones.Get("tidal_maelstrom");area.MimicChance=area.ChestChance=area.EliteChance=0;area.EnemyChance=100;area.Hp=1;area.Armor=0;c.BossLootChance=100;
        c.DiveLoot.BaseDropChance=c.DiveLoot.MaximumDropChance=100;c.DiveLoot.RarityWeights=new(){[ItemRarity.Legendary]=1};
        var store=new ViewerPointStore(_directory);await store.ApplyRpgAsync("42","Otter","seed",a=>{a.Points=20000;a.Rpg.SelectedDivingZone=area.Id;return new(true);},Token);
        var messages=new List<string>();AdventureGameService Make(ViewerPointStore p)=>new(p,config,(text,_)=>{messages.Add(text);return Task.CompletedTask;},new Lowest());
        var message=new ChatMessage{Id="same-dive",UserId="42",UserName="Otter"};await Make(store).HandleAsync(message,"!dive",new[]{"!dive"},Token);await Make(new(_directory)).HandleAsync(message,"!dive",new[]{"!dive"},Token);
        Assert.Single(messages,m=>m.Contains("LEGENDÄR!"));
    }

    [Fact]
    public async Task ChatAndServiceShareTheSameAccountAndRegistry()
    {
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        var config = Configuration();
        var user = new ChatMessage { Id = "chat-perk", UserId = "42", UserName = "Otter" };
        var result = await new RpgChatCommand(store, config.Adventures.Rpg).ExecuteAsync(user, "!perk", new[] { "!perk", "crit" }, Token);
        Assert.Contains("500", result);
        Assert.Equal(1, (await store.GetProfileAsync("42", Token)).Entry.Rpg.Perks["crit"]);
        var app = new AppConfig { Minigame = config };
        var registry = new CommandRegistry(); registry.Update(app);
        Assert.True(Assert.Single(registry.Commands.Where(c => c.CommandText == "!inventory")).Enabled);
    }
}
