using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly TextBox _wheelEntries = new() { Multiline = true, Width = 650, Height = 180, ScrollBars = ScrollBars.Vertical };
    private readonly NumericUpDown _wheelSeconds = CreateIntegerControl(8, 3, 30);
    private readonly TextBox _wheelDesign = new() { Width = 500, ReadOnly = true };
    private readonly WheelService _wheel = new();
    private CancellationTokenSource? _wheelLifetime;
    private Task? _wheelServerTask;

    private Control BuildWheelPanel()
    {
        var flow = CreateMinigameFlow();
        flow.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(800, 0), Text = "Eigenständiges Glücksrad – unabhängig vom Giveaway und ohne Punkte-Einsatz. Ein Feld pro Zeile, 2–100 Felder (max. 80 Zeichen je Feld). Alle Felder gleich wahrscheinlich. Texte/Aktionen werden nur angezeigt, nicht automatisch ausgeführt." });
        flow.Controls.Add(CreateSettingEditor("Felder (ein Eintrag je Zeile)", _wheelEntries));
        flow.Controls.Add(CreateSettingEditor("Drehdauer (Sekunden)", _wheelSeconds));
        flow.Controls.Add(CreateSettingEditor("Eigenes Design (PNG/JPG, bis 10 MB)", _wheelDesign));
        var pick = NewActionButton("Design auswählen");
        pick.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Bilder (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", CheckFileExists = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try { WheelDesign.Load(dialog.FileName); _wheelDesign.Text = dialog.FileName; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Glücksrad-Design"); }
        };
        var remove = NewActionButton("Standarddesign"); remove.Click += (_, _) => _wheelDesign.Clear();
        var spin = NewActionButton("Glücksrad drehen");
        var clear = NewActionButton("Anzeige leeren");
        var source = NewActionButton("Browserquelle starten");
        var copy = NewActionButton("OBS-Adresse kopieren"); copy.Click += (_, _) => Clipboard.SetText(WheelOverlayServer.Url);
        var save = NewActionButton("Glücksrad speichern");
        save.Click += (_, _) =>
        {
            try
            {
                var wheel = ReadWheelSettings(); wheel.Validate();
                var config = _configurationService.LoadForEditing(); config.Giveaways.Wheel = wheel;
                _configurationService.SaveGuiSettings(config);
                AppendLog("Eigenständiges Glücksrad gespeichert.");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Glücksrad speichern"); }
        };
        var status = new Label { AutoSize = true, Text = "Glücksrad bereit.", MaximumSize = new Size(800, 0) };
        void StartSource()
        {
            if (_wheelServerTask is { IsCompleted: false }) return;
            _wheelLifetime?.Dispose(); _wheelLifetime = new();
            _wheelServerTask = new WheelOverlayServer(_wheel).RunAsync(_wheelLifetime.Token);
            ObserveWheelSource(_wheelServerTask);
            status.Text = "Browserquelle bereit: " + WheelOverlayServer.Url;
        }
        source.Click += (_, _) => { try { StartSource(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Glücksrad-Browserquelle"); } };
        spin.Click += (_, _) =>
        {
            try { StartSource(); _wheel.Spin(ReadWheelSettings()); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Glücksrad"); }
        };
        clear.Click += (_, _) => { try { _wheel.Reset(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Glücksrad"); } };
        var actions = new FlowLayoutPanel { Width = 800, Height = 95, WrapContents = true };
        actions.Controls.AddRange(new Control[] { pick, remove, save, source, spin, clear, copy });
        flow.Controls.Add(actions);
        flow.Controls.Add(status);
        flow.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(800, 0), Text = "OBS-Browserquelle: " + WheelOverlayServer.Url + " · 740 × 850, transparenter Hintergrund.\nStartet über Browserquelle starten oder Glücksrad drehen, ohne Twitch-Verbindung. Design wird mittig kreisförmig beschnitten und mitgedreht. Bilddatei am ausgewählten Ort behalten. Änderungen gelten ab der nächsten Drehung." });
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            var state = _wheel.State; var busy = _wheel.IsSpinning;
            spin.Enabled = !busy; clear.Enabled = !busy;
            if (state.SpinId.Length > 0) status.Text = busy ? "Das Rad dreht sich …" : "Ergebnis: " + state.Entries[state.WinnerIndex];
            else status.Text = _wheelServerTask is { IsCompleted: false } ? "Browserquelle bereit: " + WheelOverlayServer.Url : "Glücksrad bereit.";
        };
        timer.Start();
        flow.Disposed += (_, _) => { timer.Dispose(); _wheelLifetime?.Cancel(); _wheelLifetime?.Dispose(); };
        return flow;
    }
    private async void ObserveWheelSource(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppendLog("Glücksrad-Browserquelle: " + ex.Message); }
    }
    private WheelConfig ReadWheelSettings() => new() { Entries = _wheelEntries.Lines.Select(x => x.Trim()).Where(x => x.Length > 0).ToList(),
        SpinSeconds = (int)_wheelSeconds.Value, DesignImagePath = _wheelDesign.Text };
    private void LoadWheelSettings(WheelConfig? config)
    {
        config ??= new(); _wheelEntries.Lines = config.Entries?.ToArray() ?? Array.Empty<string>();
        SetNumericValue(_wheelSeconds, config.SpinSeconds); _wheelDesign.Text = config.DesignImagePath;
    }
}
