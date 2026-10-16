using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using RaidClipPlugin.Controls;

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
        var flow = new TableLayoutPanel { Name = "WheelLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        flow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        flow.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        flow.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Eigenständiges Glücksrad ohne Giveaway oder Punkte-Einsatz. 2–100 Felder, ein Text pro Zeile.\nAlle Felder sind gleich wahrscheinlich. Texte/Aktionen werden nur angezeigt, nicht ausgeführt." }, 0, 0);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(0, 0, 12, 0) };
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        editor.Controls.Add(new Label { Text = "Felder (ein Eintrag je Zeile)", Dock = DockStyle.Fill }, 0, 0);
        _wheelEntries.Dock = DockStyle.Fill; _wheelEntries.Name = "WheelEntries";
        editor.Controls.Add(_wheelEntries, 0, 1);
        var duration = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        duration.Controls.Add(new Label { AutoSize = true, Text = "Drehdauer (Sekunden)", Padding = new Padding(0, 6, 6, 0) }); duration.Controls.Add(_wheelSeconds);
        editor.Controls.Add(duration, 0, 2);
        editor.Controls.Add(new Label { Text = "Eigenes Bild (PNG/JPG, bis 10 MB)", Dock = DockStyle.Fill }, 0, 3);
        _wheelDesign.Dock = DockStyle.Fill; editor.Controls.Add(_wheelDesign, 0, 4);
        var preview = new WheelPreviewControl { Name = "WheelPreview", Dock = DockStyle.Fill };
        columns.Controls.Add(editor, 0, 0); columns.Controls.Add(preview, 1, 0); flow.Controls.Add(columns, 0, 1);
        var pick = NewActionButton("Bild auswählen …"); pick.Name = "WheelChooseImage";
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
        var status = new Label { Dock = DockStyle.Fill, Text = "Glücksrad bereit." };
        var imageError = "";
        void UpdateDraft() { preview.SetDraft(_wheelEntries.Lines); }
        void UpdateDesign()
        {
            try { preview.SetDesign(_wheelDesign.Text); imageError = ""; }
            catch (Exception ex) { preview.SetDesign(""); imageError = ex.Message; }
            UpdateDraft();
        }
        _wheelEntries.TextChanged += (_, _) => UpdateDraft();
        _wheelDesign.TextChanged += (_, _) => UpdateDesign();
        UpdateDraft(); UpdateDesign();
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
        var imageActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false };
        imageActions.Controls.AddRange(new Control[] { pick, remove }); editor.Controls.Add(imageActions, 0, 5);
        var actions = new FlowLayoutPanel { Name = "WheelActions", Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
        actions.Controls.AddRange(new Control[] { spin, clear, save, source, copy });
        flow.Controls.Add(actions, 0, 2);
        status.Text = "Vorschau bereit. OBS: " + WheelOverlayServer.Url;
        flow.Controls.Add(status, 0, 3);
        var lastSpinId = "";
        var timer = new System.Windows.Forms.Timer { Interval = 33 };
        timer.Tick += (_, _) =>
        {
            var state = _wheel.State; var busy = _wheel.IsSpinning;
            spin.Enabled = !busy; clear.Enabled = !busy;
            foreach (var control in new Control[] { pick, remove, _wheelEntries, _wheelSeconds }) control.Enabled = !busy;
            if (state.SpinId != lastSpinId) { preview.SetState(state); lastSpinId = state.SpinId; }
            preview.Invalidate();
            if (state.SpinId.Length > 0) status.Text = busy ? "Das Rad dreht sich …" : "Ergebnis: " + state.Entries[state.WinnerIndex];
            else status.Text = _wheelServerTask is { IsCompleted: false } ? "Browserquelle bereit: " + WheelOverlayServer.Url : "Glücksrad bereit.";
            status.Text += "\nOBS: " + WheelOverlayServer.Url + " · 740 × 850, transparenter Hintergrund. Bilddatei am ausgewählten Ort behalten.";
            if (imageError.Length > 0) status.Text += "\n" + imageError;
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
