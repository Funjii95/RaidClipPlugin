using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgNumberedInventoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipNumbers-" + Guid.NewGuid().ToString("N"));
    private readonly ViewerPointStore _store;
    private readonly RpgConfig _config = new() { Enabled = true };
    public RpgNumberedInventoryTests() => _store = new(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private Task<string> Chat(string text) => new RpgChatCommand(_store, _config).ExecuteAsync(
        new() { UserId = "42", UserName = "Otter", Id = Guid.NewGuid().ToString("N") }, text.Split(' ')[0], text.Split(' '), default);
    private Task<RpgTransactionResult> Change(Action<RpgProfile> action) => _store.ApplyRpgAsync("42", "Otter", Guid.NewGuid().ToString("N"),
        a => { action(a.Rpg); return new(true); }, default);
    private static RpgItem Item(string id, EquipmentSlot? slot) => new() { InstanceId = id, DisplayName = "Otteritem", Slot = slot,
        Kind = slot.HasValue ? RpgItemKind.Equipment : RpgItemKind.Treasure, Attributes = new() { Damage = 1 } };

    [Fact]
    public async Task EmptyAndMissingViewAreExplicit()
    {
        Assert.Contains("zuerst !inv", await Chat("!equip 1"));
        Assert.Contains("Noch keine Items", await Chat("!inv"));
        Assert.Contains("existiert nicht", await Chat("!equip 1"));
    }

    [Theory]
    [InlineData(EquipmentSlot.Gloves)] [InlineData(EquipmentSlot.Weapon)] [InlineData(EquipmentSlot.Chest)]
    [InlineData(EquipmentSlot.Boots)] [InlineData(EquipmentSlot.Helmet)] [InlineData(EquipmentSlot.Amulet)]
    public async Task SlotAutomaticallyResolvedAndSurvivesRestart(EquipmentSlot slot)
    {
        await Change(p => p.Inventory.Add(Item("internal-id", slot)));
        Assert.DoesNotContain("internal-id", await Chat("!inv"));
        Assert.Contains("Otteritem", await Chat("!item 1"));
        Assert.Contains("rüstet", await Chat("!equip 1"));
        Assert.Contains("ausgerüstet", await Chat("!gear"));
        var saved = (await new ViewerPointStore(_directory).GetProfileAsync("42", default)).Entry;
        Assert.Equal("internal-id", saved.Rpg.Equipment[slot]);
    }

    [Fact]
    public async Task IdenticalRingsUseFreeSlotsThenRequireExplicitReplacement()
    {
        await Change(p => { for (var i = 1; i <= 3; i++) p.Inventory.Add(Item("ring-" + i, EquipmentSlot.Ring1)); });
        await Chat("!inv");
        Assert.Contains("Ring 1", await Chat("!equip 1"));
        Assert.Contains("Ring 2", await Chat("!equip 2"));
        Assert.Contains("Beide Ringplätze", await Chat("!equip 3"));
        Assert.Contains("Ring 2", await Chat("!equip 3 ring2"));
        await Chat("!equip 1 ring2");
        var p = (await _store.GetProfileAsync("42", default)).Entry.Rpg;
        Assert.Single(p.Equipment); Assert.Equal("ring-1", p.Equipment[EquipmentSlot.Ring2]);
    }

    [Fact]
    public async Task ChangesRejectStaleNumbersWithoutEquippingAnotherItem()
    {
        await Change(p => { p.Inventory.Add(Item("a", EquipmentSlot.Gloves)); p.Inventory.Add(Item("b", EquipmentSlot.Gloves)); });
        await Chat("!inv");
        await Change(p => p.Inventory.RemoveAt(0));
        Assert.Contains("geändert", await Chat("!equip 1"));
        Assert.Contains("geändert", await Chat("!item 1"));
        Assert.Empty((await _store.GetProfileAsync("42", default)).Entry.Rpg.Equipment);
        await Chat("!inv"); await Chat("!equip 1");
        Assert.Equal("b", (await _store.GetProfileAsync("42", default)).Entry.Rpg.Equipment[EquipmentSlot.Gloves]);
    }

    [Fact]
    public async Task ConcurrentRingActionsNeverDuplicateAnInstance()
    {
        await Change(p => p.Inventory.Add(Item("a", EquipmentSlot.Ring1))); await Chat("!inv");
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Chat("!equip 1 ring" + (i % 2 + 1))));
        var p = (await _store.GetProfileAsync("42", default)).Entry.Rpg;
        Assert.Single(p.Equipment); Assert.Equal("a", p.Equipment.Single().Value);
    }

    [Fact]
    public async Task ConcurrentInventoryMutationEitherEquipsOriginalOrRejectsButNeverReplacement()
    {
        await Change(p => { p.Inventory.Add(Item("original", EquipmentSlot.Gloves)); p.Inventory.Add(Item("replacement", EquipmentSlot.Weapon)); });
        await Chat("!inv");
        var equip = Chat("!equip 1");
        var mutation = Change(p => { p.Inventory.RemoveAt(0); p.Equipment.Clear(); });
        await Task.WhenAll(equip, mutation);
        var p = (await _store.GetProfileAsync("42", default)).Entry.Rpg;
        Assert.DoesNotContain("replacement", p.Equipment.Values);
        Assert.True((await equip).Contains("rüstet") || (await equip).Contains("geändert"));
        Assert.Contains("geändert", await Chat("!equip 1"));
    }

    [Fact]
    public async Task NumberingIsGlobalAndStableAcrossPagesAndRestart()
    {
        await Change(p => { for (var i = 0; i < 12; i++) p.Inventory.Add(Item("id-" + i, EquipmentSlot.Gloves)); });
        var first = await Chat("!inv");
        Assert.Contains("1. 🧤 Otteritem", first);
        var page2 = await Chat("!inv 2"); Assert.DoesNotContain("1. 🧤 Otteritem", page2);
        Assert.Contains("rüstet", await Chat("!equip 12"));
        Assert.Equal("id-11", (await _store.GetProfileAsync("42", default)).Entry.Rpg.Equipment[EquipmentSlot.Gloves]);
        var restarted = new RpgChatCommand(new(_directory), _config);
        var reply = await restarted.ExecuteAsync(new() { UserId = "42", UserName = "Otter" }, "!inv", new[] { "!inv" }, default);
        Assert.Contains("1. 🧤 Otteritem", reply);
    }

    [Fact]
    public async Task TreasureAndInvalidSlotsCannotBeEquippedAndLegacyIdsStillWork()
    {
        await Change(p => { p.Inventory.Add(Item("treasure", null)); p.Inventory.Add(Item("old-id", EquipmentSlot.Gloves)); });
        await Chat("!inv");
        Assert.Contains("Schatz", await Chat("!equip 1"));
        Assert.Contains("Unpassender", await Chat("!equip 2 ring2"));
        Assert.Contains("rüstet", await Chat("!equip old-id Gloves"));
    }
}
