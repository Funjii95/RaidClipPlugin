using RaidClipPlugin.Models;

namespace RaidClipPlugin.Config;

public sealed class MusicRequestConfig
{
    public bool Enabled { get; set; } = false;
    public string SpotifyClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "http://127.0.0.1:17892/callback/";
    public string SelectedRewardId { get; set; } = "";
    public string SelectedRewardName { get; set; } = "";
    public MusicPlaybackMode PlaybackMode { get; set; } = MusicPlaybackMode.AddToQueue;
    public string SelectedDeviceId { get; set; } = "";
    public bool UseActiveDevice { get; set; } = true;
    public bool ActivateSelectedDevice { get; set; } = true;
    public int MaximumTrackDurationMinutes { get; set; } = 10;
    public bool AllowExplicitTracks { get; set; } = false;
    public int MaximumQueueLength { get; set; } = 25;
    public int UserCooldownMinutes { get; set; } = 5;
    public int MaximumRequestsPerUser { get; set; } = 2;
    public bool AllowDuplicateTracks { get; set; } = false;
    public bool AllowSpotifyLinks { get; set; } = true;
    public bool AllowTextSearch { get; set; } = true;
    public bool AutoFulfillRedemptions { get; set; } = true;
    public bool AutoCancelRejectedRedemptions { get; set; } = true;
    public List<string> UserBlacklist { get; set; } = new();
    public List<string> ArtistBlacklist { get; set; } = new();
    public List<string> TrackBlacklist { get; set; } = new();
    public List<string> SongTitleBlacklist { get; set; } = new();
    public List<string> BlockedTitleTerms { get; set; } = new();
    public MusicRequestChatMessages ChatMessages { get; set; } = new();
    public MusicModeratorCommands ModeratorCommands { get; set; } = new();
}

public sealed class MusicRequestChatMessages
{
    public string Queued { get; set; } = "🎵 @{user}, dein Musikwunsch „{track}“ von {artist} wurde zur Warteschlange hinzugefügt.";
    public string Playing { get; set; } = "▶️ @{user}, jetzt läuft „{track}“ von {artist}.";
    public string NotFound { get; set; } = "❌ @{user}, der gewünschte Song wurde nicht gefunden.";
    public string NoDevice { get; set; } = "❌ @{user}, aktuell ist kein Spotify-Wiedergabegerät verfügbar.";
    public string TooLong { get; set; } = "❌ @{user}, der Song überschreitet die maximal erlaubte Länge von {maxDuration} Minuten.";
    public string ExplicitBlocked { get; set; } = "❌ @{user}, explizite Songs sind derzeit nicht erlaubt.";
    public string Cooldown { get; set; } = "⏳ @{user}, du kannst in {remainingCooldown} erneut einen Musikwunsch einreichen.";
    public string QueueFull { get; set; } = "❌ @{user}, die Musikwunsch-Warteschlange ist aktuell voll.";
    public string Blacklisted { get; set; } = "❌ @{user}, dieser Musikwunsch ist nicht erlaubt.";
    public string InvalidInput { get; set; } = "❌ @{user}, bitte gib einen gültigen Spotify-Track oder Songnamen ein.";
}

public sealed class MusicModeratorCommands
{
    public bool SongEnabled { get; set; } = true;
    public string Song { get; set; } = "!song";
    public bool SkipEnabled { get; set; } = true;
    public string Skip { get; set; } = "!skip";
    public bool QueueEnabled { get; set; } = true;
    public string Queue { get; set; } = "!musicqueue";
    public bool RemoveEnabled { get; set; } = true;
    public string Remove { get; set; } = "!removesong";
    public bool PauseEnabled { get; set; } = true;
    public string Pause { get; set; } = "!musicpause";
    public bool ResumeEnabled { get; set; } = true;
    public string Resume { get; set; } = "!musicresume";
}
