namespace RaidClipPlugin.Config;

public sealed class WheelConfig
{
    public List<string> Entries { get; set; } = new() { "Überraschung", "Nochmal drehen", "Otter-Jubel", "Freie Auswahl" };
    public int SpinSeconds { get; set; } = 8;
    public string DesignImagePath { get; set; } = "";
    public void Validate()
    {
        if (Entries is null || Entries.Count is < 2 or > 100 || Entries.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 80))
            throw new InvalidOperationException("Glücksrad: Bitte 2 bis 100 Felder mit je 1 bis 80 Zeichen eintragen.");
        if (SpinSeconds is < 3 or > 30) throw new InvalidOperationException("Glücksrad: Drehdauer muss 3 bis 30 Sekunden betragen.");
    }
}
