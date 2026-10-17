using System.ComponentModel;

namespace RaidClipPlugin.Config;

public sealed class RpgHotTimeConfig
{
    [DisplayName("Aktiviert")] public bool Enabled { get; set; } = true;
    [DisplayName("Automatisch nach Boss-Sieg")] public bool AutoAfterBoss { get; set; } = true;
    [DisplayName("Dauer (Minuten)")] public int DurationMinutes { get; set; } = 5;
    [DisplayName("Relativer Dropbonus (%)")] public decimal DropBonusPercent { get; set; } = 50;
    [DisplayName("Perlenbonus (%)")] public decimal PearlBonusPercent { get; set; } = 100;
    [DisplayName("Startmeldung")] public string StartMessage { get; set; } = "🔥 HOT TIME! Der Boss wurde besiegt! Für {minutes} Minuten gibt es +{drop}% Item-Dropchance und {reward}! 🦦💎";
    [DisplayName("Endmeldung")] public string EndMessage { get; set; } = "⌛ HOT TIME ist vorbei! Die normalen Loot- und Perlenbelohnungen gelten wieder.";
    public void Validate()
    {
        if(DurationMinutes is < 1 or > 60 || DropBonusPercent is < 0 or > 200 || PearlBonusPercent is < 0 or > 200 || StartMessage.Length>450 || EndMessage.Length>450)
            throw new InvalidOperationException("Hot Time: Dauer 1–60 Minuten, Boni 0–200 %, Chattexte maximal 450 Zeichen.");
    }
}
