using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public sealed class ClipCooldownService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTimeOffset> _lastUsers =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _successfulUsers =
        new(StringComparer.Ordinal);
    private string _streamId = "";
    private DateTimeOffset _lastGlobal = DateTimeOffset.MinValue;
    private int _successfulTotal;

    public ClipCooldownDecision TryAccept(
        string streamId,
        string userId,
        DateTimeOffset now,
        ClipCommandConfig config)
    {
        lock (_sync)
        {
            EnsureStream(streamId);
            var normalizedUser = Normalize(userId);

            if (_successfulTotal >= Math.Max(1, config.MaximumClipsPerStream))
                return new(false, "stream-limit", 0);
            if (_successfulUsers.GetValueOrDefault(normalizedUser) >=
                Math.Max(1, config.MaximumClipsPerUserPerStream))
                return new(false, "user-limit", 0);

            if (_lastUsers.TryGetValue(normalizedUser, out var lastUser))
            {
                var duplicateRemaining = lastUser.AddSeconds(
                    Math.Max(1, config.DuplicateWindowSeconds)) - now;
                if (duplicateRemaining > TimeSpan.Zero)
                    return new(false, "duplicate",
                        RoundSeconds(duplicateRemaining));
            }

            var globalRemaining = _lastGlobal.AddSeconds(
                Math.Max(0, config.GlobalCooldownSeconds)) - now;
            if (globalRemaining > TimeSpan.Zero)
                return new(false, "global-cooldown",
                    RoundSeconds(globalRemaining));

            if (_lastUsers.TryGetValue(normalizedUser, out lastUser))
            {
                var userRemaining = lastUser.AddSeconds(
                    Math.Max(0, config.UserCooldownSeconds)) - now;
                if (userRemaining > TimeSpan.Zero)
                    return new(false, "user-cooldown",
                        RoundSeconds(userRemaining));
            }

            _lastGlobal = now;
            _lastUsers[normalizedUser] = now;
            return new(true, "", 0);
        }
    }

    public void RecordSuccess(string streamId, string userId)
    {
        lock (_sync)
        {
            EnsureStream(streamId);
            _successfulTotal++;
            var normalized = Normalize(userId);
            _successfulUsers[normalized] =
                _successfulUsers.GetValueOrDefault(normalized) + 1;
        }
    }

    public ClipStreamStatistics GetStatistics(string streamId)
    {
        lock (_sync)
        {
            EnsureStream(streamId);
            return new ClipStreamStatistics(
                _successfulTotal,
                new Dictionary<string, int>(_successfulUsers));
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _streamId = "";
            _lastGlobal = DateTimeOffset.MinValue;
            _lastUsers.Clear();
            _successfulUsers.Clear();
            _successfulTotal = 0;
        }
    }

    private void EnsureStream(string streamId)
    {
        if (_streamId.Equals(streamId, StringComparison.Ordinal)) return;
        _streamId = streamId;
        _lastGlobal = DateTimeOffset.MinValue;
        _lastUsers.Clear();
        _successfulUsers.Clear();
        _successfulTotal = 0;
    }

    private static string Normalize(string userId) =>
        (userId ?? "").Trim().ToLowerInvariant();

    private static int RoundSeconds(TimeSpan value) =>
        Math.Max(1, (int)Math.Ceiling(value.TotalSeconds));
}

public sealed record ClipCooldownDecision(
    bool Accepted,
    string Reason,
    int RemainingSeconds);

public sealed record ClipStreamStatistics(
    int TotalSuccessful,
    IReadOnlyDictionary<string, int> SuccessfulPerUser);
