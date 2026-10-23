using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgProgressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipProgressTests-" + Guid.NewGuid().ToString("N"));
    private static readonly CancellationToken Token = CancellationToken.None;
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public async Task PerkCostsProgressAndSurviveRestart()
    {
        var store = new ViewerPointStore(_directory);
        var config = new RpgConfig { Enabled = true };
        var service = new RpgProgressionService(store, config);
        await store.AddPointsAsync("42", "Otter", 2000, 0, Token);
        Assert.True((await service.BuyPerkAsync("42", "Otter", "buy1", "crit", Token)).Success);
        Assert.True((await service.BuyPerkAsync("42", "Otter", "buy2", "crit", Token)).Success);
        Assert.False((await service.BuyPerkAsync("42", "Otter", "buy3", "crit", Token)).Success);
        var profile = (await new ViewerPointStore(_directory).GetProfileAsync("42", Token)).Entry;
        Assert.Equal(500, profile.Points);
        Assert.Equal(2, profile.Rpg.Perks["crit"]);
    }

    [Fact]
    public async Task RingCannotOccupyBothSlotsAndChangesActualCritDamage()
    {
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", account =>
        {
            account.Rpg.Inventory.Add(new() { InstanceId = "ring1", Slot = EquipmentSlot.Ring1,
                Attributes = new() { CritChance = 5, CritDamage = 10 } });
            return new(true);
        }, Token);
        var config = new RpgConfig { Enabled = true };
        var service = new RpgProgressionService(store, config);
        Assert.True((await service.EquipAsync("42", "Otter", "equip1", "ring1", EquipmentSlot.Ring1, Token)).Success);
        Assert.True((await service.EquipAsync("42", "Otter", "equip2", "ring1", EquipmentSlot.Ring2, Token)).Success);
        var profile = (await store.GetProfileAsync("42", Token)).Entry.Rpg;
        Assert.Single(profile.Equipment);
        var stats = CharacterStatService.Calculate(profile, config, RpgActivity.Boss);
        Assert.Equal(5, stats.CritChance);
        Assert.Equal(32, CharacterStatService.BossDamage(20, stats, 4, out var critical));
        Assert.True(critical);
        Assert.Equal(20, CharacterStatService.BossDamage(20, stats, 5, out critical));
        Assert.False(critical);
    }

    [Fact]
    public async Task SushiCraftingAndActivationDoNotConsumeParticipationCharges()
    {
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", account =>
        {
            account.Points = 1000;
            account.Rpg.Fish["sardine"] = new() { FishId = "sardine", Quantity = 2 };
            return new(true);
        }, Token);
        var service = new RpgProgressionService(store, new() { Enabled = true });
        Assert.True((await service.CraftAsync("42", "Otter", "craft", "fishing", Token)).Success);
        Assert.True((await service.ActivateSushiAsync("42", "Otter", "activate", "fishing", Token)).Success);
        Assert.True((await service.CraftAsync("42", "Otter", "craft2", "fishing", Token)).Success);
        Assert.False((await service.ActivateSushiAsync("42", "Otter", "activate2", "fishing", Token)).Success);
        var profile = (await store.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(500, profile.Points);
        Assert.Equal(1, profile.Rpg.Sushi["fishing"]);
        Assert.Equal(3, Assert.Single(profile.Rpg.Buffs).RemainingUses);
        RpgProgressionService.ConsumeBuffs(profile.Rpg, RpgActivity.Dive);
        Assert.Equal(3, Assert.Single(profile.Rpg.Buffs).RemainingUses);
        for (var i = 0; i < 3; i++) RpgProgressionService.ConsumeBuffs(profile.Rpg, RpgActivity.Fishing);
        Assert.Equal(3,Assert.Single(profile.Rpg.Buffs).RemainingUses);
        Assert.Equal(TimeSpan.FromHours(1),profile.Rpg.Buffs[0].ExpiresAt-profile.Rpg.Buffs[0].ActivatedAt);
    }

    [Fact]
    public void CharacterStatsAreCappedAfterCombiningEquipmentPerksAndBuffs()
    {
        var profile = new RpgProfile();
        profile.Perks["crit"] = 5;
        profile.Inventory.Add(new() { InstanceId = "ring", Attributes = new() { CritChance = 99, Luck = 5000, BossDamage = 5000 } });
        profile.Equipment[EquipmentSlot.Ring1] = "ring";
        var stats = CharacterStatService.Calculate(profile, new(), RpgActivity.Boss);
        Assert.Equal(50, stats.CritChance);
        Assert.Equal(100, stats.Luck);
        Assert.Equal(200, stats.BossDamage);
    }
}
