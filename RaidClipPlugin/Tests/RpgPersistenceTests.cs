using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipRpgTests-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task InventoryAndCurrencyPersistTogetherAndProfilesAreDetached()
    {
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        var result = await store.ApplyRpgAsync("42", "Otter", "purchase-1", account =>
        {
            account.Points -= 250;
            account.Rpg.Perks["crit"] = 1;
            account.Rpg.Inventory.Add(new RpgItem { ItemId = "ring", Rarity = ItemRarity.Rare });
            account.Rpg.Buffs.Add(new RpgBuff());
            return new(true);
        }, Token);
        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_directory, "viewer-points.json.pre-rpg.bak")));
        var restarted = new ViewerPointStore(_directory);
        var profile = (await restarted.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(750, profile.Points);
        Assert.Equal(1, profile.Rpg.Perks["crit"]);
        Assert.Single(profile.Rpg.Inventory);
        Assert.Equal(3, Assert.Single(profile.Rpg.Buffs).RemainingUses);
        profile.Rpg.Inventory.Clear();
        Assert.Single((await restarted.GetProfileAsync("42", Token)).Entry.Rpg.Inventory);
        var duplicate = await restarted.ApplyRpgAsync("42", "Renamed", "purchase-1", _ => throw new Exception(), Token);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(750, duplicate.Balance);
    }

    [Fact]
    public async Task ConcurrentDuplicateRequestsCommitOnce()
    {
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.ApplyRpgAsync(
            "42", "Otter", "same-action", account => { account.Points -= 100; account.Rpg.Sushi["luck"] = 1; return new(true); }, Token)));
        Assert.Single(results.Where(x => x.Success));
        Assert.Equal(900, await store.GetPointsAsync("42", Token));
    }

    [Fact]
    public async Task RejectedOrThrowingMutationLeavesNoChanges()
    {
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        var result = await store.ApplyRpgAsync("42", "Otter", "rejected", account =>
        { account.Points = 0; account.Rpg.Inventory.Add(new()); return new(false, "Nicht erlaubt"); }, Token);
        Assert.False(result.Success);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ApplyRpgAsync("42", "Otter", "negative", account =>
        { account.Points = -1; return new(true); }, Token));
        Assert.Equal(1000, await store.GetPointsAsync("42", Token));
        Assert.Empty((await store.GetProfileAsync("42", Token)).Entry.Rpg.Inventory);
    }

    [Fact]
    public async Task DiskFailureRollsBackMemoryAndLeavesPreviousSave()
    {
        var store = new ViewerPointStore(_directory);
        await store.AddPointsAsync("42", "Otter", 1000, 0, Token);
        Directory.CreateDirectory(Path.Combine(_directory, "viewer-points.json.tmp"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.ApplyRpgAsync("42", "Otter", "failed-write", account =>
        { account.Points -= 100; account.Rpg.Inventory.Add(new()); return new(true); }, Token));
        Assert.Equal(1000, await store.GetPointsAsync("42", Token));
        Assert.Equal(1000, await new ViewerPointStore(_directory).GetPointsAsync("42", Token));
    }

    [Fact]
    public async Task LegacyAccountIsNotResetAndInvalidFileCannotBeOverwritten()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "viewer-points.json");
        await File.WriteAllTextAsync(path, "{\"42\":{\"UserId\":\"42\",\"Points\":1234,\"FishCollection\":{\"Hecht\":4}}}");
        var store = new ViewerPointStore(_directory);
        await store.ApplyRpgAsync("42", "Otter", "migration", _ => new(true), Token);
        var profile = (await new ViewerPointStore(_directory).GetProfileAsync("42", Token)).Entry;
        Assert.Equal(1234, profile.Points);
        Assert.Equal(4, profile.FishCollection["Hecht"]);
        await File.WriteAllTextAsync(path, "broken json");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ViewerPointStore(_directory).AddPointsAsync("42", "Otter", 1, 0, Token));
        Assert.Equal("broken json", await File.ReadAllTextAsync(path));
    }
}
