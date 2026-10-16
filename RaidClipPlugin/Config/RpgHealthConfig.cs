using System.ComponentModel;

namespace RaidClipPlugin.Config;

public sealed class RpgHealthConfig
{
    [Category("Lebenspunkte"), DisplayName("HP-System aktiv")] public bool Enabled { get; set; } = true;
    [Category("Lebenspunkte"), DisplayName("Basis-MaxHP")] public int BaseHp { get; set; } = 100;
    [Category("Lebenspunkte"), DisplayName("MaxHP pro Level")] public int HpPerLevel { get; set; } = 2;
    [Category("Lebenspunkte"), DisplayName("Neue MaxHP beim Levelaufstieg heilen")] public bool HealLevelGain { get; set; } = true;
    [Category("Level / EXP"), DisplayName("Maximallevel")] public int MaximumLevel { get; set; } = 100;
    [Category("Level / EXP"), DisplayName("EXP je Levelstufe (steigend)")] public int ExperiencePerLevel { get; set; } = 1000;
    [Category("Level / EXP"), DisplayName("EXP Boss-Sieg")] public int VictoryExperience { get; set; } = 1000;
    [Category("Level / EXP"), DisplayName("EXP Boss entkommen")] public int EscapeExperience { get; set; } = 100;
    [Category("Level / EXP"), DisplayName("Top-Schaden Zusatz-EXP")] public int TopExperience { get; set; } = 200;
    [Category("Chat-Heilung"), DisplayName("Aktiviert")] public bool ChatHealing { get; set; } = true;
    [Category("Chat-Heilung"), DisplayName("HP je Nachricht")] public int ChatHp { get; set; } = 10;
    [Category("Chat-Heilung"), DisplayName("Cooldown (Sekunden)")] public int ChatCooldownSeconds { get; set; } = 30;
    [Category("Passive Heilung"), DisplayName("Aktiviert")] public bool PassiveHealing { get; set; } = true;
    [Category("Passive Heilung"), DisplayName("HP pro Intervall")] public int PassiveHp { get; set; } = 2;
    [Category("Passive Heilung"), DisplayName("Intervall (Sekunden)")] public int PassiveSeconds { get; set; } = 60;
    [Category("Passive Heilung"), DisplayName("Nachholgrenze (Minuten, 0 = bis MaxHP)")] public int CatchUpMinutes { get; set; }
    [Category("Boss-Gegenangriff"), DisplayName("Aktiviert")] public bool Counterattacks { get; set; } = true;
    [Category("Boss-Gegenangriff"), DisplayName("Gewöhnlich – Minimum")] public int CommonMinimum { get; set; } = 10;
    [Category("Boss-Gegenangriff"), DisplayName("Gewöhnlich – Maximum")] public int CommonMaximum { get; set; } = 20;
    [Category("Boss-Gegenangriff"), DisplayName("Stark – ab Boss-HP")] public int StrongThreshold { get; set; } = 1000;
    [Category("Boss-Gegenangriff"), DisplayName("Stark – Minimum")] public int StrongMinimum { get; set; } = 15;
    [Category("Boss-Gegenangriff"), DisplayName("Stark – Maximum")] public int StrongMaximum { get; set; } = 30;
    [Category("Boss-Gegenangriff"), DisplayName("Elite – ab Boss-HP")] public int EliteThreshold { get; set; } = 5000;
    [Category("Boss-Gegenangriff"), DisplayName("Elite – Minimum")] public int EliteMinimum { get; set; } = 25;
    [Category("Boss-Gegenangriff"), DisplayName("Elite – Maximum")] public int EliteMaximum { get; set; } = 40;
    [Category("Boss-Gegenangriff"), DisplayName("Legendär – ab Boss-HP")] public int LegendaryThreshold { get; set; } = 20000;
    [Category("Boss-Gegenangriff"), DisplayName("Legendär – Minimum")] public int LegendaryMinimum { get; set; } = 35;
    [Category("Boss-Gegenangriff"), DisplayName("Legendär – Maximum")] public int LegendaryMaximum { get; set; } = 55;
    public void Validate()
    {
        if (BaseHp is < 1 or > 1000000 || HpPerLevel is < 0 or > 10000 || MaximumLevel is < 1 or > 100 ||
            ExperiencePerLevel is < 1 or > 100000000 || VictoryExperience is < 0 or > 1000000000 || EscapeExperience is < 0 or > 1000000000 || TopExperience is < 0 or > 1000000000 ||
            ChatHp is < 0 or > 1000000 || ChatCooldownSeconds is < 1 or > 86400 || PassiveHp is < 0 or > 1000000 || PassiveSeconds is < 1 or > 86400 || CatchUpMinutes is < 0 or > 525600 ||
            StrongThreshold < 1 || EliteThreshold <= StrongThreshold || LegendaryThreshold <= EliteThreshold ||
            new[] { (CommonMinimum,CommonMaximum),(StrongMinimum,StrongMaximum),(EliteMinimum,EliteMaximum),(LegendaryMinimum,LegendaryMaximum) }
                .Any(p => p.Item1 < 0 || p.Item2 < p.Item1 || p.Item2 > 1000000))
            throw new InvalidOperationException("Ungültige HP-, Heilungs-, EXP- oder Gegenangriffseinstellungen.");
    }
}
