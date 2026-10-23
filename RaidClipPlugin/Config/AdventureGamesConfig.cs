using System.ComponentModel;

namespace RaidClipPlugin.Config;

public sealed class AdventureGamesConfig
{
    public RpgConfig Rpg { get; set; } = new();
    public SoloAdventureConfig Fishing { get; set; } = new();
    public DiveAdventureConfig Treasure { get; set; } = new();
    public RaceAdventureConfig Race { get; set; } = new();
    public BossAdventureConfig Boss { get; set; } = new();
}

public class StakeAdventureConfig
{
    [DisplayName("Cooldown-Text"), Description("{user}, {command}, {remaining} (Sekunden)")]
    public string CooldownMessage { get; set; } = "@{user}, {command} ist in {remaining} Sekunden wieder verfügbar.";
    [DisplayName("Ergebnistext"), Description("{user}, {outcome}, {stake}, {payout}, {currency}, {balance}")]
    public string ResultMessage { get; set; } = "@{user}, {outcome} Einsatz: {stake}, Auszahlung: {payout} {currency}. Stand: {balance}.";
    [Browsable(false), DisplayName("Aktiviert")] public bool Enabled { get; set; } = true;
    [DisplayName("Minimaler Einsatz")] public long MinimumBet { get; set; } = 10;
    [DisplayName("Maximaler Einsatz")] public long MaximumBet { get; set; } = 1000;
    [DisplayName("Nutzer-Cooldown (Sekunden)")] public int CooldownSeconds { get; set; } = 30;
}

public class SoloAdventureConfig : StakeAdventureConfig
{
    [Category("Gewinnchancen"), DisplayName("Stufe 1 – Niete")] public int LossChance { get; set; } = 50;
    [Category("Gewinnchancen"), DisplayName("Stufe 2 – Kleiner Fund")] public int SmallChance { get; set; } = 30;
    [Category("Gewinnchancen"), DisplayName("Stufe 3 – Großer Fund")] public int LargeChance { get; set; } = 17;
    [Category("Gewinnchancen"), DisplayName("Stufe 4 – Seltener Fund")] public int RareChance { get; set; } = 3;
    [Category("Auszahlungen"), DisplayName("Stufe 1 – Faktor")] public decimal LossMultiplier { get; set; }
    [Category("Auszahlungen"), DisplayName("Stufe 2 – Faktor")] public decimal SmallMultiplier { get; set; } = 1;
    [Category("Auszahlungen"), DisplayName("Stufe 3 – Faktor")] public decimal LargeMultiplier { get; set; } = 2;
    [Category("Auszahlungen"), DisplayName("Stufe 4 – Faktor")] public decimal RareMultiplier { get; set; } = 8;

    public void Validate(string game)
    {
        var chances = new[] { LossChance, SmallChance, LargeChance, RareChance };
        if (chances.Any(c => c is < 0 or > 100) || chances.Sum() != 100)
            throw new InvalidOperationException($"{game}: Die vier Gewinnchancen müssen jeweils zwischen 0 und 100 liegen und zusammen genau 100 % ergeben.");
        if (new[] { LossMultiplier, SmallMultiplier, LargeMultiplier, RareMultiplier }.Any(m => m < 0 || m > 100))
            throw new InvalidOperationException($"{game}: Auszahlungsfaktoren müssen zwischen 0 und 100 liegen.");
    }
}

public sealed class DiveAdventureConfig : SoloAdventureConfig
{
    public DiveAdventureConfig() { SmallChance = 25; LargeChance = 20; RareChance = 5; RareMultiplier = 6; MaximumBet = 10000; }
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

[TypeConverter(typeof(BossHpModeConverter))]
public enum BossHpMode { Fixed, Dynamic }

public sealed class BossHpModeConverter : EnumConverter
{
    public BossHpModeConverter() : base(typeof(BossHpMode)) { }
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is BossHpMode mode
            ? mode == BossHpMode.Dynamic ? "Dynamisch" : "Feste HP"
            : base.ConvertTo(context, culture, value, destinationType);
    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
        => value is string label && label == "Dynamisch" ? BossHpMode.Dynamic
            : value is string fixedLabel && fixedLabel == "Feste HP" ? BossHpMode.Fixed
            : base.ConvertFrom(context, culture, value);
}

public sealed class BossAdventureConfig
{
    [DisplayName("Boss-Schadensreduktion (%)")] public decimal DamageReductionPercent {get;set;}
    [Browsable(false)] public bool IsMimic {get;set;}
    [Browsable(false)] public string DisplayName {get;set;}="Perlenwächter";
    [DisplayName("Angriffs-Cooldown-Text"), Description("{user}, {remaining} (Sekunden)")]
    public string AttackCooldownMessage { get; set; } = "@{user}, du kannst in {remaining} Sekunden wieder mit !attack angreifen.";
    [DisplayName("Starttext")] public string StartMessage { get; set; } = "Ein Perlenwächter erscheint! {hp} LP, {seconds} Sekunden. Alle greifen mit !attack an! Belohnungstopf: {reward} {currency}.";
    [DisplayName("Angriffstext"), Description("{user}, {damage}, {hp}, {critical}. Kritische Treffer werden ohne {critical} automatisch im selben Chattext gekennzeichnet.")]
    public string AttackMessage { get; set; } = "@{user} trifft für {damage}! Boss: {hp} LP.";
    [DisplayName("Siegtext")] public string WinMessage { get; set; } = "Boss besiegt! {count} Kämpfer teilen die Beute: je {reward} {currency} (Kontolimit gilt).";
    [DisplayName("Zeitablauftext")] public string TimeoutMessage { get; set; } = "Der Boss entkommt! Zeit abgelaufen – keine Belohnung, kein Einsatz verloren.";
    [DisplayName("Abbruchtext")] public string CancelMessage { get; set; } = "Bosskampf manuell abgebrochen – keine Belohnung, kein Einsatz verloren.";
    [Browsable(false), DisplayName("Automatisch starten")] public bool AutoStartEnabled { get; set; }
    [DisplayName("Auto-Start alle X Minuten")] public int AutoStartMinutes { get; set; } = 15;
    [Browsable(false), DisplayName("Aktiviert")] public bool Enabled { get; set; } = true;
    [DisplayName("HP-Modus"), Description("Dynamisch: 100 HP pro unterschiedlichem Teilnehmer. Feste HP: verwendet Lebenspunkte. Gilt ab dem nächsten Boss.")]
    public BossHpMode HpMode { get; set; } = BossHpMode.Fixed;
    [DisplayName("Lebenspunkte"), Description("Boss-HP im Modus Feste HP. Im dynamischen Modus gilt 100 HP pro Teilnehmer.")] public int HitPoints { get; set; } = 200;
    [DisplayName("Zeitlimit (Sekunden)")] public int DurationSeconds { get; set; } = 120;
    [DisplayName("Angriffs-Cooldown je Nutzer (Sekunden)")] public int AttackCooldownSeconds { get; set; } = 10;
    [DisplayName("Belohnungstopf")] public long RewardPool { get; set; } = 500;
    [DisplayName("Rundenpause (Sekunden)")] public int RoundCooldownSeconds { get; set; } = 300;
}
