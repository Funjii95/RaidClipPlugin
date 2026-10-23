using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgItemSystemTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipItems2-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-16T12:00:00Z");
    private sealed class Low : IDuelRandom { public int NextInclusive(int min, int max) => min; }
    private sealed class High : IDuelRandom { public int NextInclusive(int min, int max) => max; }
    private sealed class Fixed(int value) : IDuelRandom { public int NextInclusive(int min, int max) => Math.Clamp(value, min, max); }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private static RpgProfile Equipped(RpgAttributes? attributes = null, params RpgItemEffect[] effects)
    {
        var profile = new RpgProfile();
        profile.Inventory.Add(new() { InstanceId = "ring", DisplayName = "Test-Ring", Slot = EquipmentSlot.Ring1,
            Attributes = attributes ?? new(), Effects = effects.ToList() });
        profile.Equipment[EquipmentSlot.Ring1] = "ring"; return profile;
    }
    private static RpgItemEffect Effect(RpgEffectTrigger trigger, decimal chance = 10) => new()
    { Id = trigger.ToString(), Name = "Otterkraft", Trigger = trigger, Chance = chance };

    [Theory]
    [InlineData(ItemRarity.Common)] [InlineData(ItemRarity.Uncommon)] [InlineData(ItemRarity.Rare)]
    [InlineData(ItemRarity.Epic)] [InlineData(ItemRarity.Legendary)]
    public void RollsStayWithinConfiguredRangesAndRarityCounts(ItemRarity rarity)
    {
        var config = new RpgLootConfig();
        foreach (var rng in new IDuelRandom[] { new Low(), new High() })
        {
            var item = new RpgItem { ItemId = "dynamic", Rarity = rarity, Slot = EquipmentSlot.Ring1,
                Generation = RpgItemGenerationService.DefaultRules(EquipmentSlot.Ring1, rarity) };
            var ranges = item.Generation.Attributes.ToDictionary(a => a.Attribute);
            RpgItemGenerationService.Generate(item, config, rng);
            Assert.True(item.Attributes.Power>0);Assert.True(item.Attributes.Stamina>0);
            var values = typeof(RpgAttributes).GetProperties().Where(p=>p.Name is not (nameof(RpgAttributes.Power) or nameof(RpgAttributes.Stamina)))
                .Select(p => (p.Name, Value: (decimal)p.GetValue(item.Attributes)!)).Where(p => p.Value != 0).ToArray();
            var counts = RpgItemGenerationService.Counts(rarity);
            Assert.InRange(values.Length, counts.Min, counts.Max);
            foreach (var value in values) Assert.InRange(value.Value, ranges[value.Name].Minimum, ranges[value.Name].Maximum);
            Assert.InRange(item.Effects.Count, counts.EffectsMin, counts.EffectsMax);
            Assert.Null(item.Generation);
        }
    }

    [Fact]
    public async Task GeneratedInstancesPersistWithoutRerollAndLegacyPropertiesStayIntact()
    {
        var config = new RpgLootConfig();
        RpgItem Make(IDuelRandom rng)
        {
            var item = new RpgItem { Slot = EquipmentSlot.Gloves, Rarity = ItemRarity.Legendary,
                Generation = RpgItemGenerationService.DefaultRules(EquipmentSlot.Gloves, ItemRarity.Legendary) };
            RpgItemGenerationService.Generate(item, config, rng); return item;
        }
        var first = Make(new Low()); var second = Make(new High());
        Assert.NotEqual(JsonSerializer.Serialize(first.Attributes), JsonSerializer.Serialize(second.Attributes));
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", a =>
        {
            a.Points = 1234; a.Rpg.Inventory.Add(first); a.Rpg.Inventory.Add(second);
            a.Rpg.Inventory.Add(new() { InstanceId = "legacy", Slot = EquipmentSlot.Gloves, Attributes = new() { Damage = 42 } });
            a.Rpg.Equipment[EquipmentSlot.Gloves] = first.InstanceId;
            a.Rpg.Fish["sardine"] = new() { Quantity = 4 }; a.Rpg.Perks["damage"] = 2;
            a.Rpg.Sushi["fishing"] = 2; a.Rpg.Buffs.Add(new() { Activity = RpgActivity.Dive }); return new(true);
        }, default);
        config.Effects[0].Chance = 99;
        var saved = (await new ViewerPointStore(_directory).GetProfileAsync("42", default)).Entry;
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(saved.Rpg.Inventory[0]));
        Assert.Equal(42, saved.Rpg.Inventory[2].Attributes.Damage); Assert.Empty(saved.Rpg.Inventory[2].Effects);
        Assert.Equal(1234, saved.Points); Assert.Equal(4, saved.Rpg.Fish["sardine"].Quantity);
        Assert.Equal(2, saved.Rpg.Sushi["fishing"]); Assert.Single(saved.Rpg.Buffs); Assert.Equal(2, saved.Rpg.Perks["damage"]);
        Assert.Equal(first.InstanceId, saved.Rpg.Equipment[EquipmentSlot.Gloves]);
    }

    [Fact]
    public void DoubleAttackUsesCalculatedCriticalHitAndDoesNotConsumeBuff()
    {
        var p = Equipped(new() { CritChance = 50, CritDamage = 50 }, Effect(RpgEffectTrigger.DoubleAttack));
        p.Buffs.Add(new() { Activity = RpgActivity.Boss, RemainingUses = 3 });
        var hit = RpgPassiveService.BossAttack(20, p, new(), new Low(), Now);
        Assert.True(hit.Critical); Assert.Equal(60, hit.Damage); Assert.Equal(1, hit.ExtraHits);
        Assert.Equal(3, p.Buffs[0].RemainingUses);
    }

    [Fact]
    public void TripleHasPriorityAndCannotRecurseOrTriggerDoubleCooldown()
    {
        var d = Effect(RpgEffectTrigger.DoubleAttack); d.CooldownSeconds = 60;
        var t = Effect(RpgEffectTrigger.TripleAttack, 3); t.CooldownSeconds = 60;
        var p = Equipped(null, d, t);
        var hit = RpgPassiveService.BossAttack(20, p, new(), new Low(), Now);
        Assert.Equal(40, hit.Damage); Assert.Equal(2, hit.ExtraHits);
        Assert.Single(p.Cooldowns); Assert.Contains(t.Id, p.Cooldowns.Single().Key);
        Assert.Equal(0, RpgPassiveService.TryProc(p, new(), RpgActivity.Boss, RpgEffectTrigger.TripleAttack, new Low(), Now));
        Assert.True(RpgPassiveService.TryProc(p, new(), RpgActivity.Boss, RpgEffectTrigger.TripleAttack, new Low(), Now.AddSeconds(60)) > 0);
    }

    [Fact]
    public void ConditionsActivityAndEffectiveCapsApplyToAllSources()
    {
        var e = Effect(RpgEffectTrigger.ExtraLoot, 100); e.MinimumStake = 500; e.Activity = RpgActivity.Dive;
        var p = Equipped(new() { DoubleAttackChance = 100, TripleAttackChance = 100, ExtraLootChance = 100,
            SushiPreservationChance = 100, PearlBonus = 100, CritChance = 100 }, e);
        p.Buffs.Add(new() { Activity = RpgActivity.Dive, Attributes = new() { ExtraLootChance = 100, PearlBonus = 100 } });
        var c = new RpgConfig(); c.Perks[0].BonusPerLevel.ExtraLootChance = 100; p.Perks["damage"] = 5;
        var s = CharacterStatService.Calculate(p, c, RpgActivity.Dive);
        Assert.Equal(20, s.DoubleAttackChance); Assert.Equal(5, s.TripleAttackChance);
        Assert.Equal(10, s.ExtraLootChance); Assert.Equal(20, s.SushiPreservationChance); Assert.Equal(15, s.PearlBonus); Assert.Equal(50, s.CritChance);
        Assert.Equal(0, RpgPassiveService.TryProc(p, c, RpgActivity.Dive, RpgEffectTrigger.ExtraLoot, new Fixed(1000), Now, 500));
        Assert.Equal(0, RpgPassiveService.TryProc(p, c, RpgActivity.Fishing, RpgEffectTrigger.ExtraLoot, new Low(), Now, 500));
        var conditional = Equipped(null, e);
        Assert.Equal(0, RpgPassiveService.TryProc(conditional, c, RpgActivity.Dive, RpgEffectTrigger.ExtraLoot, new Low(), Now, 499));
        Assert.Equal(10, CharacterStatService.Calculate(conditional, c, RpgActivity.Dive, now: Now, stake: 500).ExtraLootChance);
    }

    [Fact]
    public void SushiSavesEachMatchingBuffButNeverIndefinitely()
    {
        var p = Equipped(new() { SushiPreservationChance = 20 });
        p.Buffs.Add(new() { RecipeId = "a", Activity = RpgActivity.Dive, RemainingUses = 3 });
        p.Buffs.Add(new() { RecipeId = "b", Activity = RpgActivity.Dive, RemainingUses = 3 });
        p.Buffs.Add(new() { RecipeId = "c", Activity = RpgActivity.Fishing, RemainingUses = 3 });
        RpgPassiveService.ConsumeBuffs(p, RpgActivity.Dive, new(), new Low(), Now);
        Assert.All(p.Buffs, b => Assert.Equal(3, b.RemainingUses));
        for (var i = 1; i < 6; i++) RpgPassiveService.ConsumeBuffs(p, RpgActivity.Dive, new(), new Low(), Now.AddSeconds(i));
        Assert.Equal("c", Assert.Single(p.Buffs).RecipeId); Assert.Empty(p.Cooldowns);
    }

    [Theory]
    [InlineData(100, 100, 15, 100)] [InlineData(0, 100, 15, 0)] [InlineData(50, 100, 15, 50)]
    [InlineData(200, 100, 15, 215)] [InlineData(200, 100, 1000, 215)]
    public void PearlBonusNeverMultipliesStakeOrRefund(long payout, long stake, decimal bonus, long expected) =>
        Assert.Equal(expected, RpgPassiveService.RewardPayout(payout, stake, bonus));

    [Theory]
    [InlineData(100)] [InlineData(10000)]
    public async Task ExtraLootOnlyAddsOneItemRollAndUsesSameLegendaryGate(long stake)
    {
        var config=new MinigameConfig{Enabled=true,PointsEnabled=true};config.Adventures.Rpg.Enabled=true;var area=config.Adventures.Rpg.Zones.Get("sunny_lagoon");area.MimicChance=area.EnemyChance=0;area.ChestChance=100;
        var store=new ViewerPointStore(_directory);await store.ApplyRpgAsync("42","Otter","seed",a=>{a.Points=50000;a.Rpg=Equipped(new(){ExtraLootChance=10});return new(true);},default);
        var service=new RpgAdventureService(store,config,new Low(),()=>Now);var result=await service.ExecuteAsync("42","Otter","dive",RpgActivity.Dive,stake,default);
        Assert.True(result.Success);Assert.Equal(0,result.Stake);Assert.Equal(10,result.Payout);Assert.Equal(50010,result.Balance);Assert.Null(result.Item);Assert.All(result.AdditionalItems,i=>Assert.Null(i.Slot));Assert.Equal(2,result.AdditionalItems.Count);
        Assert.False((await service.ExecuteAsync("42","Otter","dive",RpgActivity.Dive,stake,default)).Success);
    }

    [Fact]
    public async Task DescriptionsAndNumberedInventoryUseActualEffectData()
    {
        var effect = Effect(RpgEffectTrigger.DoubleAttack, 7); effect.Strength = 0.4m; effect.CooldownSeconds = 12;
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", a => { a.Rpg = Equipped(null, effect); return new(true); }, default);
        var chat = new RpgChatCommand(store, new() { Enabled = true });
        Task<string> Run(string text) => chat.ExecuteAsync(new() { UserId = "42", UserName = "Otter" }, text.Split(' ')[0], text.Split(' '), default);
        var inv = await Run("!inv"); Assert.Contains("1. 💍 Test-Ring", inv); Assert.DoesNotContain("Otterkraft", inv); Assert.DoesNotContain("ID:ring", inv);
        var pages = new List<string>(); for (var i = 1; i <= 8; i++) pages.Add(await Run("!item 1 " + i));
        var all = string.Join(" ", pages); Assert.Contains("7 % Chance", all); Assert.Contains("40 %", all); Assert.Contains("Cooldown 12 s", all);
        Assert.All(pages, p => Assert.True(p.Length <= 480));
    }
}
