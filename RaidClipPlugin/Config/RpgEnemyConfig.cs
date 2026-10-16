using System.ComponentModel;

namespace RaidClipPlugin.Config;

public sealed class RpgEnemyConfig
{
    [DisplayName("Unterwassergegner aktiv")] public bool Enabled { get; set; } = true;
    [DisplayName("Begegnungschance (%) – 10 bis 20")] public decimal EncounterChance { get; set; } = 15;
    [DisplayName("Maximale Kampfrunden")] public int MaximumRounds { get; set; } = 6;
    public List<RpgEnemyTier> Tiers { get; set; } = new()
    {
        new() { MinimumStake=0,Name="Schnappkrabbe",HitPoints=25,MinimumDamage=3,MaximumDamage=6,Experience=30 },
        new() { MinimumStake=250,Name="Korallenräuber",HitPoints=40,MinimumDamage=5,MaximumDamage=9,Experience=60 },
        new() { MinimumStake=1000,Name="Tiefsee-Muräne",HitPoints=60,MinimumDamage=7,MaximumDamage=12,Experience=100 },
        new() { MinimumStake=5000,Name="Abgrundwächter",HitPoints=85,MinimumDamage=10,MaximumDamage=16,Experience=180 },
        new() { MinimumStake=10000,Name="Leviathan-Jungtier",HitPoints=110,MinimumDamage=12,MaximumDamage=20,Experience=300 }
    };
    public void Validate()
    {
        if(EncounterChance is <10 or >20 || MaximumRounds is <1 or >20 || Tiers is null || Tiers.Count is <1 or >30 ||
            !Tiers.Any(t=>t.MinimumStake==0) || Tiers.Select(t=>t.MinimumStake).Distinct().Count()!=Tiers.Count ||
            Tiers.Any(t=>t.MinimumStake<0 || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length>60 || t.HitPoints is <1 or >1000000 ||
                t.MinimumDamage<0 || t.MaximumDamage<t.MinimumDamage || t.MaximumDamage>1000000 || t.Experience is <0 or >1000000))
            throw new InvalidOperationException("Ungültige Unterwassergegner: Chance 10–20 %, gültige Einsatzstufen und Kampfwerte erforderlich.");
    }
}

public sealed class RpgEnemyTier
{
    public long MinimumStake { get; set; }
    public string Name { get; set; } = "Unterwassergegner";
    public int HitPoints { get; set; } = 25;
    public int MinimumDamage { get; set; } = 3;
    public int MaximumDamage { get; set; } = 6;
    public int Experience { get; set; } = 30;
}
