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
    private readonly CheckBox _adBreakEnabledCheck = NewCheck(
        "Werbepausen erkennen", false);
    private readonly CheckBox _adBreakChatCheck = NewCheck(
        "Text im Chat senden", true);
    private readonly TextBox _adBreakChatMessageBox = new()
    {
        Width = 720, Height = 58, Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Text = "Werbepause für {duration} Sekunden – gleich geht es weiter!"
    };
    private readonly CheckBox _adBreakStreamerCheck = NewCheck(
        "Lokalen Streamer-Hinweis anzeigen", true);
    private readonly CheckBox _adBreakSoundCheck = NewCheck(
        "Hinweiston abspielen", true);
    private readonly TextBox _adBreakStreamerMessageBox = new()
    {
        Width = 720, Height = 58, Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Text = "Werbung gestartet: {duration} Sekunden ({type})."
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

        var adBreakPanel = BuildAdBreakSettingsPanel();
        var bodyTabs = new TabControl { Dock = DockStyle.Fill };
        var timerTab = new TabPage("Chat-Timer") { BackColor = BackgroundColor };
        var timerBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(4)
        };
        timerBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        timerBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        timerBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        timerBody.Controls.Add(hint, 0, 0);
        timerBody.Controls.Add(actions, 0, 1);
        timerBody.Controls.Add(_timerGrid, 0, 2);
        timerTab.Controls.Add(timerBody);
        var adTab = new TabPage("Werbepausen") { BackColor = BackgroundColor };
        adTab.Controls.Add(adBreakPanel);
        var eventTab = new TabPage("Event-Trigger") { BackColor = BackgroundColor };
        eventTab.Controls.Add(BuildEventTriggerSettingsPanel());
        bodyTabs.TabPages.Add(timerTab);
        bodyTabs.TabPages.Add(adTab);
        bodyTabs.TabPages.Add(eventTab);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(bodyTabs, 0, 1);
        _timerPage.Controls.Add(layout);
    }

    private Control BuildAdBreakSettingsPanel()
    {
        var flow = CreateMinigameFlow();
        flow.Controls.Add(_adBreakEnabledCheck);
        flow.Controls.Add(_adBreakChatCheck);
        flow.Controls.Add(CreateSettingEditor(
            "Chattext beim Werbestart", _adBreakChatMessageBox));
        flow.Controls.Add(_adBreakStreamerCheck);
        flow.Controls.Add(_adBreakSoundCheck);
        flow.Controls.Add(CreateSettingEditor(
            "Lokaler Hinweis für den Streamer", _adBreakStreamerMessageBox));
        flow.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            ForeColor = MutedTextColor,
            Text = "Platzhalter: {duration}, {minutes}, {type}, {automatic}, " +
                   "{time}, {requester}. Kein Whisper – der Streamer-Hinweis " +
                   "erscheint ausschließlich lokal in RaidClip."
        });
        var save = NewHeistActionButton("Einstellungen speichern", 210);
        save.Click += (_, _) => SaveSettingsFromControls();
        flow.Controls.Add(save);
        return flow;
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

    private void LoadAdBreakSettings(AdBreakNotificationConfig config)
    {
        _adBreakEnabledCheck.Checked = config.Enabled;
        _adBreakChatCheck.Checked = config.SendChatMessage;
        _adBreakChatMessageBox.Text = config.ChatMessage;
        _adBreakStreamerCheck.Checked = config.ShowStreamerNotification;
        _adBreakSoundCheck.Checked = config.PlaySound;
        _adBreakStreamerMessageBox.Text = config.StreamerMessage;
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

    private void ReadAdBreakSettings(AppConfig config)
    {
        config.AdBreakNotifications.Enabled = _adBreakEnabledCheck.Checked;
        config.AdBreakNotifications.SendChatMessage = _adBreakChatCheck.Checked;
        config.AdBreakNotifications.ChatMessage = _adBreakChatMessageBox.Text.Trim();
        config.AdBreakNotifications.ShowStreamerNotification =
            _adBreakStreamerCheck.Checked;
        config.AdBreakNotifications.PlaySound = _adBreakSoundCheck.Checked;
        config.AdBreakNotifications.StreamerMessage =
            _adBreakStreamerMessageBox.Text.Trim();
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

    private async Task HandleAdBreakStartedAsync(
        AdBreakEvent adBreak,
        AppConfig config,
        TwitchService twitch,
        string broadcasterId,
        string senderId,
        CancellationToken cancellationToken)
    {
        var settings = config.AdBreakNotifications;
        if (!settings.Enabled) return;

        if (settings.SendChatMessage)
        {
            try
            {
                await twitch.SendChatMessageAsync(
                    broadcasterId,
                    senderId,
                    FormatAdBreakMessage(settings.ChatMessage, adBreak),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                AppendLog("Werbepausen-Chattext fehlgeschlagen: " + exception.Message);
            }
        }

        var localMessage = FormatAdBreakMessage(
            settings.StreamerMessage,
            adBreak);
        AppendLog("Werbepause erkannt: " + localMessage);
        if (settings.ShowStreamerNotification)
            ShowAdBreakStreamerNotification(localMessage, settings.PlaySound);
    }

    private void ShowAdBreakStreamerNotification(string message, bool playSound)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
                ShowAdBreakStreamerNotification(message, playSound)));
            return;
        }

        _timerStatusLabel.Text = "● Werbung: " + message;
        _timerStatusLabel.ForeColor = WaitingColor;
        _trayIcon.ShowBalloonTip(
            10_000,
            "RaidClip · Werbepause",
            message,
            ToolTipIcon.Info);
        if (playSound)
            System.Media.SystemSounds.Exclamation.Play();
    }

    public static string FormatAdBreakMessage(
        string template,
        AdBreakEvent adBreak)
    {
        var type = adBreak.IsAutomatic ? "automatisch" : "manuell";
        var minutes = Math.Ceiling(adBreak.DurationSeconds / 60d);
        return (template ?? "")
            .Replace("{duration}", adBreak.DurationSeconds.ToString(),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{minutes}", minutes.ToString("0"),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{type}", type, StringComparison.OrdinalIgnoreCase)
            .Replace("{automatic}", adBreak.IsAutomatic ? "ja" : "nein",
                StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", adBreak.StartedAt.ToLocalTime().ToString("HH:mm:ss"),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{requester}", adBreak.RequesterName,
                StringComparison.OrdinalIgnoreCase);
    }
}
