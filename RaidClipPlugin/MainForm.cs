using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed class MainForm : Form
{
    private static readonly Color ActiveColor = Color.ForestGreen;
    private static readonly Color WaitingColor = Color.DarkOrange;
    private static readonly Color ErrorColor = Color.Firebrick;
    private static readonly Color InactiveColor = Color.DimGray;

    private readonly Button _startButton = new()
    {
        Text = "Plugin starten",
        AutoSize = true,
        Padding = new Padding(14, 7, 14, 7)
    };

    private readonly TextBox _testChannelBox = new()
    {
        PlaceholderText = "Twitch-Kanal, z. B. Funjii",
        Width = 260,
        Margin = new Padding(12, 10, 4, 0)
    };

    private readonly Button _testButton = new()
    {
        Text = "Clip testen",
        AutoSize = true,
        Enabled = false,
        Padding = new Padding(14, 7, 14, 7)
    };

    private readonly Button _stopButton = new()
    {
        Text = "Stoppen",
        AutoSize = true,
        Enabled = false,
        Padding = new Padding(14, 7, 14, 7)
    };

    private readonly Button _testConnectionsButton = new()
    {
        Text = "Verbindungen testen",
        AutoSize = true,
        Padding = new Padding(14, 7, 14, 7)
    };

    private readonly Button _createObsSourceButton = new()
    {
        Text = "OBS-Quelle erstellen",
        AutoSize = true,
        Padding = new Padding(14, 7, 14, 7)
    };

    private readonly Label _overallStatusLabel = new()
    {
        Text = "Bereit",
        AutoSize = true,
        ForeColor = Color.DimGray,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Margin = new Padding(12, 14, 0, 0)
    };

    private readonly TextBox _twitchChannelBox = new()
    {
        Width = 150,
        PlaceholderText = "z. B. Funjii"
    };

    private readonly TextBox _obsHostBox = new()
    {
        Width = 130,
        Text = "127.0.0.1"
    };

    private readonly NumericUpDown _obsPortControl = new()
    {
        Minimum = 1,
        Maximum = 65535,
        Value = 4455,
        Width = 90
    };

    private readonly TextBox _obsPasswordBox = new()
    {
        Width = 150,
        UseSystemPasswordChar = true
    };

    private readonly NumericUpDown _lookbackControl = new()
    {
        Minimum = 1,
        Maximum = 3650,
        Value = 365,
        Width = 90
    };

    private readonly NumericUpDown _retryControl = new()
    {
        Minimum = 1,
        Maximum = 10,
        Value = 3,
        Width = 70
    };

    private readonly NumericUpDown _durationControl = new()
    {
        Minimum = 1,
        Maximum = 600,
        Value = 30,
        Width = 80
    };

    private readonly NumericUpDown _volumeControl = new()
    {
        Minimum = 0,
        Maximum = 100,
        Increment = 5,
        Value = 100,
        Width = 80
    };

    private readonly NumericUpDown _cooldownControl = new()
    {
        Minimum = 0,
        Maximum = 1440,
        Value = 5,
        Width = 80
    };

    private readonly TextBox _blacklistBox = new()
    {
        Width = 280,
        PlaceholderText = "Clip-IDs, durch Komma getrennt"
    };

    private readonly CheckBox _sendRaidMessageCheck = new()
    {
        Text = "Chatnachricht nach Raid",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _sendShoutoutCheck = new()
    {
        Text = "Automatischer /shoutout",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _autoUpdateCheck = new()
    {
        Text = "Automatisch nach Updates suchen",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly TextBox _chatTemplateBox = new()
    {
        Width = 420,
        PlaceholderText = "{name}, {login} und {viewers} sind verfügbar"
    };


    private readonly CheckBox _moderationEnabledCheck = new()
    {
        Text = "Chat-Moderation aktivieren",
        AutoSize = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _chatLogCheck = new()
    {
        Text = "Chatnachrichten im Log",
        AutoSize = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _autoFilterCheck = new()
    {
        Text = "Wortfilter aktivieren",
        AutoSize = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _modVipWhitelistCheck = new()
    {
        Text = "Mods/VIPs vom Filter ausnehmen",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly NumericUpDown _moderationTimeoutControl = new()
    {
        Minimum = 1,
        Maximum = 1_209_600,
        Value = 600,
        Width = 100
    };

    private readonly TextBox _blockedWordsBox = new()
    {
        Width = 320,
        PlaceholderText = "Gesperrte Wörter, durch Komma getrennt"
    };

    private readonly Button _saveSettingsButton = new()
    {
        Text = "Einstellungen speichern",
        AutoSize = true,
        Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(10, 22, 4, 4)
    };

    private readonly Label _versionLabel = new()
    {
        Text = "Version 1.2.3\n🟢 Aktuell",
        AutoSize = true,
        ForeColor = Color.ForestGreen,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Margin = new Padding(8, 8, 8, 0)
    };

    private readonly Button _updateButton = new()
    {
        Text = "Nach Updates suchen",
        AutoSize = true,
        Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(4)
    };

    private readonly Button _changelogButton = new()
    {
        Text = "Changelog anzeigen",
        AutoSize = true,
        Visible = false,
        Padding = new Padding(8, 4, 8, 4),
        Margin = new Padding(4)
    };

    private readonly Button _installUpdateButton = new()
    {
        Text = "Update installieren",
        AutoSize = true,
        Visible = false,
        Padding = new Padding(8, 4, 8, 4),
        Margin = new Padding(4)
    };

    private readonly Button _skipUpdateButton = new()
    {
        Text = "Überspringen",
        AutoSize = true,
        Visible = false,
        Padding = new Padding(8, 4, 8, 4),
        Margin = new Padding(4)
    };

    private readonly GroupBox _settingsGroup = new()
    {
        Text = "Einstellungen",
        Dock = DockStyle.Fill,
        Padding = new Padding(10)
    };

    private readonly ConfigurationService _configurationService = new();
    private readonly UpdateService _updateService = new();
    private readonly FileLogService _fileLog = new();
    private readonly ClipHistoryService _history = new();
    private readonly RaidCooldownService _raidCooldown = new();

    private readonly Label _obsIndicator = CreateIndicator("OBS");
    private readonly Label _twitchIndicator = CreateIndicator("Twitch");
    private readonly Label _eventSubIndicator = CreateIndicator("EventSub");
    private readonly Label _playerIndicator = CreateIndicator("Player");

    private readonly TextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(24, 24, 28),
        ForeColor = Color.Gainsboro,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Consolas", 10F)
    };

    private readonly ListView _historyList = new()
    {
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        Dock = DockStyle.Fill,
        BackColor = Color.White
    };


    private readonly DataGridView _chatGrid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false,
        BackgroundColor = Color.White
    };

    private readonly Label _moderationStatusLabel = new()
    {
        Text = "● Chat-Moderation: Deaktiviert",
        AutoSize = true,
        ForeColor = InactiveColor,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Padding = new Padding(4)
    };

    private CancellationTokenSource? _shutdown;
    private LocalPlayerServer? _player;
    private ObsService? _obs;
    private PlaybackService? _playback;
    private TwitchUser? _broadcaster;
    private EventSubService? _eventSub;
    private ChatModerationService? _chatModeration;
    private Task? _chatModerationTask;
    private Task? _playerTask;
    private Task? _eventSubTask;
    private UpdateInfo? _availableUpdate;
    private bool _updateBusy;

    public MainForm()
    {
        Text = $"Raid Clip Plugin {_updateService.CurrentDisplayVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 760);
        Size = new Size(1250, 920);
        BackColor = Color.FromArgb(245, 246, 250);
        Font = new Font("Segoe UI", 10F);

        BuildLayout();

        _startButton.Click += async (_, _) => await StartPluginAsync();
        _testButton.Click += async (_, _) => await PlayTestClipAsync();
        _testChannelBox.KeyDown += async (_, args) =>
        {
            if (args.KeyCode == Keys.Enter && _testButton.Enabled)
            {
                args.SuppressKeyPress = true;
                await PlayTestClipAsync();
            }
        };
        _stopButton.Click += async (_, _) => await StopPluginAsync();
        _testConnectionsButton.Click += async (_, _) =>
            await TestConnectionsAsync();
        _createObsSourceButton.Click += async (_, _) =>
            await CreateObsSourceAsync();
        _saveSettingsButton.Click += (_, _) => SaveSettingsFromControls();
        _updateButton.Click += async (_, _) =>
            await CheckForUpdatesAsync(silent: false);
        _changelogButton.Click += (_, _) => ShowUpdateChangelog();
        _installUpdateButton.Click += async (_, _) =>
            await InstallAvailableUpdateAsync();
        _skipUpdateButton.Click += (_, _) => SkipAvailableUpdate();
        _chatGrid.CellContentClick += async (_, args) =>
            await HandleChatGridActionAsync(args.RowIndex, args.ColumnIndex);
        Shown += async (_, _) =>
        {
            try
            {
                var config = _configurationService.Load();
                if (config.Update.Enabled &&
                    !string.IsNullOrWhiteSpace(config.Update.ManifestUrl))
                {
                    await CheckForUpdatesAsync(silent: true);
                }
            }
            catch (Exception exception)
            {
                AppendLog("Automatische Update-Prüfung fehlgeschlagen: " + exception.Message);
            }
        };

        var writer = new GuiTextWriter(AppendLog);
        Console.SetOut(writer);
        Console.SetError(writer);

        _history.EntryAdded += AddHistoryEntry;

        foreach (var entry in _history.GetSnapshot().Reverse())
        {
            AddHistoryEntry(entry);
        }

        LoadSettingsIntoControls();
        AppendLog("Anwendung bereit. Klicke auf ‚Plugin starten‘.");
    }

    private static Label CreateIndicator(string service)
    {
        return new Label
        {
            Text = $"● {service}{Environment.NewLine}Nicht verbunden",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = InactiveColor,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(4)
        };
    }

    private void BuildLayout()
    {
        var title = new Label
        {
            Text = $"Raid Clip Plugin {_updateService.CurrentDisplayVersion}",
            AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 39, 47)
        };

        var subtitle = new Label
        {
            Text = "Twitch-Raids erkennen und Clips automatisch in OBS abspielen",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(2, 3, 0, 0)
        };

        var header = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(4, 0, 0, 0)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        _versionLabel.Text =
            $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}🟢 Aktuell";

        var updateActions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = Padding.Empty
        };
        updateActions.Controls.Add(_updateButton);
        updateActions.Controls.Add(_changelogButton);
        updateActions.Controls.Add(_installUpdateButton);
        updateActions.Controls.Add(_skipUpdateButton);

        var updatePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        updatePanel.Controls.Add(_versionLabel);
        updatePanel.Controls.Add(updateActions);

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerRow.Controls.Add(header, 0, 0);
        headerRow.Controls.Add(updatePanel, 1, 0);

        var indicators = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 2, 0, 4)
        };
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        indicators.Controls.Add(_obsIndicator, 0, 0);
        indicators.Controls.Add(_twitchIndicator, 1, 0);
        indicators.Controls.Add(_eventSubIndicator, 2, 0);
        indicators.Controls.Add(_playerIndicator, 3, 0);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 8)
        };
        actions.Controls.Add(_startButton);
        actions.Controls.Add(_testConnectionsButton);
        actions.Controls.Add(_createObsSourceButton);
        actions.Controls.Add(_testChannelBox);
        actions.Controls.Add(_testButton);
        actions.Controls.Add(_stopButton);
        actions.Controls.Add(_overallStatusLabel);

        var settingsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };
        settingsFlow.Controls.Add(
            CreateSettingEditor("Twitch-Kanal", _twitchChannelBox));
        settingsFlow.Controls.Add(
            CreateSettingEditor("OBS Host", _obsHostBox));
        settingsFlow.Controls.Add(
            CreateSettingEditor("OBS Port", _obsPortControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("OBS Passwort", _obsPasswordBox));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Clip-Lookback (Tage)", _lookbackControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Max. Retries", _retryControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Max. Clipdauer (Sek.)", _durationControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Lautstärke (%)", _volumeControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Raid-Cooldown (Min.)", _cooldownControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Clip-Blacklist", _blacklistBox));
        settingsFlow.Controls.Add(_sendRaidMessageCheck);
        settingsFlow.Controls.Add(_sendShoutoutCheck);
        settingsFlow.Controls.Add(_autoUpdateCheck);
        settingsFlow.Controls.Add(_moderationEnabledCheck);
        settingsFlow.Controls.Add(_chatLogCheck);
        settingsFlow.Controls.Add(_autoFilterCheck);
        settingsFlow.Controls.Add(_modVipWhitelistCheck);
        settingsFlow.Controls.Add(
            CreateSettingEditor("Moderations-Timeout (Sek.)", _moderationTimeoutControl));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Gesperrte Wörter", _blockedWordsBox));
        settingsFlow.Controls.Add(
            CreateSettingEditor("Raid-Chatnachricht", _chatTemplateBox));
        settingsFlow.Controls.Add(_saveSettingsButton);
        _settingsGroup.Controls.Add(settingsFlow);

        _historyList.Columns.Add("Uhrzeit", 90);
        _historyList.Columns.Add("Kanal", 140);
        _historyList.Columns.Add("Titel", 360);
        _historyList.Columns.Add("Status", 110);
        _historyList.Columns.Add("Clip-ID", 280);

        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Time",
            HeaderText = "Uhrzeit",
            Width = 80
        });
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "User",
            HeaderText = "Nutzer",
            Width = 150
        });
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Message",
            HeaderText = "Nachricht",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _chatGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Role",
            HeaderText = "Rolle",
            Width = 90
        });
        _chatGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Timeout",
            HeaderText = "",
            Text = "Timeout",
            UseColumnTextForButtonValue = true,
            Width = 80
        });
        _chatGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Ban",
            HeaderText = "",
            Text = "Ban",
            UseColumnTextForButtonValue = true,
            Width = 60
        });
        _chatGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Delete",
            HeaderText = "",
            Text = "Löschen",
            UseColumnTextForButtonValue = true,
            Width = 75
        });

        var logPage = new TabPage("Log");
        logPage.Controls.Add(_logBox);
        var historyPage = new TabPage("Clip-Historie");
        historyPage.Controls.Add(_historyList);
        var moderationPage = new TabPage("Chat-Moderation");
        moderationPage.Controls.Add(_chatGrid);
        moderationPage.Controls.Add(new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(4),
            Controls = { _moderationStatusLabel }
        });
        var detailsTabs = new TabControl
        {
            Dock = DockStyle.Fill
        };
        detailsTabs.TabPages.Add(logPage);
        detailsTabs.TabPages.Add(historyPage);
        detailsTabs.TabPages.Add(moderationPage);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(headerRow, 0, 0);
        layout.Controls.Add(indicators, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        layout.Controls.Add(_settingsGroup, 0, 3);
        layout.Controls.Add(detailsTabs, 0, 4);

        Controls.Add(layout);
    }

    private async Task CreateObsSourceAsync()
    {
        if (_createObsSourceButton.Enabled is false)
        {
            return;
        }

        _createObsSourceButton.Enabled = false;
        SetOverallStatus("Erstelle OBS-Quelle …", WaitingColor);
        AppendLog("Browserquelle wird in der aktiven OBS-Szene eingerichtet …");

        ObsService? temporaryObs = null;

        try
        {
            var config = ReadSettingsFromControls();
            _configurationService.SaveGuiSettings(config);

            var obs = _obs;
            if (obs is null || !obs.IsConnected)
            {
                temporaryObs = new ObsService(config);
                await Task.Run(temporaryObs.Connect);
                obs = temporaryObs;
            }

            var idleUrl = $"http://127.0.0.1:{config.Player.Port}/";
            var result = await Task.Run(() =>
                obs.EnsureBrowserSourceInCurrentScene(idleUrl));

            var action = result.CreatedInput
                ? "neu erstellt"
                : result.AddedToScene
                    ? "zur Szene hinzugefügt"
                    : "bereits vorhanden und aktualisiert";

            AppendLog(
                $"OBS-Quelle '{result.SourceName}' wurde in Szene " +
                $"'{result.SceneName}' {action}.");
            SetOverallStatus("OBS-Quelle bereit", ActiveColor);

            MessageBox.Show(
                $"Die Quelle '{result.SourceName}' ist jetzt in der aktiven " +
                $"Szene '{result.SceneName}' bereit.",
                "OBS-Quelle erstellt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            AppendLog("OBS-Quelle konnte nicht erstellt werden: " + exception.Message);
            SetOverallStatus("OBS-Quellenfehler", ErrorColor);
            MessageBox.Show(
                "Die OBS-Quelle konnte nicht erstellt werden. " +
                "Prüfe OBS-WebSocket, Host, Port und Passwort.",
                "OBS-Quelle nicht erstellt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            temporaryObs?.Dispose();
            _createObsSourceButton.Enabled = true;
        }
    }

    private async Task StartPluginAsync()
    {
        if (_shutdown is not null)
        {
            return;
        }

        _startButton.Enabled = false;
        SetOverallStatus("Wird gestartet …", WaitingColor);
        AppendLog("Plugin wird gestartet …");

        _shutdown = new CancellationTokenSource();
        var cancellationToken = _shutdown.Token;

        try
        {
            var config = ReadSettingsFromControls();
            _configurationService.SaveGuiSettings(config);
            _settingsGroup.Enabled = false;
            _testConnectionsButton.Enabled = false;

            SetServiceStatus(_playerIndicator, "Player", "Startet …", WaitingColor);
            _player = new LocalPlayerServer(config.Player.Port);
            _playerTask = _player.RunAsync(cancellationToken);
            SetServiceStatus(_playerIndicator, "Player", "Läuft", ActiveColor);

            SetServiceStatus(_obsIndicator, "OBS", "Verbindet …", WaitingColor);
            _obs = new ObsService(config);
            await Task.Run(_obs.Connect, cancellationToken);
            _obs.SetBrowserUrl(_player.IdleUrl);
            SetServiceStatus(_obsIndicator, "OBS", "Verbunden", ActiveColor);

            SetServiceStatus(_twitchIndicator, "Twitch", "Anmeldung …", WaitingColor);
            AppendLog("Prüfe Twitch-Anmeldung …");
            var session = await new AuthenticationService(config)
                .GetSessionAsync(cancellationToken);

            var twitch = new TwitchService(
                config.Twitch.ClientId,
                session.AccessToken);

            _broadcaster = await twitch.GetUserAsync(
                config.Twitch.BroadcasterLogin,
                cancellationToken);

            if (_broadcaster is null)
            {
                throw new InvalidOperationException(
                    "Der konfigurierte Twitch-Kanal wurde nicht gefunden.");
            }

            SetServiceStatus(_twitchIndicator, "Twitch", "Verbunden", ActiveColor);

            _playback = new PlaybackService(
                twitch,
                _obs,
                _player,
                config,
                _history);

            SetServiceStatus(_eventSubIndicator, "EventSub", "Startet …", WaitingColor);
            _eventSub = new EventSubService(
                config.Twitch.ClientId,
                session.AccessToken,
                _broadcaster.Id);

            _eventSub.Activated += () =>
                SetServiceStatus(_eventSubIndicator, "EventSub", "Aktiv", ActiveColor);

            _eventSub.RaidReceived += async raid =>
            {
                if (raid.Viewers < config.Twitch.MinimumRaidViewers)
                {
                    AppendLog(
                        $"Raid von {raid.FromBroadcasterName} mit " +
                        $"{raid.Viewers} Zuschauern ignoriert.");
                    return;
                }

                var cooldown = TimeSpan.FromMinutes(
                    config.Twitch.RaidCooldownMinutes);

                if (!_raidCooldown.TryAcquire(cooldown, out var remaining))
                {
                    AppendLog(
                        $"Raid von {raid.FromBroadcasterName} wegen Cooldown " +
                        $"ignoriert ({Math.Ceiling(remaining.TotalMinutes)} Min. verbleibend).");
                    return;
                }

                AppendLog(
                    $"Raid von {raid.FromBroadcasterName} mit " +
                    $"{raid.Viewers} Zuschauern erkannt.");

                await HandleRaidChatActionsAsync(
                    twitch,
                    config,
                    session,
                    _broadcaster,
                    raid,
                    cancellationToken);

                try
                {
                    await _playback!.PlayRandomClipAsync(
                        raid.FromBroadcasterId,
                        raid.FromBroadcasterName,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    AppendLog(
                        "Clip konnte nicht abgespielt werden: " +
                        exception.Message);
                }
            };

            _eventSubTask = _eventSub.RunAsync(cancellationToken);
            ObserveBackgroundTask(
                _eventSubTask!,
                "Twitch EventSub",
                _eventSubIndicator,
                "EventSub");
            ObserveBackgroundTask(
                _playerTask!,
                "Lokaler Player",
                _playerIndicator,
                "Player");

            if (config.Moderation.Enabled)
            {
                SetModerationStatus("Startet …", WaitingColor);
                _chatModeration = new ChatModerationService(
                    config.Twitch.ClientId,
                    session.AccessToken,
                    _broadcaster.Id,
                    session.UserId);
                _chatModeration.Activated += () =>
                    SetModerationStatus("Aktiv", ActiveColor);
                _chatModeration.MessageReceived += message =>
                    HandleChatMessageAsync(message, config, cancellationToken);
                _chatModerationTask =
                    _chatModeration.RunAsync(cancellationToken);
                ObserveChatModerationTask(_chatModerationTask);
            }
            else
            {
                SetModerationStatus("Deaktiviert", InactiveColor);
            }

            _testButton.Enabled = true;
            _stopButton.Enabled = true;
            SetOverallStatus("Aktiv", ActiveColor);
            AppendLog(
                $"Plugin aktiv für {_broadcaster.DisplayName}. " +
                "Warte auf eingehende Raids.");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Start wurde abgebrochen.");
            await StopPluginAsync();
        }
        catch (Exception exception)
        {
            AppendLog($"Start fehlgeschlagen: {exception.Message}");
            SetOverallStatus("Fehler", ErrorColor);
            await StopPluginAsync(keepErrorStatus: true);
        }
    }

    private async Task HandleRaidChatActionsAsync(
        TwitchService twitch,
        AppConfig config,
        TwitchSession session,
        TwitchUser broadcaster,
        RaidEvent raid,
        CancellationToken cancellationToken)
    {
        if (config.Chat.SendRaidMessage)
        {
            try
            {
                var message = config.Chat.RaidMessageTemplate
                    .Replace("{name}", raid.FromBroadcasterName)
                    .Replace("{login}", raid.FromBroadcasterLogin)
                    .Replace("{viewers}", raid.Viewers.ToString());

                await twitch.SendChatMessageAsync(
                    broadcaster.Id,
                    session.UserId,
                    message,
                    cancellationToken);
                AppendLog("Chatnachricht zum Raid wurde gesendet.");
            }
            catch (Exception exception)
            {
                AppendLog(
                    "Chatnachricht konnte nicht gesendet werden: " +
                    exception.Message);
            }
        }

        if (config.Chat.SendShoutout)
        {
            try
            {
                await twitch.SendShoutoutAsync(
                    broadcaster.Id,
                    raid.FromBroadcasterId,
                    session.UserId,
                    cancellationToken);
                AppendLog(
                    $"Offizieller /shoutout für @{raid.FromBroadcasterLogin} gesendet.");
            }
            catch (Exception exception)
            {
                AppendLog(
                    "Shoutout konnte nicht gesendet werden: " +
                    exception.Message);
            }
        }
    }


    private async Task HandleChatMessageAsync(
        ChatMessage message,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        AddChatMessage(message);

        if (config.Moderation.ShowMessagesInLog)
        {
            AppendLog($"[Chat] {message.UserName}: {message.Text}");
        }

        if (!config.Moderation.AutoFilterEnabled ||
            string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        if (config.Moderation.WhitelistModsAndVips && message.IsWhitelisted)
        {
            return;
        }

        var blockedWord = config.Moderation.BlockedWords.FirstOrDefault(word =>
            ContainsBlockedWord(message.Text, word));

        if (blockedWord is null || _chatModeration is null)
        {
            return;
        }

        try
        {
            await _chatModeration.DeleteMessageAsync(
                message.Id,
                cancellationToken);
            AppendLog(
                $"Wortfilter: Nachricht von {message.UserName} wegen " +
                $"‚{blockedWord}‘ gelöscht.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppendLog(
                "Wortfilter konnte Nachricht nicht löschen: " +
                exception.Message);
        }
    }

    private void AddChatMessage(ChatMessage message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => AddChatMessage(message)));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        var role = message.IsBroadcaster
            ? "Streamer"
            : message.IsModerator
                ? "Mod"
                : message.IsVip
                    ? "VIP"
                    : "Zuschauer";

        var index = _chatGrid.Rows.Add(
            message.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"),
            message.UserName,
            message.Text,
            role);
        _chatGrid.Rows[index].Tag = message;

        while (_chatGrid.Rows.Count > 250)
        {
            _chatGrid.Rows.RemoveAt(0);
        }

        if (_chatGrid.Rows.Count > 0)
        {
            _chatGrid.FirstDisplayedScrollingRowIndex =
                _chatGrid.Rows.Count - 1;
        }
    }

    private async Task HandleChatGridActionAsync(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || columnIndex < 0 ||
            rowIndex >= _chatGrid.Rows.Count ||
            _chatModeration is null)
        {
            return;
        }

        if (_chatGrid.Rows[rowIndex].Tag is not ChatMessage message)
        {
            return;
        }

        var columnName = _chatGrid.Columns[columnIndex].Name;
        if (columnName is not ("Timeout" or "Ban" or "Delete"))
        {
            return;
        }

        if (message.IsBroadcaster &&
            (columnName == "Timeout" || columnName == "Ban"))
        {
            AppendLog("Der eigene Broadcaster kann nicht moderiert werden.");
            return;
        }

        if (columnName == "Ban")
        {
            var confirmation = MessageBox.Show(
                $"{message.UserName} wirklich dauerhaft bannen?",
                "Ban bestätigen",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }
        }

        var cancellationToken = _shutdown?.Token ?? CancellationToken.None;

        try
        {
            switch (columnName)
            {
                case "Timeout":
                    await _chatModeration.TimeoutUserAsync(
                        message.UserId,
                        decimal.ToInt32(_moderationTimeoutControl.Value),
                        "Manueller Timeout über RaidClipPlugin",
                        cancellationToken);
                    AppendLog($"{message.UserName} wurde in Timeout gesetzt.");
                    break;

                case "Ban":
                    await _chatModeration.BanUserAsync(
                        message.UserId,
                        "Manueller Ban über RaidClipPlugin",
                        cancellationToken);
                    AppendLog($"{message.UserName} wurde gebannt.");
                    break;

                case "Delete":
                    await _chatModeration.DeleteMessageAsync(
                        message.Id,
                        cancellationToken);
                    AppendLog(
                        $"Nachricht von {message.UserName} wurde gelöscht.");
                    break;
            }

            _chatGrid.Rows[rowIndex].DefaultCellStyle.BackColor =
                Color.Honeydew;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppendLog(
                $"Moderationsaktion für {message.UserName} fehlgeschlagen: " +
                exception.Message);
            _chatGrid.Rows[rowIndex].DefaultCellStyle.BackColor =
                Color.MistyRose;
        }
    }

    private async void ObserveChatModerationTask(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
            when (_shutdown?.IsCancellationRequested == true)
        {
        }
        catch (Exception exception)
        {
            AppendLog(
                "Chat-Moderation wurde beendet: " + exception.Message);
            SetModerationStatus("Fehler", ErrorColor);
        }
    }

    private static bool ContainsBlockedWord(string message, string blockedWord)
    {
        if (string.IsNullOrWhiteSpace(blockedWord))
        {
            return false;
        }

        var start = 0;
        while (start < message.Length)
        {
            var index = message.IndexOf(
                blockedWord,
                start,
                StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var beforeIsBoundary = index == 0 ||
                !char.IsLetterOrDigit(message[index - 1]);
            var afterIndex = index + blockedWord.Length;
            var afterIsBoundary = afterIndex >= message.Length ||
                !char.IsLetterOrDigit(message[afterIndex]);

            if (beforeIsBoundary && afterIsBoundary)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }

    private void SetModerationStatus(string status, Color color)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => SetModerationStatus(status, color)));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        _moderationStatusLabel.Text = $"● Chat-Moderation: {status}";
        _moderationStatusLabel.ForeColor = color;
    }

    private async Task TestConnectionsAsync()
    {
        if (_shutdown is not null)
        {
            return;
        }

        _startButton.Enabled = false;
        _testConnectionsButton.Enabled = false;
        _settingsGroup.Enabled = false;
        SetOverallStatus("Teste Verbindungen …", WaitingColor);

        try
        {
            var config = ReadSettingsFromControls();
            _configurationService.SaveGuiSettings(config);

            TwitchSession? session = null;
            TwitchService? twitch = null;
            TwitchUser? broadcaster = null;

            await RunConnectionTestAsync(
                _playerIndicator,
                "Player",
                async () =>
                {
                    using var timeout = new CancellationTokenSource(
                        TimeSpan.FromSeconds(10));
                    using var player = new LocalPlayerServer(config.Player.Port);
                    var playerTask = player.RunAsync(timeout.Token);

                    try
                    {
                        using var http = new HttpClient();
                        using var response = await http.GetAsync(
                            player.IdleUrl,
                            timeout.Token);
                        response.EnsureSuccessStatusCode();
                    }
                    finally
                    {
                        timeout.Cancel();

                        try
                        {
                            await playerTask;
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                });

            await RunConnectionTestAsync(
                _obsIndicator,
                "OBS",
                async () =>
                {
                    await Task.Run(() =>
                    {
                        using var obs = new ObsService(config);
                        obs.Connect();
                    });
                });

            var twitchSucceeded = await RunConnectionTestAsync(
                _twitchIndicator,
                "Twitch",
                async () =>
                {
                    session = await new AuthenticationService(config)
                        .GetSessionAsync(CancellationToken.None);
                    twitch = new TwitchService(
                        config.Twitch.ClientId,
                        session.AccessToken);
                    broadcaster = await twitch.GetUserAsync(
                        config.Twitch.BroadcasterLogin,
                        CancellationToken.None);

                    if (broadcaster is null)
                    {
                        throw new InvalidOperationException(
                            "Der Twitch-Kanal wurde nicht gefunden.");
                    }
                });

            if (twitchSucceeded && session is not null && broadcaster is not null)
            {
                await RunConnectionTestAsync(
                    _eventSubIndicator,
                    "EventSub",
                    async () =>
                    {
                        var eventSub = new EventSubService(
                            config.Twitch.ClientId,
                            session.AccessToken,
                            broadcaster.Id);
                        await eventSub.TestConnectionAsync(
                            CancellationToken.None);
                    });
            }
            else
            {
                SetServiceStatus(
                    _eventSubIndicator,
                    "EventSub",
                    "Nicht getestet",
                    InactiveColor);
            }

            SetOverallStatus("Verbindungstest abgeschlossen", ActiveColor);
        }
        catch (Exception exception)
        {
            AppendLog("Verbindungstest abgebrochen: " + exception.Message);
            SetOverallStatus("Testfehler", ErrorColor);
        }
        finally
        {
            _settingsGroup.Enabled = true;
            _startButton.Enabled = true;
            _testConnectionsButton.Enabled = true;
        }
    }

    private async Task<bool> RunConnectionTestAsync(
        Label indicator,
        string name,
        Func<Task> test)
    {
        SetServiceStatus(indicator, name, "Test läuft …", WaitingColor);
        AppendLog($"Teste {name} …");

        try
        {
            await test();
            SetServiceStatus(indicator, name, "Test OK", ActiveColor);
            AppendLog($"{name}-Verbindung erfolgreich.");
            return true;
        }
        catch (Exception exception)
        {
            SetServiceStatus(indicator, name, "Test fehlgeschlagen", ErrorColor);
            AppendLog($"{name}-Test fehlgeschlagen: {exception.Message}");
            return false;
        }
    }

    private async Task PlayTestClipAsync()
    {
        if (_playback is null || _broadcaster is null || _shutdown is null)
        {
            return;
        }

        _testButton.Enabled = false;
        _testChannelBox.Enabled = false;

        var requestedLogin = _testChannelBox.Text
            .Trim()
            .TrimStart('@');

        try
        {
            bool success;

            if (string.IsNullOrWhiteSpace(requestedLogin))
            {
                AppendLog(
                    $"Testclip von {_broadcaster.DisplayName} wird gesucht …");
                success = await _playback.PlayRandomClipAsync(
                    _broadcaster,
                    _shutdown.Token);
            }
            else
            {
                AppendLog(
                    $"Testclip vom Kanal {requestedLogin} wird gesucht …");
                success = await _playback.PlayRandomClipAsync(
                    requestedLogin,
                    _shutdown.Token);
            }

            if (!success)
            {
                AppendLog("Es konnte kein Testclip abgespielt werden.");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("Testclip wurde abgebrochen.");
        }
        catch (Exception exception)
        {
            AppendLog($"Testclip fehlgeschlagen: {exception.Message}");
        }
        finally
        {
            _testChannelBox.Enabled = true;
            _testButton.Enabled = _shutdown is not null &&
                                  !_shutdown.IsCancellationRequested;
        }
    }

    private async Task StopPluginAsync(bool keepErrorStatus = false)
    {
        var shutdown = _shutdown;

        if (shutdown is null)
        {
            _startButton.Enabled = true;
            _settingsGroup.Enabled = true;
            _testConnectionsButton.Enabled = true;
            return;
        }

        _testButton.Enabled = false;
        _stopButton.Enabled = false;

        if (!keepErrorStatus)
        {
            SetOverallStatus("Wird gestoppt …", WaitingColor);
        }

        shutdown.Cancel();

        var tasks = new[] { _playerTask, _eventSubTask, _chatModerationTask }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(tasks)
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
                AppendLog("Ein Hintergrunddienst wurde verzögert beendet.");
            }
            catch (Exception exception)
            {
                AppendLog($"Fehler beim Stoppen: {exception.Message}");
            }
        }

        _chatModeration?.Dispose();
        _obs?.Dispose();
        _player?.Dispose();
        shutdown.Dispose();

        _shutdown = null;
        _player = null;
        _obs = null;
        _playback = null;
        _broadcaster = null;
        _eventSub = null;
        _chatModeration = null;
        _chatModerationTask = null;
        _playerTask = null;
        _eventSubTask = null;

        ResetServiceIndicators();
        SetModerationStatus("Deaktiviert", InactiveColor);
        _testChannelBox.Enabled = true;
        _startButton.Enabled = true;
        _settingsGroup.Enabled = true;
        _testConnectionsButton.Enabled = true;
        _raidCooldown.Reset();

        if (!keepErrorStatus)
        {
            SetOverallStatus("Gestoppt", InactiveColor);
        }

        AppendLog("Plugin wurde gestoppt.");
    }

    private async void ObserveBackgroundTask(
        Task task,
        string serviceName,
        Label indicator,
        string indicatorName)
    {
        try
        {
            await task;

            if (_shutdown is { IsCancellationRequested: false })
            {
                SetServiceStatus(
                    indicator,
                    indicatorName,
                    "Beendet",
                    ErrorColor);
                AppendLog($"{serviceName} wurde unerwartet beendet.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetServiceStatus(
                indicator,
                indicatorName,
                "Fehler",
                ErrorColor);
            AppendLog($"{serviceName} wurde beendet: {exception.Message}");
        }
    }

    private void ResetServiceIndicators()
    {
        SetServiceStatus(_obsIndicator, "OBS", "Nicht verbunden", InactiveColor);
        SetServiceStatus(_twitchIndicator, "Twitch", "Nicht verbunden", InactiveColor);
        SetServiceStatus(_eventSubIndicator, "EventSub", "Nicht aktiv", InactiveColor);
        SetServiceStatus(_playerIndicator, "Player", "Gestoppt", InactiveColor);
    }

    private void SetServiceStatus(
        Label indicator,
        string service,
        string state,
        Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
                SetServiceStatus(indicator, service, state, color)));
            return;
        }

        indicator.Text = $"● {service}{Environment.NewLine}{state}";
        indicator.ForeColor = color;
    }

    private static Control CreateSettingEditor(
        string labelText,
        Control editor)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(5, 2, 5, 2)
        };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(1, 0, 0, 2)
        });
        panel.Controls.Add(editor);
        return panel;
    }

    private async Task CheckForUpdatesAsync(bool silent)
    {
        if (_updateBusy)
        {
            return;
        }

        _updateBusy = true;
        SetUpdateControlsEnabled(false);
        _versionLabel.Text =
            $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}🔄 Suche …";
        _versionLabel.ForeColor = WaitingColor;

        try
        {
            var config = _configurationService.Load();

            if (string.IsNullOrWhiteSpace(config.Update.ManifestUrl))
            {
                _availableUpdate = null;
                ShowCurrentVersion();

                if (!silent)
                {
                    AppendLog(
                        "Auto-Update ist noch nicht mit der GitHub-update.json " +
                        "verbunden. Trage die Release-Asset-Adresse in " +
                        "Config/config.template.json ein.");
                }
                return;
            }

            _availableUpdate = await _updateService.CheckAsync(
                config.Update.ManifestUrl,
                CancellationToken.None);

            if (_availableUpdate is null)
            {
                ShowCurrentVersion();
                AppendLog("Die installierte Version ist aktuell.");
                return;
            }

            if (silent && config.Update.SkippedVersion.Equals(
                    _availableUpdate.DisplayVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                ShowSkippedVersion(_availableUpdate.DisplayVersion);
                AppendLog(
                    $"Version {_availableUpdate.DisplayVersion} wurde übersprungen.");
                return;
            }

            _versionLabel.Text =
                $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}" +
                $"🟡 Update verfügbar: {_availableUpdate.DisplayVersion}";
            _versionLabel.ForeColor = WaitingColor;
            ShowAvailableUpdateButtons();
            AppendLog(
                $"Update {_availableUpdate.DisplayVersion} ist verfügbar.");
        }
        catch (Exception exception)
        {
            _availableUpdate = null;
            _versionLabel.Text =
                $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}" +
                "🔴 Update-Prüfung fehlgeschlagen";
            _versionLabel.ForeColor = ErrorColor;
            HideAvailableUpdateButtons();
            _updateButton.Text = "Erneut prüfen";
            AppendLog("Update-Prüfung fehlgeschlagen: " + exception.Message);
        }
        finally
        {
            _updateBusy = false;
            SetUpdateControlsEnabled(true);
        }
    }

    private void ShowCurrentVersion()
    {
        _versionLabel.Text =
            $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}🟢 Aktuell";
        _versionLabel.ForeColor = ActiveColor;
        HideAvailableUpdateButtons();
        _updateButton.Text = "Nach Updates suchen";
    }

    private void ShowSkippedVersion(string version)
    {
        _versionLabel.Text =
            $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}" +
            $"⚪ Version {version} übersprungen";
        _versionLabel.ForeColor = InactiveColor;
        HideAvailableUpdateButtons();
        _updateButton.Text = "Trotzdem prüfen";
    }

    private void ShowAvailableUpdateButtons()
    {
        _updateButton.Visible = false;
        _changelogButton.Visible = true;
        _installUpdateButton.Visible = true;
        _skipUpdateButton.Visible = true;
    }

    private void HideAvailableUpdateButtons()
    {
        _updateButton.Visible = true;
        _changelogButton.Visible = false;
        _installUpdateButton.Visible = false;
        _skipUpdateButton.Visible = false;
    }

    private void SetUpdateControlsEnabled(bool enabled)
    {
        _updateButton.Enabled = enabled;
        _changelogButton.Enabled = enabled;
        _installUpdateButton.Enabled = enabled;
        _skipUpdateButton.Enabled = enabled;
    }

    private void ShowUpdateChangelog()
    {
        var update = _availableUpdate;
        if (update is null)
        {
            return;
        }

        MessageBox.Show(
            string.IsNullOrWhiteSpace(update.Changelog)
                ? "Für dieses Update wurde kein Changelog angegeben."
                : update.Changelog,
            $"Changelog – Version {update.DisplayVersion}",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SkipAvailableUpdate()
    {
        var update = _availableUpdate;
        if (update is null || _updateBusy)
        {
            return;
        }

        try
        {
            var config = ReadSettingsFromControls();
            config.Update.SkippedVersion = update.DisplayVersion;
            _configurationService.SaveGuiSettings(config);
            ShowSkippedVersion(update.DisplayVersion);
            AppendLog(
                $"Update {update.DisplayVersion} wurde übersprungen.");
        }
        catch (Exception exception)
        {
            AppendLog(
                "Update konnte nicht übersprungen werden: " +
                exception.Message);
        }
    }

    private async Task InstallAvailableUpdateAsync()
    {
        var update = _availableUpdate;
        if (update is null || _updateBusy)
        {
            return;
        }

        _updateBusy = true;
        SetUpdateControlsEnabled(false);

        try
        {
            var progress = new Progress<int>(percent =>
            {
                _versionLabel.Text =
                    $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}" +
                    $"🔄 Download {percent}%";
                _versionLabel.ForeColor = WaitingColor;
            });

            AppendLog(
                $"Update {update.DisplayVersion} wird heruntergeladen …");
            var stagedUpdate = await _updateService.DownloadAndStageAsync(
                update,
                progress,
                CancellationToken.None);
            AppendLog(
                "Download und SHA256-Prüfung erfolgreich. " +
                "Der separate Updater wird gestartet.");

            if (_shutdown is not null)
            {
                await StopPluginAsync();
            }

            UpdateService.StartUpdater(stagedUpdate);
            AppendLog(
                "Updater gestartet. RaidClip wird beendet und danach neu gestartet.");
            Application.Exit();
        }
        catch (Exception exception)
        {
            _versionLabel.Text =
                $"Version {_updateService.CurrentDisplayVersion}{Environment.NewLine}" +
                "🔴 Update fehlgeschlagen";
            _versionLabel.ForeColor = ErrorColor;
            AppendLog("Update fehlgeschlagen: " + exception.Message);
            MessageBox.Show(
                "Das Update konnte nicht installiert werden. Details stehen im Log.",
                "Update fehlgeschlagen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _updateBusy = false;
            SetUpdateControlsEnabled(true);
        }
    }

    private void LoadSettingsIntoControls()
    {
        try
        {
            var config = _configurationService.Load();
            _twitchChannelBox.Text = config.Twitch.BroadcasterLogin;
            _obsHostBox.Text = config.OBS.Host;
            _obsPasswordBox.Text = config.OBS.Password;
            SetNumericValue(_obsPortControl, config.OBS.Port);
            SetNumericValue(
                _lookbackControl,
                config.Twitch.ClipLookbackDays);
            SetNumericValue(
                _retryControl,
                config.Twitch.ClipRetryAttempts);
            SetNumericValue(
                _durationControl,
                config.Player.DurationSeconds);
            SetNumericValue(
                _volumeControl,
                config.Player.VolumePercent);
            SetNumericValue(
                _cooldownControl,
                config.Twitch.RaidCooldownMinutes);
            _blacklistBox.Text = string.Join(
                ", ",
                config.Player.BlacklistedClipIds);
            _sendRaidMessageCheck.Checked = config.Chat.SendRaidMessage;
            _sendShoutoutCheck.Checked = config.Chat.SendShoutout;
            _autoUpdateCheck.Checked = config.Update.Enabled;
            _moderationEnabledCheck.Checked = config.Moderation.Enabled;
            _chatLogCheck.Checked = config.Moderation.ShowMessagesInLog;
            _autoFilterCheck.Checked = config.Moderation.AutoFilterEnabled;
            _modVipWhitelistCheck.Checked =
                config.Moderation.WhitelistModsAndVips;
            SetNumericValue(
                _moderationTimeoutControl,
                config.Moderation.TimeoutSeconds);
            _blockedWordsBox.Text = string.Join(
                ", ",
                config.Moderation.BlockedWords);
            _chatTemplateBox.Text = config.Chat.RaidMessageTemplate;
        }
        catch (Exception exception)
        {
            AppendLog(
                "Einstellungen konnten nicht geladen werden: " +
                exception.Message);
        }
    }

    private AppConfig ReadSettingsFromControls()
    {
        var config = _configurationService.Load();
        config.Twitch.BroadcasterLogin = _twitchChannelBox.Text
            .Trim()
            .TrimStart('@');
        config.OBS.Host = _obsHostBox.Text.Trim();
        config.OBS.Port = decimal.ToInt32(_obsPortControl.Value);
        config.OBS.Password = _obsPasswordBox.Text;
        config.Twitch.ClipLookbackDays =
            decimal.ToInt32(_lookbackControl.Value);
        config.Twitch.ClipRetryAttempts =
            decimal.ToInt32(_retryControl.Value);
        config.Player.DurationSeconds =
            decimal.ToInt32(_durationControl.Value);
        config.Player.VolumePercent =
            decimal.ToInt32(_volumeControl.Value);
        config.Twitch.RaidCooldownMinutes =
            decimal.ToInt32(_cooldownControl.Value);
        config.Player.BlacklistedClipIds = _blacklistBox.Text
            .Split(
                new[] { ',', ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(id => id.Trim())
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.Chat.SendRaidMessage = _sendRaidMessageCheck.Checked;
        config.Chat.SendShoutout = _sendShoutoutCheck.Checked;
        config.Update.Enabled = _autoUpdateCheck.Checked;
        config.Moderation.Enabled = _moderationEnabledCheck.Checked;
        config.Moderation.ShowMessagesInLog = _chatLogCheck.Checked;
        config.Moderation.AutoFilterEnabled = _autoFilterCheck.Checked;
        config.Moderation.WhitelistModsAndVips =
            _modVipWhitelistCheck.Checked;
        config.Moderation.TimeoutSeconds =
            decimal.ToInt32(_moderationTimeoutControl.Value);
        config.Moderation.BlockedWords = _blockedWordsBox.Text
            .Split(
                new[] { ',', ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim())
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.Chat.RaidMessageTemplate = _chatTemplateBox.Text.Trim();
        return config;
    }

    private void SaveSettingsFromControls()
    {
        try
        {
            var config = ReadSettingsFromControls();
            _configurationService.SaveGuiSettings(config);
            AppendLog("Einstellungen wurden gespeichert.");
            SetOverallStatus("Einstellungen gespeichert", ActiveColor);
        }
        catch (Exception exception)
        {
            AppendLog(
                "Einstellungen konnten nicht gespeichert werden: " +
                exception.Message);
            SetOverallStatus("Einstellungsfehler", ErrorColor);
        }
    }

    private static void SetNumericValue(
        NumericUpDown control,
        int value)
    {
        control.Value = Math.Clamp(
            value,
            decimal.ToInt32(control.Minimum),
            decimal.ToInt32(control.Maximum));
    }

    private void AddHistoryEntry(ClipHistoryEntry entry)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AddHistoryEntry(entry)));
            return;
        }

        var item = new ListViewItem(
            entry.PlayedAt.ToLocalTime().ToString("HH:mm:ss"));
        item.SubItems.Add(entry.Channel);
        item.SubItems.Add(entry.Title);
        item.SubItems.Add(entry.Status);
        item.SubItems.Add(entry.ClipId);
        _historyList.Items.Insert(0, item);

        while (_historyList.Items.Count > 100)
        {
            _historyList.Items.RemoveAt(_historyList.Items.Count - 1);
        }
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => AppendLog(message)));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        _fileLog.WriteLine(message);
        _logBox.AppendText(
            $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void SetOverallStatus(string text, Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetOverallStatus(text, color)));
            return;
        }

        _overallStatusLabel.Text = text;
        _overallStatusLabel.ForeColor = color;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _shutdown?.Cancel();
        _obs?.Dispose();
        _player?.Dispose();
        base.OnFormClosing(e);
    }
}
