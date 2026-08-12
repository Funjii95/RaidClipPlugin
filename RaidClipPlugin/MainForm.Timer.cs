using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Button _timerNavButton = CreateNavigationTile(
        "⏱  Timer",
        "Automatische Chatnachrichten");
    private readonly Panel _timerPage = new()
    {
        Dock = DockStyle.Fill,
        Visible = false
    };
    private readonly CheckBox _timerEnabledCheck = NewCheck(
        "Chat-Timer aktivieren",
        false);
    private readonly DataGridView _timerGrid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = SurfaceColor
    };
    private readonly Button _addTimerButton = NewHeistActionButton(
        "Timer hinzufügen", 170);
    private readonly Button _removeTimerButton = NewHeistActionButton(
        "Auswahl löschen", 160);
    private readonly Button _saveTimerButton = NewHeistActionButton(
        "Timer speichern", 170);
    private readonly Label _timerStatusLabel = new()
    {
        Text = "● Timer: Deaktiviert",
        AutoSize = true,
        ForeColor = InactiveColor,
        Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
        Padding = new Padding(4)
    };
    private ChatTimerService? _chatTimerService;
    private Task? _chatTimerTask;

    private void BuildTimerPage()
    {
        ConfigureTimerGrid();
        var title = new Label
        {
            Text = "Timer",
            AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = TextColor
        };
        var subtitle = new Label
        {
            Text = "Veröffentlicht Nachrichten automatisch nach frei wählbaren Intervallen.",
            AutoSize = true,
            ForeColor = MutedTextColor
        };
        var headerText = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        headerText.Controls.Add(title);
        headerText.Controls.Add(subtitle);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(headerText, 0, 0);
        header.Controls.Add(_timerStatusLabel, 1, 0);

        var hint = new Label
        {
            Text = "Jeder Eintrag besitzt ein eigenes Intervall. Mindestzuschauer 0 " +
                   "bedeutet: unabhängig von der aktuellen Zuschauerzahl posten. " +
                   "Der erste Versand erfolgt nach Ablauf des eingestellten Intervalls.",
            AutoSize = true,
            MaximumSize = new Size(1050, 0),
            ForeColor = MutedTextColor,
            Margin = new Padding(8)
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(8, 4, 8, 4)
        };
        actions.Controls.AddRange(new Control[]
        {
            _timerEnabledCheck,
            _addTimerButton,
            _removeTimerButton,
            _saveTimerButton
        });

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(hint, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        layout.Controls.Add(_timerGrid, 0, 3);
        _timerPage.Controls.Add(layout);
    }

    private void ConfigureTimerGrid()
    {
        if (_timerGrid.Columns.Count > 0) return;
        _timerGrid.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "Enabled", HeaderText = "Aktiv", Width = 60 });
        _timerGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Message",
            HeaderText = "Chattext",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _timerGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Interval", HeaderText = "Intervall (Min.)", Width = 130 });
        _timerGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "MinimumViewers", HeaderText = "Ab Zuschauer", Width = 125 });
    }

    private void InitializeTimerEvents()
    {
        _timerNavButton.Click += (_, _) => ShowSection("timer");
        _addTimerButton.Click += (_, _) => AddTimerRow();
        _removeTimerButton.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _timerGrid.SelectedRows)
                if (!row.IsNewRow) _timerGrid.Rows.Remove(row);
        };
        _saveTimerButton.Click += (_, _) => SaveSettingsFromControls();
    }

    private void AddTimerRow(ChatTimerEntryConfig? entry = null)
    {
        entry ??= new ChatTimerEntryConfig
        {
            Enabled = true,
            IntervalMinutes = 15,
            MinimumViewers = 0
        };
        var index = _timerGrid.Rows.Add(
            entry.Enabled,
            entry.Message,
            entry.IntervalMinutes,
            entry.MinimumViewers);
        _timerGrid.Rows[index].Selected = true;
    }

    private void LoadTimerSettings(ChatTimerConfig config)
    {
        _timerEnabledCheck.Checked = config.Enabled;
        _timerGrid.Rows.Clear();
        foreach (var entry in config.Entries)
            AddTimerRow(entry);
    }

    private void ReadTimerSettings(AppConfig config)
    {
        config.Timer.Enabled = _timerEnabledCheck.Checked;
        config.Timer.Entries = _timerGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => new ChatTimerEntryConfig
            {
                Enabled = row.Cells["Enabled"].Value is true,
                Message = row.Cells["Message"].Value?.ToString()?.Trim() ?? "",
                IntervalMinutes = ParseTimerNumber(row, "Interval"),
                MinimumViewers = ParseTimerNumber(row, "MinimumViewers")
            })
            .ToList();
    }

    private static int ParseTimerNumber(DataGridViewRow row, string column) =>
        int.TryParse(row.Cells[column].Value?.ToString(), out var value)
            ? value
            : -1;

    private void StartTimerModule(
        AppConfig config,
        TwitchService twitch,
        TwitchUser broadcaster,
        string senderId,
        CancellationToken cancellationToken)
    {
        if (!config.Timer.Enabled) return;
        _chatTimerService = new ChatTimerService(
            broadcaster.Id,
            senderId,
            config.Timer,
            twitch);
        _chatTimerService.StatusChanged += SetTimerStatus;
        _chatTimerService.MessageSent += (entry, viewers) =>
            AppendLog($"Timer-Nachricht gesendet ({viewers} Zuschauer): {entry.Message}");
        _chatTimerTask = _chatTimerService.RunAsync(cancellationToken);
    }

    private void SetTimerStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetTimerStatus(status)));
            return;
        }

        _timerStatusLabel.Text = "● Timer: " + status;
        _timerStatusLabel.ForeColor = status.StartsWith("Fehler", StringComparison.Ordinal)
            ? ErrorColor
            : status == "Deaktiviert"
                ? InactiveColor
                : ActiveColor;
    }
}
