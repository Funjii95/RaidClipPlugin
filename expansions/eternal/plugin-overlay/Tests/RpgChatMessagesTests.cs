using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgChatMessagesTests : IDisposable
{
    private sealed class Lowest : IDuelRandom { public int NextInclusive(int min, int max) => min; }
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipMessages-" + Guid.NewGuid().ToString("N"));
    private readonly RpgConfig _config = new() { Enabled = true };
    private readonly ViewerPointStore _points;
    public RpgChatMessagesTests() { _points = new(_directory); }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private static readonly CancellationToken Token = CancellationToken.None;
    private Task<string> Chat(string text, string? id = null) => new RpgChatCommand(_points, _config).ExecuteAsync(
        new() { UserId = "42", UserName = "Funjii", Id = id ?? Guid.NewGuid().ToString("N") }, text.Split(' ')[0], text.Split(' '), Token);
    private Task<RpgTransactionResult> Seed(Action<ViewerPointEntry> initialize) => _points.ApplyRpgAsync("42", "Funjii", Guid.NewGuid().ToString("N"),
        account => { initialize(account); return new(true); }, Token);

    [Fact]
    public async Task CraftMessageUsesActualCustomRecipeAndCostsWithSingleDebit()
    {
        var recipe = _config.Recipes.Single(r => r.Id == "fishing");
        recipe.DisplayName = "Sushi der Otterangler"; recipe.FishQuantity = 2; recipe.PearlCost = 1234;
        _config.FishSpecies.Single(f => f.Id == "sardine").Name = "Winzige Sardine";
        await Seed(a => { a.Points = 10000; a.Rpg.Fish["sardine"] = new() { FishId = "sardine", Quantity = 5 }; });
        var response = await Chat("!craft fishing", "craft-once");
        Assert.Contains("Sushi der Otterangler hergestellt", response);
        Assert.Contains("2× Winzige Sardine + 1.234 Perlen", response);
        Assert.Contains("Erhalten: 1× Sushi der Otterangler", response);
        Assert.DoesNotContain("erledigt", response);
        var duplicate = await Chat("!craft fishing", "craft-once");
        Assert.Contains("bereits verarbeitet", duplicate);
        Assert.DoesNotContain("hergestellt", duplicate);
        var account = (await _points.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(8766, account.Points); Assert.Equal(3, account.Rpg.Fish["sardine"].Quantity);
        Assert.Equal(1, account.Rpg.Sushi["fishing"]);
    }

    [Theory]
    [InlineData("fishing", "Angelaktionen")]
    [InlineData("dive", "Tauchgänge")]
    [InlineData("fighter", "Bossangriffe")]
    public async Task ActivationMessageUsesActualAttributesActivityAndCharges(string id, string activity)
    {
        _config.BuffUses = 7;
        var recipe = _config.Recipes.Single(r => r.Id == id);
        recipe.Attributes = new() { FishingLuck = 9, CritChance = 2 };
        await Seed(a => { a.Points = 999; a.Rpg.Sushi[id] = 1; });
        var response = await Chat("!sushi " + id);
        Assert.Contains("aktiviert", response);
        Assert.Contains("+9 Angelglück", response); Assert.Contains("+2 % Kritische Trefferchance", response);
        Assert.Contains("1 Stunde", response);
        var account = (await _points.GetProfileAsync("42", Token)).Entry;
        Assert.Equal(999, account.Points); Assert.Equal(7, Assert.Single(account.Rpg.Buffs).RemainingUses);
        var second = await Chat("!sushi " + id);
        Assert.DoesNotContain("aktiviert", second);
    }

    [Fact]
    public async Task PerkMessageReportsNewLevelTotalBonusAndActualPaidPrice()
    {
        var perk = _config.Perks.Single(p => p.Id == "crit"); perk.BaseCost = 700; perk.BonusPerLevel.CritChance = 1.5m;
        await Seed(a => { a.Points = 10000; a.Rpg.Perks["crit"] = 1; });
        var response = await Chat("!perk crit");
        Assert.Contains("Kritische Welle auf Level 2", response); Assert.Contains("+3 % Kritische Trefferchance", response);
        Assert.Contains("Kosten: 1.400 Perlen", response);
        Assert.Equal(8600, await _points.GetPointsAsync("42", Token));
    }

    [Fact]
    public async Task EquipMessageReportsItemBonusesAndSlotWithoutCharging()
    {
        await Seed(a => { a.Points = 999; a.Rpg.Inventory.Add(new() { InstanceId = "ring", DisplayName = "Ring des Tiefenmeeres",
            Slot = EquipmentSlot.Ring1, Attributes = new() { CritChance = 4, Luck = 5 } }); });
        var response = await Chat("!equip ring Ring2");
        Assert.Contains("rüstet Ring des Tiefenmeeres aus (Ring 2)", response);
        Assert.Contains("+4 % Kritische Trefferchance, +5 Glück", response);
        Assert.Equal(999, await _points.GetPointsAsync("42", Token));
        Assert.Equal("ring", (await _points.GetProfileAsync("42", Token)).Entry.Rpg.Equipment[EquipmentSlot.Ring2]);
    }

    [Fact]
    public async Task FailureHasNoSuccessDetailsOrSideEffects()
    {
        var result = await new RpgProgressionService(_points, _config).CraftAsync("42", "Funjii", "no-materials", "fishing", Token);
        Assert.False(result.Success); Assert.Null(result.Details);
        var response = await Chat("!craft fishing");
        Assert.Contains("Nicht genügend Fische oder Perlen", response);
        Assert.DoesNotContain("hergestellt", response);
    }

    [Fact]
    public async Task StructuredDetailsAreDetachedFromLaterConfigChanges()
    {
        await Seed(a => { a.Points = 10000; });
        var result = await new RpgProgressionService(_points, _config).BuyPerkAsync("42", "Funjii", "buy", "crit", Token);
        var details = Assert.IsType<RpgActionDetails>(result.Details);
        _config.Perks.Single(p => p.Id == "crit").BonusPerLevel.CritChance = 99;
        Assert.Equal(1, details.Bonuses!.CritChance); Assert.Equal(500, details.Cost);
    }

    [Fact]
    public async Task EmptyViewsAndStatusExplainWhatIsMissing()
    {
        Assert.Contains("Noch keine Items", await Chat("!inventory"));
        Assert.Contains("Noch nichts ausgerüstet", await Chat("!equipment"));
        Assert.Contains("keine aktiven Buffs", await Chat("!sushi"));
        await Seed(a => { a.Rpg.Buffs.Add(new() { RecipeId = "fishing", Activity = RpgActivity.Fishing, RemainingUses = 2, Attributes = new() { FishingLuck = 5 } }); });
        var status = await Chat("!sushi");
        Assert.Contains("Angler-Sushi", status); Assert.Contains("+5 Angelglück", status); Assert.Contains("noch 2 gültige Angelaktionen", status);
    }

    [Fact]
    public async Task PerkOverviewAndLongInventoryStayOnOneCompactPage()
    {
        var perks = await Chat("!perks");
        Assert.Contains("Weiter: !perks 2", perks); Assert.True(perks.Length <= 480);
        await Seed(a => { for (var i = 0; i < 30; i++) a.Rpg.Inventory.Add(new() { DisplayName = new string('X', 100), InstanceId = "item" + i }); });
        var response = await Chat("!inventory 2");
        Assert.Contains("Inventar 2/", response); Assert.True(response.Length <= 480);
        Assert.DoesNotContain('\n', response);
    }

    [Fact]
    public async Task BossAttackShowsCritInOneMessageWithoutChangingBalance()
    {
        await Seed(a => { a.Points = 1000; a.Rpg.Inventory.Add(new() { InstanceId = "ring", Attributes = new() { CritChance = 5 } });
            a.Rpg.Equipment[EquipmentSlot.Ring1] = "ring"; });
        var config = new MinigameConfig { Enabled = true, PointsEnabled = true };
        config.Adventures.Rpg = _config;
        var messages = new List<string>();
        var service = new AdventureGameService(_points, config, (text, _) => { messages.Add(text); return Task.CompletedTask; }, new Lowest(), broadcasterId: "b");
        await service.HandleAsync(new() { UserId = "b", IsBroadcaster = true }, "!start boss", new[] { "!start boss" }, Token);
        messages.Clear();
        await service.ExecuteBossAttackAsync("42", "Funjii", "attack", Token);
        Assert.Contains("Kritischer Treffer!", Assert.Single(messages));
        Assert.Equal(1000, await _points.GetPointsAsync("42", Token));
    }

    [Fact]
    public async Task FishCollectionUsesActualFishNamesAndExcludesZeroStocks()
    {
        await Seed(a => { a.Rpg.Fish["sardine"] = new() { FishId = "sardine", Quantity = 3 };
            a.Rpg.Fish["perch"] = new() { FishId = "perch", Quantity = 0 }; });
        var config = new MinigameConfig { Enabled = true, PointsEnabled = true }; config.Adventures.Rpg = _config;
        var messages = new List<string>();
        var service = new AdventureGameService(_points, config, (text, _) => { messages.Add(text); return Task.CompletedTask; });
        await service.HandleAsync(new() { UserId = "42", UserName = "Funjii" }, "!sammlung", new[] { "!sammlung" }, Token);
        Assert.Contains("Kleine Sardine: 3×", Assert.Single(messages));
        Assert.DoesNotContain("0×", messages[0]);
    }

    [Fact]
    public void FishingTextDoesNotMislabelOrdinaryActualCatchAsLegendary()
    {
        var result = new RpgAdventureResult(true, "42", 100, 800, 2000, "", "Ein legendärer Fisch beißt an!",
            new() { DisplayName = "Kleine Sardine", Rarity = ItemRarity.Common }, 0, Array.Empty<string>());
        var text = RpgChatPresentation.AdventureOutcome(result, RpgActivity.Fishing);
        Assert.Contains("1× Kleine Sardine (Common)", text); Assert.DoesNotContain("legendär", text);
    }
}
