using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class CustomCommandImportTests
{
    private readonly CustomCommandImportService _service = new();

    [Fact]
    public void JsonImportRecognizesFlexibleStreamElementsFields()
    {
        const string json = """
            { "commands": [
              { "trigger": "hello", "reply": "Hallo @{user}!", "active": true,
                "cooldown": 20, "userlevel": "moderator", "aliases": ["hi"] }
            ] }
            """;
        var item = Assert.Single(_service.Parse(json, "streamelements.json"));
        Assert.Equal("!hello", item.Command.Command);
        Assert.Equal("Hallo @{user}!", item.Command.Response);
        Assert.Equal("!hi", Assert.Single(item.Command.Aliases));
        Assert.Equal("Moderator", item.Command.RequiredRole);
        Assert.Equal(20, item.Command.UserCooldownSeconds);
    }

    [Fact]
    public void CsvImportSupportsQuotedResponses()
    {
        const string csv = """
            command,response,enabled,cooldown,permission
            hello,"Hallo, Chat!",true,12,vip
            """;
        var item = Assert.Single(_service.Parse(csv, "commands.csv"));
        Assert.Equal("!hello", item.Command.Command);
        Assert.Equal("Hallo, Chat!", item.Command.Response);
        Assert.Equal("Vip", item.Command.RequiredRole);
    }

    [Fact]
    public void PasteImportSupportsArrowSyntax()
    {
        var item = Assert.Single(_service.Parse(
            "!raid => Die Otter-Familie ist da!", "Paste-Import"));
        Assert.Equal("!raid", item.Command.Command);
        Assert.Equal("Die Otter-Familie ist da!", item.Command.Response);
    }

    [Fact]
    public void LongMultilineResponsesStayCompleteButPreviewIsBounded()
    {
        var response = "Zeile eins" + Environment.NewLine +
            "Zeile zwei" + (char)9 + new string('x', 400);
        var json = JsonSerializer.Serialize(new[]
        {
            new { command = "!lang", response }
        });
        var item = Assert.Single(_service.Parse(json, "long.json"));
        Assert.Equal(response.Trim(), item.Command.Response);
        var preview = MainForm.PreviewText(item.Command.Response, 180);
        Assert.True(preview.Length <= 180);
        Assert.DoesNotContain((char)13, preview);
        Assert.DoesNotContain((char)10, preview);
        Assert.DoesNotContain((char)9, preview);
    }

    [Fact]
    public void MoreThanOneHundredCommandsCanBeAnalyzed()
    {
        var text = string.Join(Environment.NewLine, Enumerable.Range(1, 150)
            .Select(index => $"!import{index} => Antwort {index}"));
        var config = new AppConfig();
        var analyzed = _service.Analyze(_service.Parse(text, "bulk.txt"), config);
        Assert.Equal(150, analyzed.Count);
        Assert.All(analyzed, item =>
            Assert.Equal(CustomCommandConflictType.None, item.ConflictType));
    }

    [Fact]
    public void DuplicateCommandsInsideImportAreDetectedCaseInsensitive()
    {
        var items = _service.Parse("punkte => Eins" + Environment.NewLine +
            "!PUNKTE => Zwei", "Paste");
        var analyzed = _service.Analyze(items, new AppConfig());
        Assert.Contains(analyzed, item =>
            item.ConflictType == CustomCommandConflictType.DuplicateInImport);
    }

    [Fact]
    public void BuiltInCommandsAndAliasesCannotBeOverwritten()
    {
        const string json = """
            [{"command":"!neu","response":"Test","aliases":["!gamble"]}]
            """;
        var config = new AppConfig();
        var item = Assert.Single(_service.Analyze(
            _service.Parse(json, "commands.json"), config));
        Assert.Equal(CustomCommandConflictType.BuiltIn, item.ConflictType);
        item.Action = CustomCommandImportAction.Overwrite;
        var result = _service.Apply(new[] { item }, config);
        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void ExistingCustomCommandCanBeExplicitlyOverwritten()
    {
        var config = new AppConfig();
        config.Commands.CustomCommands = new List<CustomChatCommandConfig>
        {
            new() { Id = "existing", Enabled = true, Command = "!hello", Response = "Alt" }
        };
        var item = Assert.Single(_service.Analyze(_service.Parse(
            "!hello => Neu", "Paste"), config));
        Assert.True(item.CanOverwrite);
        item.Action = CustomCommandImportAction.Overwrite;
        var result = _service.Apply(new[] { item }, config);
        var command = Assert.Single(result.Commands);
        Assert.Equal("existing", command.Id);
        Assert.Equal("Neu", command.Response);
    }

    [Fact]
    public void BuiltInConflictCanBeRenamedSafely()
    {
        var config = new AppConfig();
        var item = Assert.Single(_service.Analyze(_service.Parse(
            "!gamble => Eigene Antwort", "Paste"), config));
        item.Action = CustomCommandImportAction.Rename;
        var result = _service.Apply(new[] { item }, config);
        var imported = Assert.Single(result.Commands, command =>
            command.Response == "Eigene Antwort");
        Assert.False(imported.Command.Equals("!gamble",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImportedCommandsSurviveConfigSerialization()
    {
        var config = new AppConfig();
        var item = Assert.Single(_service.Analyze(_service.Parse(
            "!otter => Viel Liebe!", "Paste"), config));
        var result = _service.Apply(new[] { item }, config);
        config.Commands.CustomCommands = result.Commands.ToList();
        var restored = JsonSerializer.Deserialize<AppConfig>(
            JsonSerializer.Serialize(config));
        Assert.Contains(restored!.Commands.CustomCommands,
            command => command.Command == "!otter" && command.Response == "Viel Liebe!");
    }
}
