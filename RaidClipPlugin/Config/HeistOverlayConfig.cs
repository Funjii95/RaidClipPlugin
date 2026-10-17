namespace RaidClipPlugin.Config;

public sealed class HeistOverlayConfig
{
    public bool Enabled { get; set; } = true;
    public string Title { get; set; } = "Überfall auf die Perlenschatzkammer";
    public string BrowserSource { get; set; } = "";
    public string Scene { get; set; } = "";
    public int ResultSeconds { get; set; } = 10;
    public int AnimationSeconds { get; set; } = 6;
    public bool ShowParticipants { get; set; } = true;
    public int MaximumVisibleParticipants { get; set; } = 12;
    public bool SuccessAnimation { get; set; } = true;
    public bool FailureAnimation { get; set; } = true;
    public bool ShowHotTime { get; set; } = true;
    public int BossPriority { get; set; } = 100;
    public int RacePriority { get; set; } = 80;
    public int HeistPriority { get; set; } = 60;
    public int ResultPriority { get; set; } = 20;
    public int MaximumQueuedSeconds { get; set; } = 120;
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Title) || Title.Length>120 || BrowserSource is null || Scene is null || BrowserSource.Length>200 || Scene.Length>200 || ResultSeconds is <2 or >60 || AnimationSeconds is <2 or >20 ||
            MaximumVisibleParticipants is <1 or >50 || MaximumQueuedSeconds is <10 or >600 ||
            new[]{BossPriority,RacePriority,HeistPriority,ResultPriority}.Any(p=>p is <0 or >1000))
            throw new InvalidOperationException("Ungültige Heist-Overlay-Einstellungen: Animation 2–20s, Ergebnis 2–60s, Teilnehmer 1–50, Warteschlange 10–600s.");
    }
}
