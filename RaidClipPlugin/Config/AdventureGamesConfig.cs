using System.ComponentModel;

namespace RaidClipPlugin.Config;

public sealed class AdventureGamesConfig
{
    public StakeAdventureConfig Fishing { get; set; } = new();
    public StakeAdventureConfig Treasure { get; set; } = new();
    public RaceAdventureConfig Race { get; set; } = new();
    public BossAdventureConfig Boss { get; set; } = new();
}

public class StakeAdventureConfig
{
    [DisplayName("Cooldown-Text"), Description("{user}, {command}, {remaining} (Sekunden)")]
    public string CooldownMessage { get; set; } = "@{user}, {command} ist in {remaining} Sekunden wieder verfügbar.";
    [DisplayName("Ergebnistext"), Description("{user}, {outcome}, {stake}, {payout}, {currency}, {balance}")]
    public string ResultMessage { get; set; } = "@{user}, {outcome} Einsatz: {stake}, Auszahlung: {payout} {currency}. Stand: {balance}.";
    [DisplayName("Aktiviert")] public bool Enabled { get; set; } = true;
    [DisplayName("Minimaler Einsatz")] public long MinimumBet { get; set; } = 10;
    [DisplayName("Maximaler Einsatz")] public long MaximumBet { get; set; } = 1000;
    [DisplayName("Nutzer-Cooldown (Sekunden)")] public int CooldownSeconds { get; set; } = 30;
}

public sealed class RaceAdventureConfig : StakeAdventureConfig
{
    [DisplayName("Starttext")] public string StartMessage { get; set; } = "Rennen eröffnet! Mit !joinrace beitreten ({seconds} Sekunden). Einsatz: {stake} {currency} für alle; Abrechnung erst beim Rennstart. Mindestens 2 Teilnehmer.";
    [DisplayName("Beitrittstext")] public string JoinMessage { get; set; } = "@{user} ist im Rennen! Teilnehmer: {count}.";
    [DisplayName("Gewinntext")] public string WinMessage { get; set; } = "Zieleinlauf! @{user} gewinnt den Topf von {pot} {currency}! Stand: {balance}.";
    [DisplayName("Abbruchtext")] public string CancelMessage { get; set; } = "Rennen abgesagt: {reason}. Keine Einsätze abgebucht.";
    [DisplayName("Beitrittsphase (Sekunden)")] public int JoinSeconds { get; set; } = 30;
    [DisplayName("Maximale Teilnehmer")] public int MaximumPlayers { get; set; } = 50;
    [DisplayName("Rundenpause (Sekunden)")] public int RoundCooldownSeconds { get; set; } = 60;
}

public sealed class BossAdventureConfig
{
    [DisplayName("Starttext")] public string StartMessage { get; set; } = "Ein Perlenwächter erscheint! {hp} LP, {seconds} Sekunden. Alle greifen mit !joinboss an! Belohnungstopf: {reward} {currency}.";
    [DisplayName("Angriffstext")] public string AttackMessage { get; set; } = "@{user} trifft für {damage}! Boss: {hp} LP.";
    [DisplayName("Siegtext")] public string WinMessage { get; set; } = "Boss besiegt! {count} Kämpfer teilen die Beute: je {reward} {currency} (Kontolimit gilt).";
    [DisplayName("Zeitablauftext")] public string TimeoutMessage { get; set; } = "Der Boss entkommt! Zeit abgelaufen – keine Belohnung, kein Einsatz verloren.";
    [DisplayName("Abbruchtext")] public string CancelMessage { get; set; } = "Bosskampf manuell abgebrochen – keine Belohnung, kein Einsatz verloren.";
    [DisplayName("Automatisch starten")] public bool AutoStartEnabled { get; set; }
    [DisplayName("Auto-Start alle X Minuten")] public int AutoStartMinutes { get; set; } = 15;
    [DisplayName("Aktiviert")] public bool Enabled { get; set; } = true;
    [DisplayName("Lebenspunkte")] public int HitPoints { get; set; } = 200;
    [DisplayName("Zeitlimit (Sekunden)")] public int DurationSeconds { get; set; } = 120;
    [DisplayName("Angriffs-Cooldown je Nutzer (Sekunden)")] public int AttackCooldownSeconds { get; set; } = 10;
    [DisplayName("Belohnungstopf")] public long RewardPool { get; set; } = 500;
    [DisplayName("Rundenpause (Sekunden)")] public int RoundCooldownSeconds { get; set; } = 300;
}
