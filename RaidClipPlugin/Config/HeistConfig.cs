namespace RaidClipPlugin.Config;

public sealed class HeistConfig
{
    public bool Enabled { get; set; }
    public string StartCommand { get; set; } = "!heist";
    public string JoinCommand { get; set; } = "!join";
    public int MinimumParticipants { get; set; } = 3;
    public int MaximumParticipants { get; set; } = 50;
    public int JoinDurationSeconds { get; set; } = 60;
    public int SuccessChancePercent { get; set; } = 50;
    public int UserCooldownMinutes { get; set; } = 30;
    public int GlobalCooldownMinutes { get; set; } = 10;
    public bool ApplyGlobalCooldownOnCancelledHeist { get; set; }
    public bool AllowEveryone { get; set; } = true;
    public bool AllowFollowers { get; set; } = true;
    public bool AllowSubscribers { get; set; } = true;
    public bool AllowVips { get; set; } = true;
    public bool AllowModerators { get; set; } = true;
    public bool SendParticipantJoinMessages { get; set; } = true;
    public bool SendCountdownMessages { get; set; }
    public bool SendResultMessages { get; set; } = true;
    public bool ResetJackpotAfterSuccess { get; set; } = true;
    public string StartMessage { get; set; } = "@{user} plant einen Heist! Schließt euch innerhalb von {seconds} Sekunden mit {joinCommand} an. Mindestens {minimum} Teilnehmer werden benötigt.";
    public string JoinMessage { get; set; } = "@{user} nimmt am Heist teil! Aktuelle Teilnehmer: {current}/{minimum}";
    public string AlreadyJoinedMessage { get; set; } = "@{user}, du nimmst bereits an diesem Heist teil.";
    public string NoActiveHeistMessage { get; set; } = "@{user}, aktuell läuft kein Heist.";
    public string MaximumParticipantsMessage { get; set; } = "@{user}, der Heist ist bereits voll. Es können maximal {maximum} Teilnehmer mitmachen.";
    public string NotEnoughParticipantsMessage { get; set; } = "Der Heist wurde abgebrochen. Es haben nur {current} von mindestens {minimum} benötigten Teilnehmern mitgemacht.";
    public string EvaluationMessage { get; set; } = "Der Heist beginnt mit {count} Teilnehmern! Ziel ist der aktuelle Jackpot von {jackpot} {currencyName}.";
    public string SuccessMessage { get; set; } = "Der Heist war erfolgreich! {count} Teilnehmer teilen sich den Jackpot von {jackpot} {currencyName}. Jeder erhält ungefähr {share} {currencyName}.";
    public string FailureMessage { get; set; } = "Der Heist ist gescheitert! Der Jackpot von {jackpot} {currencyName} bleibt erhalten.";
}

public sealed class CommandsConfig
{
    public bool Enabled { get; set; } = true;
    public string Command { get; set; } = "!commands";
    public int UserCooldownSeconds { get; set; } = 15;
    public int GlobalCooldownSeconds { get; set; } = 3;
    public int CommandsPerPage { get; set; } = 8;
    public int MaximumMessagesPerRequest { get; set; } = 3;
    public bool ShowDescriptions { get; set; }
    public bool ShowAliases { get; set; }
    public bool GroupByModule { get; set; } = true;
    public bool IncludeDisabledCommandsInUi { get; set; } = true;
    public string ExportDirectory { get; set; } = "exports";
    public Dictionary<string, string> CommandRoleOverrides { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public bool CustomCommandsEnabled { get; set; } = true;
    public List<CustomChatCommandConfig> CustomCommands { get; set; } =
        CustomChatCommandConfig.CreateExamples();
}

public sealed class CustomChatCommandConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; }
    public string Command { get; set; } = "!command";
    public List<string> Aliases { get; set; } = new();
    public string Response { get; set; } = "Hallo @{user}!";
    public string RequiredRole { get; set; } = "Viewer";
    public int UserCooldownSeconds { get; set; } = 15;
    public int GlobalCooldownSeconds { get; set; } = 3;

    public static List<CustomChatCommandConfig> CreateExamples() => new()
    {
        new()
        {
            Enabled = false,
            Command = "!raid",
            Response = "funjiiRaid Funjii's Otter-Familie ist angekommen! Viel Liebe und gute Vibes für den Stream! funjiiRaid",
            RequiredRole = "Viewer",
            UserCooldownSeconds = 30,
            GlobalCooldownSeconds = 5
        }
    };
}
