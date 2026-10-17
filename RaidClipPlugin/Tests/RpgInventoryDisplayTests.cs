using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgInventoryDisplayTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipInventory-" + Guid.NewGuid().ToString("N"));
    private readonly RpgConfig _config = new() { Enabled = true };
    private readonly ViewerPointStore _points;
    public RpgInventoryDisplayTests() { _points = new(_directory); }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private Task<string> Chat(string text, string userId = "42") => new RpgChatCommand(_points, _config).ExecuteAsync(
        new() { UserId = userId, UserName = "Funjii", Id = Guid.NewGuid().ToString("N") }, text.Split(' ')[0], text.Split(' '), default);
    private Task<RpgTransactionResult> Seed(Action<ViewerPointEntry> change) => _points.ApplyRpgAsync("42", "Funjii", Guid.NewGuid().ToString("N"),
        account => { change(account); return new(true); }, default);

    [Theory]
    [InlineData("!inventar")]
    [InlineData("!inventory")]
    public async Task EmptyInventoryIsExplicitAndNotJustAHeading(string command)
    {
        var text = await Chat(command);
        Assert.Contains("Noch keine Items", text); Assert.DoesNotContain("1/1:", text);
    }

    [Fact]
    public async Task OneItemShowsNameRarityBonusesFullUsableIdAndEquippedState()
    {
        await Seed(a => { a.Rpg.Inventory.Add(new() { InstanceId = "actual-ring-id", ItemId = "deep-ring", DisplayName = "Ring des Tiefenmeeres",
            Rarity = ItemRarity.Epic, Slot = EquipmentSlot.Ring1, Attributes = new() { CritChance = 4, Luck = 5 } });
            a.Rpg.Equipment[EquipmentSlot.Ring1] = "actual-ring-id"; });
        var text = await Chat("!inventar");
        Assert.Contains("Ring des Tiefenmeeres [Epic]", text); Assert.DoesNotContain("+4", text);
        Assert.Contains("ausgerüstet: Ring 1", text); Assert.Contains("1. 💍 Ring", text); Assert.DoesNotContain("actual-ring-id", text);
        Assert.True(text.Length <= 480);
        var details = await Chat("!item actual-ring-id"); Assert.Contains("Ring des Tiefenmeeres", details);
        Assert.DoesNotContain("nicht in deinem Inventar", details);
    }

    [Fact]
    public async Task MultiplePagesPreserveEveryItemAndOutOfRangeDoesNotReturnAnEmptyHeading()
    {
        await Seed(a => { for (var i = 0; i < 9; i++) a.Rpg.Inventory.Add(new() { InstanceId = "id-" + i, DisplayName = "Item-" + i, Attributes = new() { Damage = i + 1 } }); });
        var all = new List<string>();
        for (var page = 1; page <= 9; page++)
        { var text = await Chat("!inventar " + page); Assert.True(text.Length <= 480); all.Add(text); }
        for (var i = 0; i < 9; i++) Assert.Contains(all, text => text.Contains((i + 1) + ". 🦦 Item-" + i));
        Assert.Contains("9. 🦦 Item-8", await Chat("!inventar 2147483647"));
    }

    [Fact]
    public async Task LongNamesAndEveryAttributeRemainAvailableThroughItemDetailPages()
    {
        var longName = string.Concat(Enumerable.Repeat("Otter🌊", 90)) + "NAME-END";
        await Seed(a => a.Rpg.Inventory.Add(new() { InstanceId = "long-item", DisplayName = longName, Description = new string('D', 700) + "DESCRIPTION-END",
            Rarity = ItemRarity.Legendary, Attributes = new() { Damage = 2, CritChance = 3, CritDamage = 4, Luck = 5, BossDamage = 6, PearlBonus = 7, FishingLuck = 8, DiveLuck = 9 } }));
        var pages = new List<string>();
        for (var page = 1; page <= 20; page++)
        { var text = await Chat("!item long-item " + page); Assert.True(text.Length <= 480); Assert.DoesNotContain("…", text); pages.Add(text); }
        var combined = string.Join(" ", pages);
        Assert.Contains("NAME-END", combined); Assert.Contains("DESCRIPTION-END", combined); Assert.DoesNotContain("ID:long-item", combined);
        foreach (var bonus in new[] { "+2 % Schaden", "+3 % Kritische Trefferchance", "+4 % Kritischer Schaden", "+5 Glück", "+6 % Boss-Schaden", "+7 % Perlenbonus", "+8 Angelglück", "+9 Tauchglück" }) Assert.Contains(bonus, combined);
        Assert.Contains("💎", combined);
    }

    [Fact]
    public async Task MissingDisplayNameFallsBackWithoutChangingTheStoredItem()
    {
        await Seed(a => a.Rpg.Inventory.Add(new() { InstanceId = "named-by-id", ItemId = "old-shell", DisplayName = "" }));
        var before = File.ReadAllBytes(Path.Combine(_directory, "viewer-points.json"));
        Assert.Contains("old-shell [Common]", await Chat("!inventar"));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(_directory, "viewer-points.json")));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"InstanceId\":\"id\",\"Attributes\":null}]")]
    [InlineData("[{\"DisplayName\":\"Broken without ID\"}]")]
    public async Task InvalidInventoryIsNotReportedAsEmptyAndFileIsNotChanged(string inventory)
    {
        var path = Path.Combine(_directory, "viewer-points.json");
        var json = "{\"42\":{\"UserId\":\"42\",\"Rpg\":{\"Inventory\":" + inventory + "}}}";
        await File.WriteAllTextAsync(path, json);
        var text = await Chat("!inventar");
        Assert.Contains("Protokoll", text); Assert.DoesNotContain("Noch keine Items", text); Assert.DoesNotContain("Inventar 1/1:", text);
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task BrokenJsonAndMissingInventoryAreLoadErrorsButLegacyAccountsRemainReadable()
    {
        var path = Path.Combine(_directory, "viewer-points.json");
        foreach (var source in new[] { "broken json", "{\"42\":{\"UserId\":\"42\",\"Rpg\":{}}}" })
        {
            await File.WriteAllTextAsync(path, source);
            var response = await new RpgChatCommand(new(_directory), _config).ExecuteAsync(new() { UserId = "42", UserName = "Funjii" }, "!inventar", new[] { "!inventar" }, default);
            Assert.Contains("konnten nicht geladen", response); Assert.Equal(source, await File.ReadAllTextAsync(path));
        }
        await File.WriteAllTextAsync(path, "{\"42\":{\"UserId\":\"42\",\"Points\":123}}" );
        var legacy = await new RpgChatCommand(new(_directory), _config).ExecuteAsync(new() { UserId = "42", UserName = "Funjii" }, "!inventar", new[] { "!inventar" }, default);
        Assert.Contains("Noch keine Items", legacy);
    }

    [Fact]
    public async Task AuthoritativeTwitchKeyDoesNotHideItemsWhenDuplicateUserIdFieldIsMissing()
    {
        var path = Path.Combine(_directory, "viewer-points.json");
        const string source = """
        {"42":{"Points":123,"Rpg":{"Inventory":[{"InstanceId":"legacy-item","DisplayName":"Vorhandenes Item"}]}}}
        """;
        await File.WriteAllTextAsync(path, source);
        Assert.Contains("Vorhandenes Item", await Chat("!inventar"));
        Assert.Equal(source, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task FormattingFailureIsDistinctFromLoadFailureAndChangesNothing()
    {
        await Seed(a => a.Points = 1000);
        var path = Path.Combine(_directory, "viewer-points.json"); var before = File.ReadAllBytes(path);
        // Invalid configured price breaks next-cost formatting, not account loading.
        _config.Perks[0].BaseCost = 0;
        var response = await Chat("!perks");
        Assert.Contains("Antwort konnte nicht erstellt", response);
        Assert.DoesNotContain("konnten nicht geladen", response);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ItemDetailsKeepFractionalValuesAndNeverAccessOtherUsersItems()
    {
        await Seed(a => a.Rpg.Inventory.Add(new() { InstanceId = "fractional", DisplayName = "Präzisionsring", Attributes = new() { CritChance = 0.00125m } }));
        Assert.Contains("+0,00125 % Kritische Trefferchance", await Chat("!item fractional"));
        Assert.Contains("nicht in deinem Inventar", await Chat("!item fractional", "43"));
        var app = new AppConfig(); app.Minigame.Enabled = true; app.Minigame.Adventures.Rpg.Enabled = true;
        var registry = new CommandRegistry(); registry.Update(app);
        Assert.True(Assert.Single(registry.Commands, c => c.CommandText == "!item").Enabled);
        Assert.True(Assert.Single(registry.Commands, c => c.CommandText == "!rpgtext").Enabled);
    }

    [Fact]
    public async Task LongCraftReplyIsPagedWithoutRepeatingTheCraftOrLeakingBetweenAccounts()
    {
        _config.Recipes.Single(r => r.Id == "fishing").DisplayName = new string('S', 800) + "SUSHI-END";
        await Seed(a => { a.Points = 1000; a.Rpg.Fish["sardine"] = new() { FishId = "sardine", Quantity = 2 }; });
        var replies = new List<string> { await Chat("!craft fishing") };
        Assert.Contains("Weiter: !rpgtext 2", replies[0]);
        for (var page = 2; page <= 12; page++) replies.Add(await Chat("!rpgtext " + page));
        Assert.All(replies, text => Assert.True(text.Length <= 480));
        Assert.Contains("SUSHI-END", string.Join(" ", replies));
        var profile = (await _points.GetProfileAsync("42", default)).Entry;
        Assert.Equal(750, profile.Points); Assert.Equal(1, profile.Rpg.Sushi["fishing"]);
        Assert.Contains("keine längere Antwort", await Chat("!rpgtext 2", "other-account"));
    }
}
