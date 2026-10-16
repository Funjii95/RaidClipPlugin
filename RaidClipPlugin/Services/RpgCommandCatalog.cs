namespace RaidClipPlugin.Services;

public sealed record RpgCommandDefinition(string Command, IReadOnlyList<string> Aliases, string Usage);

public static class RpgCommandCatalog
{
    public static IReadOnlyList<RpgCommandDefinition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new RpgCommandDefinition("!inventory", new[] { "!inventar", "!inv" }, "[Seite]"),
        new("!item", Array.Empty<string>(), "<Nummer|Item-ID> [Seite]"),
        new("!rpgtext", Array.Empty<string>(), "[Seite]"),
        new("!equipment", new[] { "!ausrüstung", "!gear" }, "[Seite]"),
        new("!equip", Array.Empty<string>(), "<Nummer|Item-ID> [Slot]"),
        new("!unequip", Array.Empty<string>(), "<Slot>"),
        new("!sets", Array.Empty<string>(), "[Seite]"),
        new("!salvage", Array.Empty<string>(), "<Nummer|duplicates|all common> [confirm]"),
        new("!itemlock", Array.Empty<string>(), "<Nummer> [on|off]"),
        new("!favorite", Array.Empty<string>(), "<Nummer> [on|off]"),
        new("!perks", Array.Empty<string>(), "[Seite]"),
        new("!perk", new[] { "!buyperk" }, "<ID>"),
        new("!sushi", Array.Empty<string>(), "[Rezept|status <Seite>]"),
        new("!craft", Array.Empty<string>(), "<Rezept>"),
        new("!stats", Array.Empty<string>(), ""),
        new("!rpg", Array.Empty<string>(), "[hilfe]"),
    });
    public static RpgCommandDefinition? Find(string command) => Definitions.FirstOrDefault(d =>
        d.Command.Equals(command, StringComparison.OrdinalIgnoreCase) || d.Aliases.Contains(command, StringComparer.OrdinalIgnoreCase));
}
