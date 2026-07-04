using System.Globalization;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public enum CustomCommandImportFormat { Auto, Json, Csv, Text }
public enum CustomCommandConflictType
{
    None, Invalid, DuplicateInImport, ExistingCustom, BuiltIn
}
public enum CustomCommandImportAction { Import, Skip, Overwrite, Rename }

public sealed class CustomCommandImportCandidate
{
    public required CustomChatCommandConfig Command { get; init; }
    public required string Source { get; init; }
    public int RowNumber { get; init; }
    public CustomCommandConflictType ConflictType { get; set; }
    public string ConflictMessage { get; set; } = "";
    public string? ExistingCustomId { get; set; }
    public bool CanOverwrite { get; set; }
    public CustomCommandImportAction Action { get; set; } =
        CustomCommandImportAction.Import;
    public string ImportStatus => ConflictType switch
    {
        CustomCommandConflictType.None => "Bereit",
        CustomCommandConflictType.Invalid => "Ungültig",
        _ => "Konflikt"
    };
}

public sealed record CustomCommandImportResult(
    IReadOnlyList<CustomChatCommandConfig> Commands,
    int Recognized, int Imported, int Skipped, int Conflicts, int Invalid);

public sealed class CustomCommandImportService
{
    private static readonly string[] CommandNames = { "command", "name", "trigger", "cmd" };
    private static readonly string[] ResponseNames = { "response", "reply", "message", "output", "text" };

