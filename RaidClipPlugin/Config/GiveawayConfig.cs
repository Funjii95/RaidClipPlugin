namespace RaidClipPlugin.Config;

public sealed class GiveawayConfig
{
    public bool Enabled { get; set; } = false;
    public string Title { get; set; } = "Community Giveaway";
    public string Description { get; set; } = "";
    public string Prize { get; set; } = "Überraschung";
    public string Command { get; set; } = "!giveaway";
    public List<string> Aliases { get; set; } = new() { "!join", "!gewinnspiel" };
    public int DurationMinutes { get; set; } = 10;
    public int MaximumWinners { get; set; } = 1;
    public bool PreventDuplicateEntries { get; set; } = true;
    public bool PreventReentryAfterLeaving { get; set; } = true;
    public bool AutoDrawWhenExpired { get; set; } = true;
    public bool LiveOnly { get; set; } = true;
    public bool AnnounceWinners { get; set; } = true;
    public bool AnnounceParticipantCount { get; set; } = false;
    public int ParticipantCountIntervalMinutes { get; set; } = 5;
    public bool ShowParticipantList { get; set; } = true;
    public bool AutoCloseAfterDraw { get; set; } = true;
    public GiveawayAllowedRoles AllowedRoles { get; set; } = new();
    public int MinimumFollowMinutes { get; set; } = 0;
    public int MinimumPoints { get; set; } = 0;
    public int EntryCost { get; set; } = 0;
    public bool DeductPointsAtJoin { get; set; } = true;
    public bool RefundPointsOnCancel { get; set; } = true;
    public List<string> AllowedUsers { get; set; } = new();
    public List<string> BlockedUsers { get; set; } = new();
    public bool ExcludeBots { get; set; } = true;
    public bool ExcludeBroadcasterFromDraw { get; set; } = true;
    public bool AllowPreviousWinners { get; set; } = false;
    public bool ExtraTicketsEnabled { get; set; } = false;
    public int ExtraTicketCost { get; set; } = 100;
    public int MaximumExtraTickets { get; set; } = 5;
    public GiveawayModeratorCommands ModeratorCommands { get; set; } = new();
    public GiveawayChatMessages ChatMessages { get; set; } = new();
}

public sealed class GiveawayAllowedRoles
{
    public bool Everyone { get; set; } = true;
    public bool Followers { get; set; } = true;
    public bool Subscribers { get; set; } = true;
    public bool Vips { get; set; } = true;
    public bool Moderators { get; set; } = true;
    public bool Broadcaster { get; set; } = false;
}

public sealed class GiveawayModeratorCommands
{
    public bool Enabled { get; set; } = true;
    public string Start { get; set; } = "!giveaway start";
    public string Stop { get; set; } = "!giveaway stop";
    public string Pause { get; set; } = "!giveaway pause";
    public string Resume { get; set; } = "!giveaway resume";
    public string Draw { get; set; } = "!giveaway draw";
    public string Reroll { get; set; } = "!giveaway reroll";
    public string Status { get; set; } = "!giveaway status";
}

public sealed class GiveawayChatMessages
{
    public GiveawayChatMessage Started { get; set; } = new(true,
        "🎉 Giveaway gestartet! Gewinn: {prize} – Teilnahme mit {command}");
    public GiveawayChatMessage Joined { get; set; } = new(true,
        "@{username}, du nimmst am Giveaway teil!");
    public GiveawayChatMessage Duplicate { get; set; } = new(true,
        "@{username}, du bist bereits im Giveaway eingetragen.");
    public GiveawayChatMessage InsufficientPoints { get; set; } = new(true,
        "@{username}, du benötigst mindestens {requiredPoints} Punkte.");
    public GiveawayChatMessage Excluded { get; set; } = new(true,
        "@{username}, du darfst an diesem Giveaway nicht teilnehmen.");
    public GiveawayChatMessage Ended { get; set; } = new(true,
        "Das Giveaway ist beendet. Insgesamt haben {participantCount} Zuschauer teilgenommen.");
    public GiveawayChatMessage Winner { get; set; } = new(true,
        "🎉 Gewinner des Giveaways ist @{winner}! Herzlichen Glückwunsch zu {prize}!");
    public GiveawayChatMessage Winners { get; set; } = new(true,
        "🎉 Die Gewinner sind: {winners}");
    public GiveawayChatMessage Status { get; set; } = new(true,
        "Giveaway {title}: {participantCount} Teilnehmer, noch {remainingTime}.");
    public GiveawayChatMessage Paused { get; set; } = new(true,
        "Das Giveaway wurde pausiert.");
    public GiveawayChatMessage Resumed { get; set; } = new(true,
        "Das Giveaway läuft weiter. Teilnahme mit {command}");
    public GiveawayChatMessage Cancelled { get; set; } = new(true,
        "Das Giveaway wurde abgebrochen.");
    public GiveawayChatMessage Offline { get; set; } = new(true,
        "Giveaway-Teilnahme ist nur während des Livestreams möglich.");
    public GiveawayChatMessage NotActive { get; set; } = new(true,
        "Aktuell läuft kein Giveaway.");
}

public sealed class GiveawayChatMessage
{
    public GiveawayChatMessage() { }
    public GiveawayChatMessage(bool enabled, string text)
    {
        Enabled = enabled;
        Text = text;
    }
    public bool Enabled { get; set; }
    public string Text { get; set; } = "";
}