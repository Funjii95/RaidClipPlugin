namespace RaidClipPlugin.Config;


public sealed class DuelConfig
{
    public bool Enabled { get; set; }
    public string DuelCommand { get; set; } = "!duel";
    public string AcceptCommand { get; set; } = "!accept";
    public string DenyCommand { get; set; } = "!deny";
    public int MinimumBet { get; set; } = 10;
    public int MaximumBet { get; set; } = 10000;
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int UserCooldownSeconds { get; set; } = 30;
    public int GlobalCooldownSeconds { get; set; } = 3;
    public bool AllowEveryone { get; set; } = true;
    public bool AllowFollowers { get; set; } = true;
    public bool AllowSubscribers { get; set; } = true;
    public bool AllowVips { get; set; } = true;
    public bool AllowModerators { get; set; } = true;
    public bool AllowAllIn { get; set; } = true;
    public bool FairMode { get; set; } = true;
    public int ChallengerWinChancePercent { get; set; } = 50;
    public bool SendRequestMessage { get; set; } = true;
    public bool SendResultMessage { get; set; } = true;
    public bool SendDenyMessage { get; set; } = true;
    public bool SendTimeoutMessage { get; set; } = true;
    public bool TimeoutLoserEnabled { get; set; }
    public int LoserTimeoutSeconds { get; set; } = 60;
    public string LoserTimeoutReason { get; set; } = "Duel verloren";
    public string DuelRequestMessage { get; set; } = "@{challenger} fordert @{target} zu einem Duel um {amount} {currencyName} heraus! @{target}, antworte mit {acceptCommand} oder {denyCommand}. Zeit: {seconds} Sekunden.";
    public string DuelAcceptedMessage { get; set; } = "@{target} nimmt das Duel gegen @{challenger} an! Der Pot beträgt {pot} {currencyName}.";
    public string DuelWinMessage { get; set; } = "Das Duel ist entschieden! @{winner} gewinnt gegen @{loser} und erhält {pot} {currencyName}!";
    public string DuelDeniedMessage { get; set; } = "@{target} hat das Duel gegen @{challenger} abgelehnt. Der Einsatz wurde zurückgegeben.";
    public string DuelTimeoutMessage { get; set; } = "Das Duel zwischen @{challenger} und @{target} ist abgelaufen. Der Einsatz wurde zurückgegeben.";
    public string NotEnoughPointsChallengerMessage { get; set; } = "@{challenger}, du hast nicht genug {currencyName} für dieses Duel.";
    public string NotEnoughPointsTargetMessage { get; set; } = "@{target}, du hast nicht genug {currencyName}, um dieses Duel anzunehmen.";
    public string SelfDuelMessage { get; set; } = "@{user}, du kannst dich nicht selbst duellieren.";
    public string NoPendingDuelMessage { get; set; } = "@{user}, du hast aktuell keine offene Duel-Anfrage.";
    public string WrongTargetMessage { get; set; } = "@{user}, diese Duel-Anfrage ist nicht für dich bestimmt.";
    public string AlreadyPendingDuelMessage { get; set; } = "@{user}, du hast bereits eine offene Duel-Anfrage.";
    public string InvalidBetMessage { get; set; } = "@{user}, bitte nutze: {duelCommand} <user> <punkte>";
}