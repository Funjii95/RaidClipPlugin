using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgCommandRoutingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaidClipPerkCommands-" + Guid.NewGuid().ToString("N"));
    private readonly MinigameConfig _config = new() { Enabled = true, PointsEnabled = true };
    private readonly ViewerPointStore _points;
    public RpgCommandRoutingTests() { _points = new(_directory); _config.Adventures.Rpg.Enabled = true; }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private Task<string> Chat(string text, string? id = null, CommandAuthorization authorization = CommandAuthorization.Default)
    {
        var parsed = ChatMinigameService.ParseMinigameCommand(text);
        return new RpgChatCommand(_points, _config.Adventures.Rpg).ExecuteAsync(new() { UserId = "42", UserName = "Otter", Id = id ?? Guid.NewGuid().ToString("N"),
            CommandAuthorization = authorization }, parsed.Command, parsed.Parts, default);
    }

    [Theory]
    [InlineData("!perk damage")]
    [InlineData("!buyperk damage")]
    [InlineData("!BUYPERK damage")]
    public async Task PrimaryAndAliasUseSamePurchaseAndProgressiveNextCosts(string text)
    {
        await _points.AddPointsAsync("42", "Otter", 10000, 0, default);
        var first = await Chat(text, "purchase1");
        Assert.Contains("Schaden auf Level 1", first); Assert.Contains("+2 % Schaden", first);
        Assert.Contains("Kosten: 500 Perlen", first); Assert.Contains("Stand: 9.500 Perlen", first);
        Assert.Contains("Nächstes Upgrade: 1.000 Perlen", first);
        var second = await Chat("!perk damage", "purchase2");
        Assert.Contains("Level 2", second); Assert.Contains("+4 % Schaden", second);
        Assert.Contains("Stand: 8.500 Perlen", second); Assert.Contains("Nächstes Upgrade: 2.000 Perlen", second);
        var account = (await _points.GetProfileAsync("42", default)).Entry;
        Assert.Equal(8500, account.Points); Assert.Equal(2, account.Rpg.Perks["damage"]);
        var duplicate = await Chat("!buyperk damage", "purchase2");
        Assert.Contains("bereits verarbeitet", duplicate); Assert.Equal(8500, await _points.GetPointsAsync("42", default));
    }

    [Fact]
    public async Task AliasRespectsExistingCustomPricesInsteadOfApplyingNewDefaults()
    {
        var perk = _config.Adventures.Rpg.Perks.Single(p => p.Id == "damage");
        perk.BaseCost = 250; perk.CostGrowth = 3;
        await _points.AddPointsAsync("42", "Otter", 10000, 0, default);
        var response = await Chat("!buyperk damage");
        Assert.Contains("Kosten: 250 Perlen", response); Assert.Contains("Nächstes Upgrade: 750 Perlen", response);
        Assert.Equal(9750, await _points.GetPointsAsync("42", default));
        Assert.Equal(250, perk.BaseCost); Assert.Equal(3, perk.CostGrowth);
    }

    [Fact]
    public void EveryRecognizedCommandAndAliasIsRegisteredExactlyOnceWithSameEnablement()
    {
        var app = new AppConfig { Minigame = _config };
        var registry = new CommandRegistry(); registry.Update(app);
        foreach (var definition in RpgCommandCatalog.Definitions)
        {
            var entry = Assert.Single(registry.Commands, d => d.CommandId == "rpg." + definition.Command[1..]);
            Assert.Equal(definition.Command, entry.CommandText); Assert.True(entry.Enabled);
            foreach (var command in new[] { definition.Command }.Concat(definition.Aliases))
            {
                Assert.True(RpgChatCommand.Recognizes(command)); Assert.True(AdventureGameService.Recognizes(command)); Assert.True(ChatMinigameService.IsGameCommand(command));
                Assert.True(registry.IsCommandEnabledForMessage(command + " damage"));
            }
        }
        var perk = Assert.Single(registry.Commands, d => d.CommandId == "rpg.perk");
        Assert.Contains("!buyperk", perk.Aliases);
        Assert.DoesNotContain(registry.FindCollisions(false), c => c.Command == "!buyperk");
        app.Commands.CommandEnabledOverrides["rpg.perk"] = false; registry.Update(app);
        Assert.False(registry.IsCommandEnabledForMessage("!perk damage"));
        Assert.False(registry.IsCommandEnabledForMessage("!buyperk damage"));
        app.Commands.CommandEnabledOverrides.Clear(); app.Minigame.Adventures.Rpg.Enabled = false; registry.Update(app);
        Assert.All(registry.Commands.Where(c => c.CommandId.StartsWith("rpg.")), c => Assert.False(c.Enabled));
    }

    [Theory]
    [InlineData("!perk")]
    [InlineData("!buyperk")]
    [InlineData("!perk damage extra")]
    [InlineData("!equip item invalidslot")]
    [InlineData("!inventar abc")]
    [InlineData("!perks -2")]
    [InlineData("!craft")]
    [InlineData("!sushi status abc")]
    [InlineData("!stats damage")]
    public async Task InvalidUsageProducesGuidanceWithoutSpending(string text)
    {
        await _points.AddPointsAsync("42", "Otter", 1000, 0, default);
        var response = await Chat(text);
        Assert.Contains("Nutzung:", response); Assert.True(response.Length <= 480);
        Assert.Equal(1000, await _points.GetPointsAsync("42", default));
        Assert.Empty((await _points.GetProfileAsync("42", default)).Entry.Rpg.Perks);
    }

    [Fact]
    public async Task UnknownPerkAndUnknownRpgSubcommandHaveSpecificHelp()
    {
        Assert.Contains("Verfügbare IDs: damage", await Chat("!perk nonexistent"));
        Assert.Contains("Unbekannter RPG-Unterbefehl", await Chat("!rpg nonexistent"));
        Assert.Contains("!perk <ID>", await Chat("!rpg help"));
        var direct = await new RpgChatCommand(_points, _config.Adventures.Rpg).ExecuteAsync(new() { UserId = "42", UserName = "Otter" }, "!not-a-command", new[] { "!not-a-command" }, default);
        Assert.Contains("Unbekannter RPG-Befehl", direct);
        Assert.False(RpgChatCommand.Recognizes("!unrelated-custom-command"));
    }

    [Fact]
    public async Task InsufficientFundsAndMaximumLevelCannotChangeAccount()
    {
        var insufficient = await Chat("!buyperk damage");
        Assert.Contains("Nicht genügend Perlen", insufficient); Assert.Contains("Kosten: 500", insufficient);
        await _points.AddPointsAsync("42", "Otter", 15500, 0, default);
        for (var i = 0; i < 5; i++)
        {
            var response = await Chat("!perk damage");
            if (i == 4) { Assert.Contains("Maximallevel erreicht", response); Assert.DoesNotContain("Nächstes Upgrade", response); }
        }
        var account = (await _points.GetProfileAsync("42", default)).Entry;
        Assert.Equal(0, account.Points); Assert.Equal(5, account.Rpg.Perks["damage"]);
        Assert.Contains("Maximallevel erreicht", await Chat("!buyperk damage"));
        Assert.Equal(0, await _points.GetPointsAsync("42", default));
    }

    [Fact]
    public async Task AliasUsesAdventureRoutingAndShowsCooldownInsteadOfGoingSilent()
    {
        await _points.AddPointsAsync("42", "Otter", 1000, 0, default);
        var messages = new List<string>();
        var service = new AdventureGameService(_points, _config, (message, _) => { messages.Add(message); return Task.CompletedTask; });
        await service.HandleAsync(new() { UserId = "42", UserName = "Otter", Id = "buy" }, "!buyperk", new[] { "!buyperk", "damage" }, default);
        Assert.Contains("Schaden auf Level 1", Assert.Single(messages));
        await service.HandleAsync(new() { UserId = "42", UserName = "Otter", Id = "second" }, "!perk", new[] { "!perk", "damage" }, default);
        Assert.Equal(2, messages.Count); Assert.Contains("warten", messages[^1]);
        Assert.Equal(500, await _points.GetPointsAsync("42", default));
    }

    [Fact]
    public async Task DeniedOrDisabledRpgCannotBePurchasedThroughAlias()
    {
        await _points.AddPointsAsync("42", "Otter", 1000, 0, default);
        Assert.Contains("nicht erlaubt", await Chat("!buyperk damage", authorization: CommandAuthorization.Denied));
        _config.Adventures.Rpg.Enabled = false;
        Assert.Contains("deaktiviert", await Chat("!buyperk damage"));
        Assert.Equal(1000, await _points.GetPointsAsync("42", default));
    }
}
