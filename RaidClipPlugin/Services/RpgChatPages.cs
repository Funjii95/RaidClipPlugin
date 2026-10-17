using System.Runtime.CompilerServices;

namespace RaidClipPlugin.Services;

internal static class RpgChatPages
{
    internal static IEnumerable<string> Split(string value, int size = 300)
    {
        while (value.Length > size)
        {
            var length = char.IsHighSurrogate(value[size - 1]) ? size - 1 : size;
            yield return value[..length]; value = value[length..];
        }
        if (value.Length > 0) yield return value;
    }
    public static string Page(string user, string title, IEnumerable<string> entries, int page, string command, string empty)
    {
        var rows = entries.ToArray();
        if (rows.Length == 0) return $"{title} @{user}: {empty}";
        var groups = new List<List<string>>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row)) throw new InvalidDataException("Leerer formatierter RPG-Eintrag.");
            var part = 0;
            foreach (var text in Split(RpgChatPresentation.Clean(row), 296))
            {
                var entry = part++ == 0 ? text : "↳ " + text;
                if (groups.Count == 0 || groups[^1].Count == 3 || groups[^1].Sum(x => x.Length + 3) + entry.Length > 300) groups.Add(new());
                groups[^1].Add(entry);
            }
        }
        page = Math.Clamp(page, 1, groups.Count);
        return $"{title} {page}/{groups.Count}: @{user}, " + string.Join(" | ", groups[page - 1]) +
            (page < groups.Count ? $" | Weiter: {command} {page + 1}" : "");
    }
}

// Per store and verified user: retrieving a long reply never re-executes its purchase.
internal sealed class RpgReplyCache
{
    private static readonly ConditionalWeakTable<ViewerPointStore, RpgReplyCache> Stores = new();
    private readonly Dictionary<string, (string Text, DateTimeOffset Until)> _replies = new();
    private readonly object _sync = new();
    public static RpgReplyCache For(ViewerPointStore points) => Stores.GetValue(points, _ => new());
    public string Publish(string userId, string name, string text)
    {
        if (text.Length <= 480) return text;
        lock (_sync)
        {
            foreach (var key in _replies.Where(p => p.Value.Until <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) _replies.Remove(key);
            if (_replies.Count >= 256) _replies.Remove(_replies.MinBy(p => p.Value.Until).Key);
            _replies[userId] = (text, DateTimeOffset.UtcNow.AddMinutes(15));
        }
        return Read(userId, name, 1);
    }
    public string Read(string userId, string name, int page)
    {
        lock (_sync)
        {
            if (!_replies.TryGetValue(userId, out var reply) || reply.Until <= DateTimeOffset.UtcNow)
                return $"🦦 @{name}, keine längere Antwort gespeichert (15 Minuten verfügbar).";
            return RpgChatPages.Page(name, "🦦 RPG-Antwort", new[] { reply.Text }, page, "!rpgtext", "Keine Antwort.");
        }
    }
}

internal sealed class RpgChatLoadException(Exception inner) : Exception("RPG-Daten konnten nicht geladen werden.", inner);
internal sealed class RpgChatDataException(string message) : Exception(message);
