namespace RaidClipPlugin.Services;

public sealed record ChatCommandParseResult(
    bool IsCommand,
    string Prefix,
    string Command,
    string Arguments,
    string NormalizedText,
    string IgnoreReason);

public static class ChatCommandParser
{
    public static ChatCommandParseResult Parse(
        string? text,
        string prefix = "!")
    {
        var normalized = (text ?? "").Trim();
        var effectivePrefix = string.IsNullOrWhiteSpace(prefix)
            ? "!"
            : prefix.Trim();

        if (normalized.Length == 0)
        {
            return new(false, effectivePrefix, "", "", normalized,
                "Nachricht ist leer.");
        }

        if (!normalized.StartsWith(
                effectivePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(false, effectivePrefix, "", "", normalized,
                "Command-Prefix fehlt.");
        }

        var remainder = normalized[effectivePrefix.Length..].TrimStart();
        if (remainder.Length == 0)
        {
            return new(false, effectivePrefix, "", "", normalized,
                "Commandname fehlt.");
        }

        var parts = remainder.Split(' ', 2,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return new(false, effectivePrefix, "", "", normalized,
                "Commandname fehlt.");
        }

        return new(
            true,
            effectivePrefix,
            parts[0].ToLowerInvariant(),
            parts.Length > 1 ? parts[1] : "",
            normalized,
            "");
    }
}
