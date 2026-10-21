using System.ComponentModel;
namespace RaidClipPlugin.Config;
public sealed class RpgMimicConfig
{
    [Category("Fundchance"),DisplayName("Mimic aktiv")] public bool Enabled {get;set;}=true;
    [Category("Fundchance"),DisplayName("Grundchance je Tauchgang (%)"),Description("Gilt auch ohne Perk. Abgrundsinn ergänzt 0,1 Prozentpunkte pro Rang (maximal +0,5).")]
    public decimal BaseSpawnChance {get;set;}=.5m;
    [Category("Kampf"),DisplayName("Name")] public string Name {get;set;}="Mimic – Lebende Schatztruhe";
    [Category("Kampf"),DisplayName("Lebenspunkte")] public int HitPoints {get;set;}=200;
    [Category("Kampf"),DisplayName("Zeitlimit (Sekunden)")] public int DurationSeconds {get;set;}=120;
    [Category("Kampf"),DisplayName("Angriffscooldown (Sekunden)")] public int AttackCooldownSeconds {get;set;}=10;
    [Category("Beute"),DisplayName("Perlen-Belohnungstopf")] public long RewardPool {get;set;}=500;
    [Category("Beute"),DisplayName("Mimic Legendary-Chance (%)")] public decimal LegendaryChance {get;set;}=50;
    [Category("Beute"),DisplayName("Normaler Boss Legendary-Chance (%)")] public decimal NormalBossLegendaryChance {get;set;}=5;
    public void Validate()
    {
        if(BaseSpawnChance is <0 or >99.5m || LegendaryChance is <0 or >100 || NormalBossLegendaryChance is <0 or >100 ||
            string.IsNullOrWhiteSpace(Name) || Name.Length>60 || HitPoints is <10 or >1000000 || DurationSeconds is <10 or >1800 ||
            AttackCooldownSeconds is <1 or >86400 || RewardPool is <0 or >1000000000)
            throw new InvalidOperationException("Mimic: ungültige Fundchance, Bosswerte oder Belohnungen. Unter RPG → Mimic & Bossbeute korrigieren.");
    }
}
