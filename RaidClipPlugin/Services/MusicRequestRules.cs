using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class MusicRequestRules
{
    public static bool ShouldAcceptRedemption(
        MusicRequestConfig config, MusicRequestRedemption redemption) =>
        config.Enabled &&
        redemption.RewardId.Equals(config.SelectedRewardId,
            StringComparison.Ordinal) &&
        redemption.Status.Equals("unfulfilled",
            StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(redemption.RedemptionId) &&
        !string.IsNullOrWhiteSpace(redemption.UserInput);

    public static string? ValidateTrack(
        MusicRequestConfig config,
        SpotifyTrack track,
        IEnumerable<string> openTrackIds)
    {
        if (track.Type != "track" || track.IsLocal || !track.IsPlayable ||
            string.IsNullOrWhiteSpace(track.Uri)) return "not-playable";
        if (track.DurationMs > TimeSpan.FromMinutes(
                config.MaximumTrackDurationMinutes).TotalMilliseconds)
            return "too-long";
        if (track.Explicit && !config.AllowExplicitTracks) return "explicit";
        if (config.TrackBlacklist.Contains(track.Id,
                StringComparer.OrdinalIgnoreCase) ||
            config.ArtistBlacklist.Any(artist =>
                track.Artist.Contains(artist,
                    StringComparison.OrdinalIgnoreCase)) ||
            config.SongTitleBlacklist.Any(title =>
                track.Name.Equals(title,
                    StringComparison.OrdinalIgnoreCase)) ||
            config.BlockedTitleTerms.Any(term =>
                track.Name.Contains(term,
                    StringComparison.OrdinalIgnoreCase)))
            return "blacklist";
        if (!config.AllowDuplicateTracks && openTrackIds.Contains(
                track.Id, StringComparer.Ordinal)) return "duplicate";
        return null;
    }

    public static bool IsUserBlacklisted(
        MusicRequestConfig config, string login) =>
        config.UserBlacklist.Contains(
            login.Trim().TrimStart('@'), StringComparer.OrdinalIgnoreCase);

    public static string? ValidateLimits(
        MusicRequestConfig config,
        IEnumerable<MusicRequestEntry> existing,
        string userId,
        DateTimeOffset now,
        out TimeSpan remainingCooldown)
    {
        remainingCooldown = TimeSpan.Zero;
        var entries = existing.ToArray();
        var open = entries.Where(IsOpen).ToArray();
        if (open.Length >= config.MaximumQueueLength) return "queue-full";
        if (open.Count(item => item.UserId.Equals(
                userId, StringComparison.Ordinal)) >=
            config.MaximumRequestsPerUser) return "user-limit";
        var last = entries.Where(item => item.UserId.Equals(
                    userId, StringComparison.Ordinal) &&
                item.Status is MusicRequestStatus.Queued or
                    MusicRequestStatus.Playing or MusicRequestStatus.Completed)
            .OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
        if (last is null) return null;
        remainingCooldown = TimeSpan.FromMinutes(config.UserCooldownMinutes) -
                            (now - last.UpdatedAt);
        return remainingCooldown > TimeSpan.Zero ? "cooldown" : null;
    }

    public static bool IsOpen(MusicRequestEntry entry) => entry.Status is
        MusicRequestStatus.Checking or MusicRequestStatus.Accepted or
        MusicRequestStatus.Queued or MusicRequestStatus.Playing or
        MusicRequestStatus.Failed;
}
