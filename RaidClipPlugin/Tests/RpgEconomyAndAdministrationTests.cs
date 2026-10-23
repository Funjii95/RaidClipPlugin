using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgEconomyAndAdministrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipEconomyTests-" + Guid.NewGuid().ToString("N"));
    private static readonly CancellationToken Token = CancellationToken.None;
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public void NewDefaultsMatchEconomy12WithoutChangingOtherGames()
    {
        var config = new MinigameConfig();
        var rpg = config.Adventures.Rpg;
        rpg.Validate();
        Assert.Equal(25, rpg.DiveLoot.BaseDropChance);
        Assert.Equal(10000, rpg.DiveLoot.LegendaryMinimumStake);
        Assert.Equal(new long[] { 0, 250, 1000, 5000, 10000 }, rpg.DiveLoot.RiskTiers.Select(t => t.MinimumStake));
        Assert.Equal(new decimal[] { 0, 4, 8, 12, 15 }, rpg.DiveLoot.RiskTiers.Select(t => t.DropBonus));
        Assert.Equal(new[] { 6000, 2900, 850, 200, 50 }, Enum.GetValues<ItemRarity>().Where(r=>r<=ItemRarity.Legendary).Select(r => rpg.DiveLoot.RarityWeights[r]));
        Assert.Equal(10000, config.Adventures.Treasure.MaximumBet);
        Assert.Equal(1000, config.Adventures.Fishing.MaximumBet);
        Assert.Equal(1000, config.MaximumBet);
        Assert.Equal(1000, config.CoinflipMaximumBet);
        Assert.Equal(15, config.Adventures.Boss.AutoStartMinutes);
        Assert.Equal(500, config.Adventures.Boss.RewardPool);
        foreach (var perk in rpg.Perks)
        {
            Assert.Equal(new long[] { 500, 1000, 2000, 4000, 8000 }, Enumerable.Range(0, 5).Select(perk.Cost));
            Assert.Equal(15500, Enumerable.Range(0, 5).Sum(perk.Cost));
        }
        Assert.Equal(new long[] { 250, 500, 1000, 1500 }, rpg.Recipes.Take(4).Select(r => r.PearlCost));
        Assert.Equal(new[] { 6500, 2500, 800, 190, 10 }, rpg.FishSpecies.Take(5).Select(f => f.Weight));
    }

    [Fact]
    public void LoadingSavedValuesNeverSilentlyAppliesPreset()
    {
        const string legacy = """
        {"Adventures":{"Treasure":{"MaximumBet":7777},"Rpg":{"Enabled":true,"DiveLoot":{"BaseDropChance":20,"LegendaryMinimumStake":5000,"Items":[]},"Perks":[{"Id":"crit","MaximumLevel":5,"BaseCost":250,"CostGrowth":2,"BonusPerLevel":{"CritChance":3}}],"Recipes":[{"Id":"fishing","FishId":"sardine","PearlCost":100,"Attributes":{"FishingLuck":9}}]}}}
        """;
        var loaded = JsonSerializer.Deserialize<MinigameConfig>(legacy)!;
        Assert.Equal(7777, loaded.Adventures.Treasure.MaximumBet);
        Assert.Equal(20, loaded.Adventures.Rpg.DiveLoot.BaseDropChance);
        Assert.Equal(5000, loaded.Adventures.Rpg.DiveLoot.LegendaryMinimumStake);
        Assert.Equal(250, Assert.Single(loaded.Adventures.Rpg.Perks).BaseCost);
        var before = JsonSerializer.Serialize(loaded);
        var applied = RpgEconomyPreset.Apply12(loaded.Adventures.Rpg);
        Assert.Equal(before, JsonSerializer.Serialize(loaded));
        Assert.Equal(500, Assert.Single(applied.Perks).BaseCost);
        Assert.Equal(3, Assert.Single(applied.Perks).BonusPerLevel.CritChance);
        Assert.Equal(9, Assert.Single(applied.Recipes).Attributes.FishingLuck);
        Assert.Equal(250, Assert.Single(applied.Recipes).PearlCost);
        Assert.True(applied.Enabled);
    }

    [Fact]
    public void EquipmentPerksAndSushiShareAttributeCaps()
    {
        var config = new RpgConfig();
        var profile = new RpgProfile();
        foreach (var perk in config.Perks) profile.Perks[perk.Id] = 5;
        var bonus = new RpgAttributes { Damage = 150, CritChance = 40, CritDamage = 250, Luck = 80, BossDamage = 150, FishingLuck = 80, DiveLuck = 80 };
        profile.Inventory.Add(new() { InstanceId = "ring", Attributes = bonus });
        profile.Equipment[EquipmentSlot.Ring1] = "ring";
        profile.Buffs.Add(new() { Activity = RpgActivity.Boss, Attributes = bonus });
        var stats = CharacterStatService.Calculate(profile, config, RpgActivity.Boss);
        Assert.Equal(200, stats.Damage); Assert.Equal(50, stats.CritChance);
        Assert.Equal(300, stats.CritDamage); Assert.Equal(100, stats.Luck);
        Assert.Equal(200, stats.BossDamage); Assert.Equal(100, stats.FishingLuck); Assert.Equal(100, stats.DiveLuck);
    }

    private async Task<ViewerPointStore> Seed()
    {
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "seed", account =>
        {
            account.Points = 12345;
            account.FishCollection["Hecht"] = 99;
            account.Rpg.Fish["perch"] = new() { FishId = "perch", Quantity = 5 };
            account.Rpg.Inventory.Add(new() { InstanceId = "ring", Slot = EquipmentSlot.Ring1, Attributes = new() { CritChance = 5 } });
            account.Rpg.Inventory.Add(new() { InstanceId = "other", Slot = EquipmentSlot.Weapon });
            account.Rpg.Equipment[EquipmentSlot.Ring1] = "ring";
            account.Rpg.Perks["crit"] = 3;
            account.Rpg.Buffs.Add(new() { Activity = RpgActivity.Dive });
            return new(true);
        }, Token);
        return store;
    }

    [Fact]
    public async Task DeleteSelectedFishQuantityPersistsWithoutChangingOtherPlayerData()
    {
        var store = await Seed();
        var admin = new RpgAdministrationService(store);
        Assert.False((await admin.DeleteFishAsync("42", "Otter", "too-many", "perch", 6, Token)).Success);
        Assert.True((await admin.DeleteFishAsync("42", "Otter", "delete", "perch", 2, Token)).Success);
        Assert.True((await admin.DeleteFishAsync("42", "Otter", "delete", "perch", 2, Token)).Duplicate);
        var profile = (await new ViewerPointStore(_directory).GetProfileAsync("42", Token)).Entry;
        Assert.Equal(3, profile.Rpg.Fish["perch"].Quantity);
        Assert.Equal(99, profile.FishCollection["Hecht"]);
        Assert.Equal(12345, profile.Points); Assert.Equal(3, profile.Rpg.Perks["crit"]);
        Assert.Equal(2, profile.Rpg.Inventory.Count); Assert.Single(profile.Rpg.Buffs);
        Assert.Single(Directory.GetFiles(_directory, "*.rpg-delete-*.bak"));
        Assert.True((await admin.DeleteFishAsync("42", "Otter", "delete-all", "perch", 3, Token)).Success);
        await store.ApplyRpgAsync("42", "Otter", "later-action", _ => new(true), Token);
        Assert.Empty((await store.GetProfileAsync("42", Token)).Entry.Rpg.Fish);
    }

    [Fact]
    public async Task DeleteEquipmentRemovesOnlyChosenInstanceAndItsSlot()
    {
        var store = await Seed();
        var admin = new RpgAdministrationService(store);
        Assert.True((await admin.DeleteEquipmentAsync("42", "Otter", "delete-ring", "ring", Token)).Success);
        Assert.False((await admin.DeleteEquipmentAsync("42", "Otter", "delete-missing", "ring", Token)).Success);
        var profile = (await new ViewerPointStore(_directory).GetProfileAsync("42", Token)).Entry;
        Assert.Equal("other", Assert.Single(profile.Rpg.Inventory).InstanceId);
        Assert.Empty(profile.Rpg.Equipment);
        Assert.Equal(12345, profile.Points); Assert.Equal(3, profile.Rpg.Perks["crit"]);
        Assert.Equal(5, profile.Rpg.Fish["perch"].Quantity);
        Assert.Single(Directory.GetFiles(_directory, "*.rpg-delete-*.bak"));
    }

    [Fact]
    public async Task FailedDeleteSaveRollsBackInventoryAndSlot()
    {
        var store = await Seed();
        Directory.CreateDirectory(Path.Combine(_directory, "viewer-points.json.tmp"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new RpgAdministrationService(store).DeleteEquipmentAsync("42", "Otter", "delete-ring", "ring", Token));
        var profile = (await store.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(2, profile.Rpg.Inventory.Count);
        Assert.Equal("ring", profile.Rpg.Equipment[EquipmentSlot.Ring1]);
    }
}
