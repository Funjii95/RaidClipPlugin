using System.Collections.Concurrent;

namespace RaidClipPlugin.Services;

public sealed class SongRequestPermits
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _expires = new(StringComparer.Ordinal);
    public void Grant(string userId, DateTimeOffset now) => _expires[userId] = now.AddMinutes(5);
    public bool IsAllowed(string userId, DateTimeOffset now)
    {
        if (!_expires.TryGetValue(userId, out var expiry)) return false;
        if (now < expiry) return true;
        _expires.TryRemove(userId, out _);
        return false;
    }
}
