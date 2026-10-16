using System.Text.Json;
using System.ComponentModel;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly PropertyGrid _fishingSettings = NewAdventureGrid();
    private readonly PropertyGrid _treasureSettings = NewAdventureGrid();
    private readonly PropertyGrid _raceSettings = NewAdventureGrid();
    private readonly PropertyGrid _bossSettings = NewAdventureGrid();
    private readonly CheckBox _rpgEnabled = new() { Text = "RPG-Erweiterung aktivieren", AutoSize = true };
    private readonly TextBox _rpgConfigJson = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both,
        WordWrap = false, Font = new Font("Consolas", 10) };
    private Control BuildRpgSettingsPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(12) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        panel.Controls.Add(_rpgEnabled, 0, 0);
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "RPG-Balancing (JSON): Loot, dynamische Itemregeln, Effekte, Perks, Rezepte und Caps.\nDynamische Items: Vorlage unten übernehmen, dann speichern. Vorhandene Items behalten ihre Werte.\nChat: !inv, !item <Nummer>, !equip <Nummer>, !gear, !perks, !perk, !craft, !sushi, !stats." }, 0, 1);
        panel.Controls.Add(_rpgConfigJson, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        var preset = NewActionButton("Balancing 1.2 übernehmen");
        preset.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Lootwerte, Standard-Perkkosten und Rezeptkosten im Editor durch Balancing 1.2 ersetzen? Der Dive-Maximaleinsatz wird auf mindestens 10.000 erhöht. Eigene Itemdefinitionen, Fische, Attribute und Spielstände bleiben erhalten. Erst Speichern übernimmt die Einstellungen dauerhaft.",
                "Balancing 1.2", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                var current = JsonSerializer.Deserialize<RpgConfig>(_rpgConfigJson.Text) ?? throw new InvalidOperationException("RPG-Einstellungen fehlen.");
                _rpgConfigJson.Text = JsonSerializer.Serialize(RpgEconomyPreset.Apply12(current), new JsonSerializerOptions { WriteIndented = true });
                if (_treasureSettings.SelectedObject is DiveAdventureConfig dive) { dive.MaximumBet = Math.Max(10000, dive.MaximumBet); _treasureSettings.Refresh(); }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Balancing 1.2"); }
        };
        var save = NewActionButton("RPG-Einstellungen speichern");
        save.Click += (_, _) => SaveMinigameSettingsFromControls();
        var dynamicItems = NewActionButton("Dynamische Items vorbereiten");
        dynamicItems.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Zufällige Attribute und Spezialeffekte für künftige Ausrüstungsfunde im Editor ergänzen? Vorhandene Items und eigene dynamische Regeln bleiben unverändert. Erst Speichern aktiviert die Vorlage.",
                "Item-System 2.0", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                var current = JsonSerializer.Deserialize<RpgConfig>(_rpgConfigJson.Text) ?? throw new InvalidOperationException("RPG-Einstellungen fehlen.");
                _rpgConfigJson.Text = JsonSerializer.Serialize(RpgItemGenerationService.EnableForFutureDrops(current), new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Item-System 2.0"); }
        };
        var sets = NewActionButton("Sets & Legenden vorbereiten");
        sets.Click += (_, _) =>
        {
            try
            {
                var current = JsonSerializer.Deserialize<RpgConfig>(_rpgConfigJson.Text) ?? throw new InvalidOperationException("RPG-Einstellungen fehlen.");
                _rpgConfigJson.Text = JsonSerializer.Serialize(RpgEquipmentCatalog.Prepare(current), new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Sets & Legenden"); }
        };
        actions.Controls.AddRange(new Control[] { save, preset, dynamicItems, sets });
        panel.Controls.Add(actions, 0, 3);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddMinigameTab(tabs, "Einstellungen", panel);
        AddMinigameTab(tabs, "Spielerverwaltung", BuildRpgAdministrationPanel());
        return tabs;
    }
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
        var solo = ReferenceEquals(grid, _fishingSettings) || ReferenceEquals(grid, _treasureSettings);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = solo ? 5 : 4, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        if (solo) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.Controls.Add(new Label { Text = description + "\nBenötigt aktivierte Punkte und Minigames. Bestehende Tages- und Kontolimits gelten für Einsatzspiele.", Dock = DockStyle.Fill }, 0, 0);
        var switches = BuildAdventureSwitches(grid);
        grid.SelectedObjectsChanged += (_, _) => ApplyRaidClipTheme(switches);
        layout.Controls.Add(switches, 0, 1);
        if (solo)
        {
            var summary = new Label { Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(4), TextAlign = ContentAlignment.MiddleLeft };
            void UpdateSummary()
            {
                if (grid.SelectedObject is not SoloAdventureConfig settings) return;
                summary.Text = ChanceSummary(settings);
            }
            grid.PropertyValueChanged += (_, _) => UpdateSummary();
            grid.SelectedObjectsChanged += (_, _) => UpdateSummary();
            layout.Controls.Add(summary, 0, 2);
            UpdateSummary();
        }
        layout.Controls.Add(grid, 0, solo ? 3 : 2);
        var save = NewActionButton("Einstellungen speichern");
        save.Click += (_, _) => SaveMinigameSettingsFromControls();
        layout.Controls.Add(save, 0, solo ? 4 : 3);
        return layout;
    }
    internal static FlowLayoutPanel BuildAdventureSwitches(PropertyGrid grid)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        void Rebuild()
        {
            foreach (var old in panel.Controls.Cast<Control>().ToArray()) old.Dispose();
            if (grid.SelectedObject is not { } settings) return;
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(settings))
            {
                if (property.PropertyType != typeof(bool)) continue;
                var check = new CheckBox { Name = property.Name, Text = property.DisplayName, AutoSize = true,
                    Checked = (bool)(property.GetValue(settings) ?? false), Margin = new Padding(4, 8, 20, 4) };
                check.CheckedChanged += (_, _) => { property.SetValue(settings, check.Checked); grid.Refresh(); };
                panel.Controls.Add(check);
            }
        }
        grid.SelectedObjectsChanged += (_, _) => Rebuild();
        Rebuild();
        return panel;
    }
    internal static string ChanceSummary(SoloAdventureConfig settings)
    {
        var finds = (long)settings.SmallChance + settings.LargeChance + settings.RareChance;
        var total = finds + settings.LossChance;
        var balance = total == 100 ? "vollständig" : total < 100 ? $"noch {100 - total} frei" : $"{total - 100} zu viel";
        var lossBudget = finds <= 100 ? $"{100 - finds}/100" : $"0/100 (Fundstufen bereits {finds - 100} über 100)";
        return $"Gesamt vergeben: {total}/100 – {balance}\nFundstufen ohne Niete: {finds}/100 · Für Niete übrig: {lossBudget} · Niete aktuell: {settings.LossChance}/100";
    }
    private void LoadAdventureSettings(MinigameConfig config)
    {
        var copy = JsonSerializer.Deserialize<AdventureGamesConfig>(JsonSerializer.Serialize(config.Adventures))!;
        _rpgEnabled.Checked = copy.Rpg.Enabled;
        _rpgConfigJson.Text = JsonSerializer.Serialize(copy.Rpg, new JsonSerializerOptions { WriteIndented = true });
        _fishingSettings.SelectedObject = copy.Fishing;
        _treasureSettings.SelectedObject = copy.Treasure;
        _raceSettings.SelectedObject = copy.Race;
        _bossSettings.SelectedObject = copy.Boss;
    }
    private void ReadAdventureSettings(MinigameConfig config)
    {
        if (_fishingSettings.SelectedObject is not StakeAdventureConfig fishing) return;
        var rpg = JsonSerializer.Deserialize<RpgConfig>(_rpgConfigJson.Text) ?? throw new InvalidOperationException("RPG-Einstellungen fehlen.");
        rpg.Enabled = _rpgEnabled.Checked;
        rpg.Validate();
        config.Adventures = JsonSerializer.Deserialize<AdventureGamesConfig>(JsonSerializer.Serialize(new AdventureGamesConfig {
            Rpg = rpg,
            Fishing = (SoloAdventureConfig)fishing, Treasure = (DiveAdventureConfig)_treasureSettings.SelectedObject,
            Race = (RaceAdventureConfig)_raceSettings.SelectedObject, Boss = (BossAdventureConfig)_bossSettings.SelectedObject }))!;
    }
}
