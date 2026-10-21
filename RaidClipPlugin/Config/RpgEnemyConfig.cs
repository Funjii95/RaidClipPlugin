using System.ComponentModel;

namespace RaidClipPlugin.Config;

public sealed class RpgEnemyConfig
{
    [DisplayName("Unterwassergegner aktiv")] public bool Enabled { get; set; } = true;
    [DisplayName("Begegnungschance (%) – 0 bis 100")] public decimal EncounterChance { get; set; } = 15;
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
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        void Fail(string detail)=>throw new InvalidOperationException("Unterwassergegner: "+detail+" Einstellungen unter RPG → Unterwassergegner korrigieren oder dort den zuletzt geladenen Stand wiederherstellen.");
        if(EncounterChance is <0 or >100)Fail($"Begegnungschance {EncounterChance} % ist ungültig; erlaubt sind 0–100 %.");
        if(MaximumRounds is <1 or >20)Fail($"Maximale Kampfrunden {MaximumRounds} sind ungültig; erlaubt sind 1–20.");
        if(Tiers is null || Tiers.Count is <1 or >30)Fail("Es sind 1–30 Gegnerstufen erforderlich.");
        if(Tiers!.Any(t=>t is null))Fail("Eine Gegnerstufe fehlt.");
        if(!Tiers.Any(t=>t.MinimumStake==0))Fail("Die Basisstufe mit Mindesteinsatz 0 fehlt.");
        var duplicate=Tiers.GroupBy(t=>t.MinimumStake).FirstOrDefault(g=>g.Count()>1);
        if(duplicate is not null)Fail($"Mindesteinsatz {duplicate.Key} ist mehrfach vergeben ({string.Join(", ",duplicate.Select(t=>t.Name))}); jede Stufe benötigt einen eindeutigen Einsatz.");
        for(var i=0;i<Tiers.Count;i++)
        {
            var t=Tiers[i];var label=$"Stufe {i+1} ({t.Name}): ";
            if(t.MinimumStake<0)Fail(label+"Mindesteinsatz darf nicht negativ sein.");
            if(string.IsNullOrWhiteSpace(t.Name) || t.Name.Length>60)Fail(label+"Name muss 1–60 Zeichen enthalten.");
            if(t.HitPoints is <1 or >1000000)Fail(label+$"Lebenspunkte {t.HitPoints} sind ungültig; erlaubt sind 1–1.000.000.");
            if(t.MinimumDamage<0 || t.MaximumDamage<t.MinimumDamage || t.MaximumDamage>1000000)Fail(label+$"Schadensbereich {t.MinimumDamage}–{t.MaximumDamage} ist ungültig; Maximum muss mindestens dem Minimum entsprechen (0–1.000.000).");
            if(t.Experience is <0 or >1000000)Fail(label+$"EXP {t.Experience} sind ungültig; erlaubt sind 0–1.000.000.");
        }
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