    public IReadOnlyList<CustomCommandImportCandidate> Parse(string content,
        string source, CustomCommandImportFormat format = CustomCommandImportFormat.Auto)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Die Importquelle ist leer.");
        format = DetectFormat(content, source, format);
        return format switch
        {
            CustomCommandImportFormat.Json => ParseJson(content, source),
            CustomCommandImportFormat.Csv => ParseCsv(content, source),
            _ => ParseText(content, source)
        };
    }

    public IReadOnlyList<CustomCommandImportCandidate> Analyze(
        IEnumerable<CustomCommandImportCandidate> candidates, AppConfig config)
    {
        var items = candidates.ToList();
        var registry = new CommandRegistry();
        registry.Update(config);
        var existing = new Dictionary<string, List<ChatCommandDefinition>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var definition in registry.Commands)
        foreach (var text in new[] { definition.CommandText }.Concat(definition.Aliases))
        {
            var normalized = CommandRegistry.Normalize(text);
            if (!existing.TryGetValue(normalized, out var owners))
                existing[normalized] = owners = new List<ChatCommandDefinition>();
            owners.Add(definition);
        }

        var importedTokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            item.ConflictType = CustomCommandConflictType.None;
            item.ConflictMessage = "";
            item.ExistingCustomId = null;
            item.CanOverwrite = false;
            Normalize(item.Command);
            var validation = Validate(item.Command);
            if (validation is not null)
            {
                SetConflict(item, CustomCommandConflictType.Invalid, validation);
                continue;
            }

            var localConflicts = new List<string>();
            var builtInConflicts = new List<string>();
            var customOwners = new List<ChatCommandDefinition>();
            foreach (var token in Tokens(item.Command))
            {
                if (importedTokens.TryGetValue(token, out var previous))
                    localConflicts.Add($"{token} ist bereits in Importzeile {previous} enthalten.");
                else
                    importedTokens[token] = item.RowNumber;

                if (!existing.TryGetValue(token, out var owners)) continue;
                foreach (var owner in owners)
                {
                    if (owner.ModuleId.Equals("custom", StringComparison.OrdinalIgnoreCase))
                        customOwners.Add(owner);
                    else
                        builtInConflicts.Add($"{token} gehört zu {owner.ModuleDisplayName} ({owner.CommandText}).");
                }
            }

            if (localConflicts.Count > 0)
                SetConflict(item, CustomCommandConflictType.DuplicateInImport,
                    string.Join(" ", localConflicts.Distinct()));
            else if (builtInConflicts.Count > 0)
                SetConflict(item, CustomCommandConflictType.BuiltIn,
                    "Eingebauter Command: " + string.Join(" ", builtInConflicts.Distinct()));
            else if (customOwners.Count > 0)
            {
                var ids = customOwners.Select(owner => owner.CommandId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                item.CanOverwrite = ids.Length == 1 && ids[0].StartsWith("custom.",
                    StringComparison.OrdinalIgnoreCase);
                item.ExistingCustomId = item.CanOverwrite
                    ? ids[0]["custom.".Length..] : null;
                SetConflict(item, CustomCommandConflictType.ExistingCustom,
                    item.CanOverwrite
                        ? "Ein vorhandener Custom Command oder Alias verwendet diesen Namen."
                        : "Mehrere vorhandene Custom Commands verwenden diesen Namen.");
            }
            else if (item.Action is CustomCommandImportAction.Skip or
                     CustomCommandImportAction.Overwrite or CustomCommandImportAction.Rename)
                item.Action = CustomCommandImportAction.Import;
        }
        return items;
    }

    public CustomCommandImportResult Apply(
        IEnumerable<CustomCommandImportCandidate> candidates, AppConfig config,
        bool onlyConflictFree = false)
    {
        var items = Analyze(candidates, config).ToList();
        var commands = config.Commands.CustomCommands.Select(Clone).ToList();
        var builtIns = new CommandRegistry();
        var withoutCustom = CloneConfigForRegistry(config);
        withoutCustom.Commands.CustomCommands = new List<CustomChatCommandConfig>();
        builtIns.Update(withoutCustom);
        var used = builtIns.Commands.SelectMany(definition =>
                new[] { definition.CommandText }.Concat(definition.Aliases))
            .Select(CommandRegistry.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands)
            foreach (var token in Tokens(command)) used.Add(token);

        var imported = 0;
        var skipped = 0;
        foreach (var item in items)
        {
            var action = onlyConflictFree
                ? item.ConflictType == CustomCommandConflictType.None
                    ? CustomCommandImportAction.Import : CustomCommandImportAction.Skip
                : item.Action;
            if (item.ConflictType == CustomCommandConflictType.Invalid ||
                action == CustomCommandImportAction.Skip)
            {
                skipped++;
                continue;
            }

            var command = Clone(item.Command);
            command.Id = Guid.NewGuid().ToString("N");
            if (action == CustomCommandImportAction.Overwrite)
            {
                if (!item.CanOverwrite || string.IsNullOrWhiteSpace(item.ExistingCustomId))
                {
                    skipped++;
                    continue;
                }
                var existing = commands.FirstOrDefault(value => value.Id.Equals(
                    item.ExistingCustomId, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    skipped++;
                    continue;
                }
                foreach (var token in Tokens(existing)) used.Remove(token);
                command.Id = existing.Id;
                commands.Remove(existing);
            }
            else if (action == CustomCommandImportAction.Rename)
            {
                var renameUsed = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
                command.Command = MakeUnique(command.Command, renameUsed);
                renameUsed.Add(command.Command);
                var aliases = new List<string>();
                foreach (var alias in command.Aliases)
                {
                    var renamed = MakeUnique(alias, renameUsed);
                    renameUsed.Add(renamed);
                    aliases.Add(renamed);
                }
                command.Aliases = aliases;
            }
            else if (item.ConflictType != CustomCommandConflictType.None)
            {
                skipped++;
                continue;
            }

            if (Tokens(command).Any(used.Contains))
            {
                skipped++;
                continue;
            }
            commands.Add(command);
            foreach (var token in Tokens(command)) used.Add(token);
            imported++;
        }

        return new CustomCommandImportResult(commands, items.Count, imported, skipped,
            items.Count(item => item.ConflictType is not CustomCommandConflictType.None and
                not CustomCommandConflictType.Invalid),
            items.Count(item => item.ConflictType == CustomCommandConflictType.Invalid));
    }

    private static IReadOnlyList<CustomCommandImportCandidate> ParseJson(
        string content, string source)
    {
        using var document = JsonDocument.Parse(content);
        var result = new List<CustomCommandImportCandidate>();
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            AddJsonArray(root, source, result);
        else if (root.ValueKind == JsonValueKind.Object)
        {
            var collection = FindProperty(root, "commands", "items", "data", "customCommands");
            if (collection is { ValueKind: JsonValueKind.Array })
                AddJsonArray(collection.Value, source, result);
            else if (HasAnyProperty(root, CommandNames))
                result.Add(FromJson(root, source, 1));
            else
            {
                var row = 0;
                foreach (var property in root.EnumerateObject())
                {
                    row++;
                    if (property.Value.ValueKind == JsonValueKind.String)
                        result.Add(Create(property.Name, property.Value.GetString(), null,
                            true, 15, 3, "Viewer", source, row));
                    else if (property.Value.ValueKind == JsonValueKind.Object)
                        result.Add(FromJson(property.Value, source, row, property.Name));
                }
            }
        }
        if (result.Count == 0)
            throw new InvalidOperationException("Die JSON-Datei enthält keine erkennbaren Commands.");
        return result;
    }

    private static void AddJsonArray(JsonElement array, string source,
        List<CustomCommandImportCandidate> result)
    {
        var row = 0;
        foreach (var element in array.EnumerateArray())
        {
            row++;
            if (element.ValueKind == JsonValueKind.Object)
                result.Add(FromJson(element, source, row));
        }
    }

    private static CustomCommandImportCandidate FromJson(JsonElement element,
        string source, int row, string? fallbackCommand = null)
    {
        var command = GetString(element, CommandNames) ?? fallbackCommand;
        var response = GetString(element, ResponseNames);
        var enabled = GetBool(element, true, "enabled", "active", "isEnabled");
        var userCooldown = GetInt(element, 15, "cooldown", "userCooldown",
            "user_cooldown", "cooldownSeconds");
        var globalCooldown = GetInt(element, 3, "globalCooldown",
            "global_cooldown", "globalCooldownSeconds");
        var role = GetString(element, "userlevel", "userLevel", "permission",
            "role", "level");
        var aliases = ReadAliases(element);
        return Create(command, response, aliases, enabled, userCooldown,
            globalCooldown, role, source, row);
    }

    private static IReadOnlyList<CustomCommandImportCandidate> ParseCsv(
        string content, string source)
    {
        var firstLine = content.Split(new[] { (char)13, (char)10 },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var delimiter = new[] { ',', ';', (char)9 }
            .OrderByDescending(value => firstLine.Count(character => character == value))
            .First();
        var rows = ParseDelimited(content, delimiter);
        if (rows.Count < 2)
            throw new InvalidOperationException("Die CSV-Datei enthält keine Datenzeilen.");
        var headers = rows[0].Select(value => value.Trim()).ToArray();
        if (!headers.Any(header => CommandNames.Contains(header,
                StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("In der CSV-Datei fehlt eine Command-Spalte.");
        var result = new List<CustomCommandImportCandidate>();
        for (var index = 1; index < rows.Count; index++)
        {
            if (rows[index].All(string.IsNullOrWhiteSpace)) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < headers.Length; column++)
                values[headers[column]] = column < rows[index].Count
                    ? rows[index][column] : "";
            result.Add(FromValues(values, source, index + 1));
        }
        return result;
    }

    private static IReadOnlyList<CustomCommandImportCandidate> ParseText(
        string content, string source)
    {
        var lines = content.Split(new[] { (char)13, (char)10 },
            StringSplitOptions.None);
        var result = new List<CustomCommandImportCandidate>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            string[] parts;
            if (line.Contains("=>", StringComparison.Ordinal))
                parts = line.Split(new[] { "=>" }, 2, StringSplitOptions.TrimEntries);
            else if (line.Contains((char)9))
                parts = line.Split(new[] { (char)9 }, 2, StringSplitOptions.TrimEntries);
            else if (line.Contains('|'))
                parts = line.Split(new[] { '|' }, 2, StringSplitOptions.TrimEntries);
            else if (line.Contains(';'))
                parts = line.Split(new[] { ';' }, 2, StringSplitOptions.TrimEntries);
            else
                parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            result.Add(Create(parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1),
                null, true, 15, 3, "Viewer", source, index + 1));
        }
        if (result.Count == 0)
            throw new InvalidOperationException("Der eingefügte Text enthält keine Commands.");
        return result;
    }

    private static CustomCommandImportCandidate FromValues(
        IReadOnlyDictionary<string, string> values, string source, int row)
    {
        string? Get(params string[] names) => names.Select(name =>
            values.TryGetValue(name, out var value) ? value : null)
            .FirstOrDefault(value => value is not null);
        var aliases = SplitAliases(Get("aliases", "alias", "alternative"));
        return Create(Get(CommandNames), Get(ResponseNames), aliases,
            ParseBool(Get("enabled", "active", "isEnabled"), true),
            ParseInt(Get("cooldown", "userCooldown", "user_cooldown"), 15),
            ParseInt(Get("globalCooldown", "global_cooldown"), 3),
            Get("userlevel", "userLevel", "permission", "role", "level"),
            source, row);
    }

    private static CustomCommandImportCandidate Create(string? command,
        string? response, IEnumerable<string>? aliases, bool enabled,
        int userCooldown, int globalCooldown, string? role, string source, int row)
    {
        var config = new CustomChatCommandConfig
        {
            Enabled = enabled,
            Command = command ?? "",
            Aliases = aliases?.ToList() ?? new List<string>(),
            Response = response?.Trim() ?? "",
            RequiredRole = ParseRole(role).ToString(),
            UserCooldownSeconds = Math.Max(0, userCooldown),
            GlobalCooldownSeconds = Math.Max(0, globalCooldown)
        };
        Normalize(config);
        return new CustomCommandImportCandidate
        {
            Command = config,
            Source = source,
            RowNumber = row
        };
    }

    private static void Normalize(CustomChatCommandConfig command)
    {
        command.Command = CommandRegistry.Normalize(command.Command);
        command.Aliases = command.Aliases.Select(CommandRegistry.Normalize)
            .Where(value => value.Length > 1)
            .Where(value => !value.Equals(command.Command,
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        command.Response = (command.Response ?? "").Trim();
        command.RequiredRole = ParseRole(command.RequiredRole).ToString();
        command.UserCooldownSeconds = Math.Max(0, command.UserCooldownSeconds);
        command.GlobalCooldownSeconds = Math.Max(0, command.GlobalCooldownSeconds);
    }

    private static string? Validate(CustomChatCommandConfig command)
    {
        if (command.Command.Length <= 1) return "Command darf nicht leer sein.";
        if (command.Command.Any(char.IsWhiteSpace))
            return "Command darf keine Leerzeichen enthalten.";
        if (command.Command.Length > 80)
            return "Command darf höchstens 80 Zeichen lang sein.";
        if (command.Response.Length == 0) return "Antwort darf nicht leer sein.";
        if (command.Response.Length > 480)
            return "Antwort darf höchstens 480 Zeichen lang sein.";
        if (command.Aliases.Any(alias => alias.Any(char.IsWhiteSpace)))
            return "Aliase dürfen keine Leerzeichen enthalten.";
        if (command.Aliases.Any(alias => alias.Length > 80))
            return "Aliase dürfen höchstens 80 Zeichen lang sein.";
        return null;
    }

    private static IEnumerable<string> Tokens(CustomChatCommandConfig command) =>
        new[] { command.Command }.Concat(command.Aliases)
            .Select(CommandRegistry.Normalize).Where(value => value.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static void SetConflict(CustomCommandImportCandidate item,
        CustomCommandConflictType type, string message)
    {
        item.ConflictType = type;
        item.ConflictMessage = message;
        if (item.Action == CustomCommandImportAction.Import ||
            type == CustomCommandConflictType.Invalid)
            item.Action = CustomCommandImportAction.Skip;
    }

    private static string MakeUnique(string command, HashSet<string> used)
    {
        var normalized = CommandRegistry.Normalize(command);
        if (!used.Contains(normalized)) return normalized;
        var stem = normalized.TrimStart('!');
        for (var suffix = 2; suffix < 10000; suffix++)
        {
            var candidate = $"!{stem}{suffix}";
            if (!used.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException($"Für {normalized} konnte kein freier Name erzeugt werden.");
    }

    private static CustomChatCommandConfig Clone(CustomChatCommandConfig value) => new()
    {
        Id = value.Id,
        Enabled = value.Enabled,
        Command = value.Command,
        Aliases = value.Aliases.ToList(),
        Response = value.Response,
        RequiredRole = value.RequiredRole,
        UserCooldownSeconds = value.UserCooldownSeconds,
        GlobalCooldownSeconds = value.GlobalCooldownSeconds
    };

    private static AppConfig CloneConfigForRegistry(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private static CustomCommandImportFormat DetectFormat(string content,
        string source, CustomCommandImportFormat requested)
    {
        if (requested != CustomCommandImportFormat.Auto) return requested;
        if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            content.TrimStart().StartsWith('[') || content.TrimStart().StartsWith('{'))
            return CustomCommandImportFormat.Json;
        if (source.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return CustomCommandImportFormat.Csv;
        var first = content.Split(new[] { (char)13, (char)10 },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return CommandNames.Any(name => first.Contains(name,
            StringComparison.OrdinalIgnoreCase)) && first.IndexOfAny(new[] { ',', ';', (char)9 }) >= 0
            ? CustomCommandImportFormat.Csv : CustomCommandImportFormat.Text;
    }

    private static List<List<string>> ParseDelimited(string content, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == delimiter && !quoted)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((character == (char)13 || character == (char)10) && !quoted)
            {
                if (character == (char)13 && index + 1 < content.Length &&
                    content[index + 1] == (char)10) index++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else field.Append(character);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    private static JsonElement? FindProperty(JsonElement element,
        params string[] names)
    {
        foreach (var property in element.EnumerateObject())
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                return property.Value;
        return null;
    }

    private static bool HasAnyProperty(JsonElement element,
        IEnumerable<string> names) => element.EnumerateObject().Any(property =>
        names.Contains(property.Name, StringComparer.OrdinalIgnoreCase));

    private static string? GetString(JsonElement element, params string[] names)
    {
        var value = FindProperty(element, names);
        if (value is null) return null;
        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool GetBool(JsonElement element, bool fallback,
        params string[] names) => ParseBool(GetString(element, names), fallback);

    private static int GetInt(JsonElement element, int fallback,
        params string[] names) => ParseInt(GetString(element, names), fallback);

    private static IReadOnlyList<string> ReadAliases(JsonElement element)
    {
        var value = FindProperty(element, "aliases", "alias", "alternative");
        if (value is null) return Array.Empty<string>();
        if (value.Value.ValueKind == JsonValueKind.Array)
            return value.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? "").ToArray();
        return SplitAliases(value.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() : null);
    }

    private static IReadOnlyList<string> SplitAliases(string? value) =>
        (value ?? "").Split(new[] { ',', ';', '|', ' ' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool ParseBool(string? value, bool fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback :
        value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "ja" or "active" or "enabled" => true,
            "false" or "0" or "no" or "nein" or "inactive" or "disabled" => false,
            _ => fallback
        };

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var parsed) ? parsed : fallback;

    private static CommandRole ParseRole(string? value) =>
        (value ?? "").Trim().ToLowerInvariant() switch
        {
            "follower" or "followers" => CommandRole.Follower,
            "subscriber" or "sub" or "subs" or "regular" => CommandRole.Subscriber,
            "vip" => CommandRole.Vip,
            "moderator" or "mod" or "mods" => CommandRole.Moderator,
            "broadcaster" or "owner" or "streamer" => CommandRole.Broadcaster,
            _ => CommandRole.Viewer
        };
}
