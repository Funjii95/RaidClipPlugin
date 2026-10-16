using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly PropertyGrid _fishingSettings = NewAdventureGrid();
    private readonly PropertyGrid _treasureSettings = NewAdventureGrid();
    private readonly PropertyGrid _raceSettings = NewAdventureGrid();
    private readonly PropertyGrid _bossSettings = NewAdventureGrid();
    private static PropertyGrid NewAdventureGrid() => new()
    { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true, PropertySort = PropertySort.NoSort };

    internal static void StyleAdventureGrid(PropertyGrid grid, Color input, Color surface,
        Color text, Color muted, Color border, Color selection)
    {
        grid.BackColor = surface;
        grid.ForeColor = text;
        grid.ViewBackColor = input;
        grid.ViewForeColor = text;
        grid.ViewBorderColor = border;
        grid.HelpBackColor = surface;
        grid.HelpForeColor = text;
        grid.HelpBorderColor = border;
        grid.LineColor = border;
        grid.CategoryForeColor = text;
        grid.CategorySplitterColor = surface;
        grid.DisabledItemForeColor = muted;
        grid.SelectedItemWithFocusBackColor = selection;
        grid.SelectedItemWithFocusForeColor = text;
        grid.CommandsBackColor = surface;
        grid.CommandsForeColor = text;
        grid.CommandsBorderColor = border;
    }

    private Control BuildAdventureControlPanel()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var status = new Label { Dock = DockStyle.Fill, Text = "Keine laufenden Events.", AutoSize = false };
        var stake = new NumericUpDown { Minimum = 1, Maximum = 1000000000000, Value = 100, Width = 140, ThousandsSeparator = true };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        actions.Controls.Add(new Label { Text = "Renn-Einsatz:", AutoSize = true, Margin = new Padding(4, 10, 4, 4) });
        actions.Controls.Add(stake);
        foreach (var (title, action) in new[] { ("Boss starten", "boss"), ("Boss abbrechen", "cancelboss"), ("Rennen starten", "race"), ("Rennen abbrechen", "cancelrace") })
        {
            var button = NewActionButton(title);
            button.Click += async (_, _) =>
            {
                if (_minigame is null) { MessageBox.Show(this, "Bitte zuerst das Minigame-Modul starten."); return; }
                button.Enabled = false;
                try { await _minigame.ControlAdventureAsync(action, (long)stake.Value, _shutdown?.Token ?? CancellationToken.None, _broadcaster); }
                catch (Exception ex) { AppendLog("Event-Steuerung: " + ex.Message); MessageBox.Show(this, ex.Message, "Event-Steuerung"); }
                finally { button.Enabled = true; }
            };
            actions.Controls.Add(button);
        }
        var overlay = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        overlay.Controls.Add(new Label { Text = "OBS: Browserquelle anlegen, 600 × 500, transparenter Hintergrund.\nURL: " + AdventureOverlayServer.Url + "\nNur während das Minigame-Modul läuft; ohne Event bleibt die Anzeige leer.", AutoSize = true });
        var copy = NewActionButton("OBS-Adresse kopieren");
        copy.Click += (_, _) => Clipboard.SetText(AdventureOverlayServer.Url);
        overlay.Controls.Add(copy);
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) =>
        {
            var s = _minigame?.AdventureStatus ?? new AdventureStatus();
            static int Seconds(DateTimeOffset? end) => end is null ? 0 : Math.Max(0, (int)(end.Value - DateTimeOffset.UtcNow).TotalSeconds);
            status.Text = (s.BossActive ? $"Boss: {s.BossHp}/{s.BossMaxHp} LP · {s.BossPlayers} Kämpfer · {Seconds(s.BossEndsAt)} Sekunden" : "Boss: inaktiv") + "\n" +
                (s.RaceActive ? $"Rennen: {s.RacePlayers} Teilnehmer · Einsatz {s.RaceStake} · Start in {Seconds(s.RaceEndsAt)} Sekunden" : "Rennen: inaktiv");
        };
        timer.Start();
        layout.Disposed += (_, _) => timer.Dispose();
        layout.Controls.Add(status, 0, 0); layout.Controls.Add(actions, 0, 1); layout.Controls.Add(overlay, 0, 2);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Chattexte werden direkt in den Spiel-Reitern angepasst.\nPlatzhalter: {user}, {currency}, {seconds}, {stake}, {count}, {hp}, {damage}, {reward}, {pot}, {balance}, {reason}.\nFischen/Schatzsuche zusätzlich: {outcome}, {payout}. Leere Texte werden nicht gesendet.\nFischsammlung: !sammlung · persönliche Statistiken: !profil.\nAbbrechen beendet die Runde ohne Auszahlung oder Einsatzabbuchung.", AutoSize = false }, 0, 3);
        return layout;
    }

    private Control BuildAdventurePanel(PropertyGrid grid, string description)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.Controls.Add(new Label { Text = description + "\nBenötigt aktivierte Punkte und Minigames. Bestehende Tages- und Kontolimits gelten für Einsatzspiele.", Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        var save = NewActionButton("Einstellungen speichern");
        save.Click += (_, _) => SaveMinigameSettingsFromControls();
        layout.Controls.Add(save, 0, 2);
        return layout;
    }
    private void LoadAdventureSettings(MinigameConfig config)
    {
        var copy = JsonSerializer.Deserialize<AdventureGamesConfig>(JsonSerializer.Serialize(config.Adventures))!;
        _fishingSettings.SelectedObject = copy.Fishing;
        _treasureSettings.SelectedObject = copy.Treasure;
        _raceSettings.SelectedObject = copy.Race;
        _bossSettings.SelectedObject = copy.Boss;
    }
    private void ReadAdventureSettings(MinigameConfig config)
    {
        if (_fishingSettings.SelectedObject is not StakeAdventureConfig fishing) return;
        config.Adventures = JsonSerializer.Deserialize<AdventureGamesConfig>(JsonSerializer.Serialize(new AdventureGamesConfig {
            Fishing = fishing, Treasure = (StakeAdventureConfig)_treasureSettings.SelectedObject,
            Race = (RaceAdventureConfig)_raceSettings.SelectedObject, Boss = (BossAdventureConfig)_bossSettings.SelectedObject }))!;
    }
}
