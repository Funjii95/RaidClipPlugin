using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm : Form
{
    private static Color BackgroundColor = Color.FromArgb(8, 8, 9);
    private static Color SidebarColor = Color.FromArgb(12, 12, 13);
    private static Color SurfaceColor = Color.FromArgb(19, 19, 21);
    private static Color InputColor = Color.FromArgb(13, 13, 14);
    private static Color BorderColor = Color.FromArgb(86, 86, 90);
    private static Color AccentColor = Color.FromArgb(222, 24, 30);
    private static Color AccentDarkColor = Color.FromArgb(92, 12, 15);
    private static Color TextColor = Color.FromArgb(235, 235, 238);
    private static Color MutedTextColor = Color.FromArgb(174, 174, 180);
    private static Color ActiveColor = Color.FromArgb(239, 36, 42);
    private static readonly Color WaitingColor = Color.DarkOrange;
    private static readonly Color ErrorColor = Color.OrangeRed;
    private static readonly Color InactiveColor = Color.FromArgb(145, 145, 150);

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
        Text = "Version 1.4.2\n🟢 Aktuell",
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


    private readonly GroupBox _moderationSettingsGroup = new()
    {
        Text = "Chat-Moderation Einstellungen",
        Dock = DockStyle.Fill,
        Padding = new Padding(10)
    };

    private readonly Button _saveModerationSettingsButton = new()
    {
        Text = "Moderation speichern",
        AutoSize = true,
        Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(10, 22, 4, 4)
    };

    private readonly Button _raidClipNavButton = CreateNavigationTile(
        "🎬  Raid Clip",
        "Raids, Clips und OBS");

    private readonly Button _moderationNavButton = CreateNavigationTile(
        "🛡  Chat-Moderation",
        "Chat, Filter und Aktionen");

    private readonly Panel _raidPage = new()
    {
        Dock = DockStyle.Fill
    };

    private readonly Panel _moderationPage = new()
    {
        Dock = DockStyle.Fill,
        Visible = false
    };

    private readonly GroupBox _minigameSettingsGroup = new()
    {
        Text = "Chat-Minigame Einstellungen",
        Dock = DockStyle.Fill,
        Padding = new Padding(10)
    };

    private readonly CheckBox _minigameEnabledCheck = new()
    {
        Text = "Spiele aktivieren (!gamble, Roulette, Slots …)",
        AutoSize = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _pointsEnabledCheck = new()
    {
        Text = "Punktesystem aktivieren",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly CheckBox _gambleEnabledCheck = new()
    {
        Text = "!gamble aktivieren",
        AutoSize = true,
        Checked = true,
        Margin = new Padding(8, 24, 4, 4)
    };

    private readonly NumericUpDown _pointsPerIntervalControl =
        CreateIntegerControl(10, 0, 1_000_000);
    private readonly NumericUpDown _lurkerPointsPerIntervalControl =
        CreateIntegerControl(5, 0, 1_000_000);
    private readonly NumericUpDown _pointsIntervalControl =
        CreateIntegerControl(5, 1, 1440);
    private readonly NumericUpDown _minimumPointsControl =
        CreateIntegerControl(0, 0, 1_000_000_000);
    private readonly NumericUpDown _pointsCommandCooldownControl =
        CreateIntegerControl(30, 0, 3600);
    private readonly NumericUpDown _gambleCooldownControl =
        CreateIntegerControl(20, 0, 3600);
    private readonly NumericUpDown _globalCommandCooldownControl =
        CreateIntegerControl(2, 0, 3600);
    private readonly NumericUpDown _minimumBetControl =
        CreateIntegerControl(10, 0, 1_000_000_000);
    private readonly NumericUpDown _maximumBetControl =
        CreateIntegerControl(1000, 0, 1_000_000_000);
    private readonly TextBox _currencySingularBox = new()
        { Text = "Punkt", Width = 150, MaxLength = 30 };
    private readonly TextBox _currencyPluralBox = new()
        { Text = "Punkte", Width = 150, MaxLength = 30 };
    private readonly Label _currencyPreviewLabel = new()
        { AutoSize = true, Margin = new Padding(8, 24, 8, 4) };
    private readonly CheckBox _pointsCommandPunkteCheck =
        NewCheck("!punkte", true);
    private readonly CheckBox _pointsCommandPointsCheck =
        NewCheck("!points", false);
    private readonly CheckBox _pointsCommandPerlenCheck =
        NewCheck("!perlen", false);
    private readonly TextBox _customPointsCommandBox = new()
        { Width = 150, MaxLength = 30 };
    private readonly TextBox _pointsBlacklistInput = new()
        { Width = 180, MaxLength = 50 };
    private readonly ListBox _pointsBlacklistList = new()
        { Width = 220, Height = 115, IntegralHeight = false };
    private readonly Button _addPointsBlacklistButton =
        NewActionButton("Hinzufügen");
    private readonly Button _removePointsBlacklistButton =
        NewActionButton("Auswahl entfernen");

    private readonly CheckBox _chatPointsCheck = NewCheck(
        "Chatnachrichten-Punkte", true);
    private readonly NumericUpDown _chatPointsControl =
        CreateIntegerControl(1, 0, 1_000_000);
    private readonly NumericUpDown _chatPointsCooldownControl =
        CreateIntegerControl(60, 0, 3600);
    private readonly CheckBox _followPointsCheck = NewCheck(
        "Follow-Punkte", true);
    private readonly NumericUpDown _followPointsControl =
        CreateIntegerControl(50, 0, 1_000_000);
    private readonly CheckBox _subPointsCheck = NewCheck(
        "Sub-Punkte", true);
    private readonly NumericUpDown _subPointsControl =
        CreateIntegerControl(250, 0, 1_000_000);
    private readonly CheckBox _raidPointsCheck = NewCheck(
        "Raid-Punkte", true);
    private readonly NumericUpDown _raidPointsControl =
        CreateIntegerControl(100, 0, 1_000_000);
    private readonly CheckBox _rewardPointsCheck = NewCheck(
        "Channel-Reward-Punkte", true);
    private readonly NumericUpDown _rewardPointsControl =
        CreateIntegerControl(25, 0, 1_000_000);
    private readonly CheckBox _dailyCheck = NewCheck("!daily", true);
    private readonly NumericUpDown _dailyPointsControl =
        CreateIntegerControl(100, 0, 1_000_000);
    private readonly CheckBox _leaderboardCheck = NewCheck(
        "!top / !rang", true);
    private readonly NumericUpDown _maximumTopControl =
        CreateIntegerControl(10, 1, 100);
    private readonly NumericUpDown _leaderboardCooldownControl =
        CreateIntegerControl(30, 0, 3600);
    private readonly CheckBox _profileCheck = NewCheck("!profil", true);
    private readonly NumericUpDown _profileCooldownControl =
        CreateIntegerControl(30, 0, 3600);
    private readonly CheckBox _historyEnabledCheck = NewCheck(
        "Historie speichern", true);
    private readonly NumericUpDown _historyLimitControl =
        CreateIntegerControl(500, 1, 10000);
    private readonly CheckBox _coinflipEnabledCheck = NewCheck(
        "Coinflip aktivieren", false);
    private readonly NumericUpDown _coinflipMultiplierControl =
        CreateMultiplierControl(2m);
    private readonly NumericUpDown _coinflipMinControl =
        CreateIntegerControl(10, 0, 1_000_000_000);
    private readonly NumericUpDown _coinflipMaxControl =
        CreateIntegerControl(1000, 0, 1_000_000_000);
    private readonly NumericUpDown _coinflipCooldownControl =
        CreateIntegerControl(20, 0, 3600);
    private readonly CheckBox _slotsEnabledCheck = NewCheck(
        "Slots aktivieren", false);
    private readonly TextBox _slotSymbolsBox = new()
        { Text = "🍒,🍋,🔔,⭐,💎,7️⃣", Width = 230 };
    private readonly NumericUpDown _slotsThreeControl =
        CreateMultiplierControl(5m);
    private readonly NumericUpDown _slotsTwoControl =
        CreateMultiplierControl(1.5m);
    private readonly NumericUpDown _slotsSevenControl =
        CreateMultiplierControl(10m);
    private readonly NumericUpDown _slotsMinControl =
        CreateIntegerControl(10, 0, 1_000_000_000);
    private readonly NumericUpDown _slotsMaxControl =
        CreateIntegerControl(1000, 0, 1_000_000_000);
    private readonly NumericUpDown _slotsCooldownControl =
        CreateIntegerControl(20, 0, 3600);
    private readonly CheckBox _rouletteEnabledCheck = NewCheck(
        "Roulette aktivieren", false);
    private readonly NumericUpDown _rouletteEvenMoneyControl =
        CreateMultiplierControl(2m);
    private readonly NumericUpDown _rouletteNumberControl =
        CreateMultiplierControl(36m);
    private readonly NumericUpDown _rouletteMinControl =
        CreateIntegerControl(10, 0, 1_000_000_000);
    private readonly NumericUpDown _rouletteMaxControl =
        CreateIntegerControl(1000, 0, 1_000_000_000);
    private readonly NumericUpDown _rouletteCooldownControl =
        CreateIntegerControl(20, 0, 3600);
    private readonly CheckBox _jackpotEnabledCheck = NewCheck(
        "Jackpot aktivieren", false);
    private readonly NumericUpDown _jackpotStartControl =
        CreateIntegerControl(1000, 0, 1_000_000_000);
    private readonly NumericUpDown _jackpotContributionControl =
        CreateMultiplierControl(10m);
    private readonly Label _jackpotValueLabel = new()
        { Text = "Aktueller Jackpot: 1.000", AutoSize = true,
          Font = new Font("Segoe UI", 11F, FontStyle.Bold),
          Margin = new Padding(8, 24, 8, 8) };
    private readonly CheckBox _maximumAccountCheck = NewCheck(
        "Maximales Punktekonto", false);
    private readonly NumericUpDown _maximumAccountControl =
        CreateIntegerControl(1_000_000, 0, 1_000_000_000);
    private readonly CheckBox _dailyGamesCheck = NewCheck(
        "Tägliches Spielelimit", false);
    private readonly NumericUpDown _dailyGamesControl =
        CreateIntegerControl(100, 0, 1_000_000);
    private readonly CheckBox _dailyLossCheck = NewCheck(
        "Tägliches Verlustlimit", false);
    private readonly NumericUpDown _dailyLossControl =
        CreateIntegerControl(10000, 0, 1_000_000_000);
    private readonly CheckBox _dailyWinCheck = NewCheck(
        "Tägliches Gewinnlimit", false);
    private readonly NumericUpDown _dailyWinControl =
        CreateIntegerControl(10000, 0, 1_000_000_000);
    private readonly ListView _minigameTopList = NewDetailsList();
    private readonly ListView _minigameHistoryList = NewDetailsList();
    private readonly Button _exportMinigameButton = NewActionButton(
        "Daten exportieren");
    private readonly Button _importMinigameButton = NewActionButton(
        "Daten importieren");

    private readonly NumericUpDown[] _gambleFromControls =
        new[]
        {
            CreateIntegerControl(1, 1, 100, 65),
            CreateIntegerControl(32, 1, 100, 65),
            CreateIntegerControl(51, 1, 100, 65),
            CreateIntegerControl(71, 1, 100, 65)
        };

    private readonly NumericUpDown[] _gambleToControls =
        new[]
        {
            CreateIntegerControl(31, 1, 100, 65),
            CreateIntegerControl(50, 1, 100, 65),
            CreateIntegerControl(70, 1, 100, 65),
            CreateIntegerControl(100, 1, 100, 65)
        };

    private readonly NumericUpDown[] _gambleMultiplierControls =
        new[]
        {
            CreateMultiplierControl(0.0m),
            CreateMultiplierControl(0.5m),
            CreateMultiplierControl(1.0m),
            CreateMultiplierControl(2.0m)
        };

    private readonly TextBox[] _gambleTextBoxes =
        MinigameConfig.CreateDefaultRanges()
            .Select(range => new TextBox
            {
                Text = range.ChatText,
                Dock = DockStyle.Fill
            })
            .ToArray();

    private readonly Button _saveMinigameSettingsButton = new()
    {
        Text = "Minigame speichern",
        AutoSize = true,
        Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(10, 22, 4, 4)
    };

    private readonly Button _resetPointsButton = new()
    {
        Text = "Punktedaten zurücksetzen",
        AutoSize = true,
        Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(10, 22, 4, 4),
        BackColor = Color.MistyRose
    };

    private readonly Label _minigameStatusLabel = new()
    {
        Text = "● Minigame: Deaktiviert",
        AutoSize = true,
        ForeColor = InactiveColor,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Padding = new Padding(4)
    };

    private readonly Button _minigameNavButton = CreateNavigationTile(
        "🎲  Minigame",
        "Punkte, Commands und Gamble");

    private readonly Panel _minigamePage = new()
    {
        Dock = DockStyle.Fill,
        Visible = false
    };

    private readonly ConfigurationService _configurationService = new();
    private readonly UpdateService _updateService = new();
    private readonly FileLogService _fileLog = new();
    private readonly ClipHistoryService _history = new();
    private readonly RaidCooldownService _raidCooldown = new();
    private readonly ViewerPointStore _viewerPoints = new();

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
    private ChatMinigameService? _minigame;
    private MinigameEventService? _minigameEvents;
    private Task? _chatModerationTask;
    private Task? _minigameTask;
    private Task? _minigameEventTask;
    private Task? _playerTask;
    private Task? _eventSubTask;
    private UpdateInfo? _availableUpdate;
    private bool _updateBusy;
    private AppConfig? _activeConfig;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        try
        {
            var enabled = 1;
            const int immersiveDarkMode = 20;
            _ = DwmSetWindowAttribute(
                Handle,
                immersiveDarkMode,
                ref enabled,
                sizeof(int));
        }
        catch
        {
            // Ältere Windows-Versionen verwenden weiterhin ihre Standard-Titelleiste.
        }
    }

    public MainForm()
    {
        Text = $"Raid Clip Plugin {_updateService.CurrentDisplayVersion}";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                   Application.ExecutablePath) ??
               SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1280, 800);
        Size = new Size(1600, 960);
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        BuildLayout();
        InitializeMusicRequestEvents();
        InitializeStreamCheckEvents();
        InitializeClipDiscordEvents();
        InitializeGiveawayEvents();
        InitializeChatDiagnosticsEvents();
        InitializeThemeEvents();

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
        _saveModerationSettingsButton.Click += (_, _) => SaveSettingsFromControls();
        _saveMinigameSettingsButton.Click += (_, _) => SaveSettingsFromControls();
        _addPointsBlacklistButton.Click += (_, _) => AddPointsBlacklistEntry();
        _removePointsBlacklistButton.Click += (_, _) =>
            RemoveSelectedPointsBlacklistEntry();
        _currencySingularBox.TextChanged += (_, _) => UpdateCurrencyPreview();
        _currencyPluralBox.TextChanged += (_, _) => UpdateCurrencyPreview();
        _resetPointsButton.Click += async (_, _) => await ResetPointDataAsync();
        _exportMinigameButton.Click += async (_, _) =>
            await ExportMinigameDataAsync();
        _importMinigameButton.Click += async (_, _) =>
            await ImportMinigameDataAsync();
        _raidClipNavButton.Click += (_, _) => ShowSection("raid");
        _moderationNavButton.Click += (_, _) => ShowSection("moderation");
        _minigameNavButton.Click += (_, _) => ShowSection("minigame");
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
            BackColor = SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(4)
        };
    }

    private static Button CreateNavigationTile(string title, string subtitle)
    {
        return new Button
        {
            Text = $"{title}{Environment.NewLine}{subtitle}",
            Width = 242,
            Height = 88,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = TextColor,
            BackColor = SidebarColor,
            Padding = new Padding(18, 8, 12, 8),
            Margin = new Padding(5, 5, 5, 2),
            Cursor = Cursors.Hand
        };
    }

    private static CheckBox NewCheck(string text, bool isChecked) => new()
    {
        Text = text, AutoSize = true, Checked = isChecked,
        Margin = new Padding(8, 24, 8, 4)
    };

    private static ListView NewDetailsList() => new()
    {
        View = View.Details, FullRowSelect = true, GridLines = true,
        Dock = DockStyle.Fill, BackColor = Color.White
    };

    private static Button NewActionButton(string text) => new()
    {
        Text = text, AutoSize = true, Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(8)
    };

    private static NumericUpDown CreateIntegerControl(
        int value,
        int minimum,
        int maximum,
        int width = 90)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
            Width = width,
            ThousandsSeparator = true
        };
    }

    private static NumericUpDown CreateMultiplierControl(decimal value)
    {
        return new NumericUpDown
        {
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 2,
            Increment = 0.1m,
            Value = value,
            Width = 75
        };
    }

    private static Image LoadBrandImage()
    {
        using var stream = new MemoryStream(LogoAssets.GetPngBytes());
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private void ApplyRaidClipTheme(Control root)
    {
        if (root is Form or Panel)
        {
            root.BackColor = root.Name == "SidebarNavigation"
                ? SidebarColor
                : root.Name == "SurfacePanel"
                    ? SurfaceColor
                    : BackgroundColor;
        }

        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    StyleButton(button);
                    break;

                case TextBox textBox:
                    textBox.BackColor = InputColor;
                    textBox.ForeColor = TextColor;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox comboBox:
                    comboBox.BackColor = InputColor;
                    comboBox.ForeColor = TextColor;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;

                case NumericUpDown numeric:
                    numeric.BackColor = InputColor;
                    numeric.ForeColor = TextColor;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = TextColor;
                    checkBox.FlatStyle = FlatStyle.Flat;
                    checkBox.AutoCheck = true;
                    var checkTextSize = TextRenderer.MeasureText(
                        checkBox.Text, checkBox.Font,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding);
                    checkBox.MinimumSize = new Size(
                        checkTextSize.Width + 40,
                        Math.Max(26, checkTextSize.Height + 8));
                    checkBox.Padding = new Padding(0, 0, 12, 0);
                    checkBox.Paint -= DrawDarkCheckBox;
                    checkBox.Paint += DrawDarkCheckBox;
                    break;

                case ListBox listBox:
                    listBox.BackColor = InputColor;
                    listBox.ForeColor = TextColor;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case GroupBox groupBox:
                    groupBox.BackColor = BackgroundColor;
                    groupBox.ForeColor = AccentColor;
                    groupBox.FlatStyle = FlatStyle.Flat;
                    break;

                case TabControl tabs:
                    tabs.BackColor = BackgroundColor;
                    tabs.ForeColor = TextColor;
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tabs.SizeMode = TabSizeMode.Normal;
                    tabs.Padding = new Point(18, 7);
                    tabs.ItemSize = new Size(0, 36);
                    tabs.DrawItem -= DrawDarkTab;
                    tabs.DrawItem += DrawDarkTab;
                    break;

                case TabPage tabPage:
                    tabPage.BackColor = BackgroundColor;
                    tabPage.ForeColor = TextColor;
                    break;

                case ListView listView:
                    listView.BackColor = InputColor;
                    listView.ForeColor = TextColor;
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case DataGridView grid:
                    grid.BackgroundColor = InputColor;
                    grid.GridColor = Color.FromArgb(50, 50, 54);
                    grid.BorderStyle = BorderStyle.FixedSingle;
                    grid.EnableHeadersVisualStyles = false;
                    grid.ColumnHeadersDefaultCellStyle.BackColor =
                        Color.FromArgb(28, 28, 31);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
                    grid.DefaultCellStyle.BackColor = InputColor;
                    grid.DefaultCellStyle.ForeColor = TextColor;
                    grid.DefaultCellStyle.SelectionBackColor = AccentDarkColor;
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    break;

                case Label label:
                    if (ReferenceEquals(label, _obsIndicator) ||
                        ReferenceEquals(label, _twitchIndicator) ||
                        ReferenceEquals(label, _eventSubIndicator) ||
                        ReferenceEquals(label, _playerIndicator))
                    {
                        label.BackColor = SurfaceColor;
                    }
                    else if (ReferenceEquals(label, _overallStatusLabel) ||
                             ReferenceEquals(label, _moderationStatusLabel) ||
                             ReferenceEquals(label, _minigameStatusLabel) ||
                             ReferenceEquals(label, _versionLabel))
                    {
                        label.BackColor = Color.Transparent;
                    }
                    else
                    {
                        label.BackColor = Color.Transparent;
                        if (label.ForeColor == AccentColor)
                            label.ForeColor = AccentColor;
                        else if (label.Font.Size >= 16)
                            label.ForeColor = Color.White;
                        else if (label.ForeColor == Color.DimGray)
                            label.ForeColor = MutedTextColor;
                        else
                            label.ForeColor = TextColor;
                    }
                    break;

                case Panel:
                    control.BackColor = control.Name == "SidebarNavigation"
                        ? SidebarColor
                        : control.Name == "SurfacePanel"
                            ? SurfaceColor
                            : BackgroundColor;
                    break;
            }

            ApplyRaidClipTheme(control);
        }

        _versionLabel.ForeColor = ActiveColor;
        _jackpotValueLabel.ForeColor = AccentColor;
    }

    private static void DrawDarkCheckBox(object? sender, PaintEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;

        e.Graphics.Clear(checkBox.Parent?.BackColor ?? BackgroundColor);
        const int boxSize = 16;
        var box = new Rectangle(2, Math.Max(1, (checkBox.Height - boxSize) / 2),
            boxSize, boxSize);
        using var fill = new SolidBrush(
            checkBox.Checked ? AccentColor : InputColor);
        using var border = new Pen(
            checkBox.Focused ? Color.White : AccentColor, 1.4F);
        e.Graphics.FillRectangle(fill, box);
        e.Graphics.DrawRectangle(border, box);

        if (checkBox.Checked)
        {
            using var checkPen = new Pen(Color.White, 2F)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawLines(checkPen, new[]
            {
                new Point(box.Left + 3, box.Top + 8),
                new Point(box.Left + 7, box.Bottom - 4),
                new Point(box.Right - 3, box.Top + 4)
            });
        }

        var textArea = new Rectangle(box.Right + 7, 0,
            Math.Max(0, checkBox.Width - box.Right - 7), checkBox.Height);
        TextRenderer.DrawText(e.Graphics, checkBox.Text, checkBox.Font,
            textArea, checkBox.Enabled ? TextColor : MutedTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        if (checkBox.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, textArea);
    }

    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = SurfaceColor;
        button.ForeColor = TextColor;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.MouseOverBackColor = AccentDarkColor;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(125, 12, 17);
        button.AutoEllipsis = false;
        button.UseCompatibleTextRendering = false;
        button.MinimumSize = new Size(
            button.MinimumSize.Width, Math.Max(button.MinimumSize.Height, 40));
        button.Cursor = Cursors.Hand;
        button.Paint -= DrawRaidClipButton;
        button.Paint += DrawRaidClipButton;
        button.Invalidate();
    }

    private static void DrawRaidClipButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button) return;
        var bounds = button.ClientRectangle;
        var hovered = button.Enabled &&
            bounds.Contains(button.PointToClient(Cursor.Position));
        var pressed = hovered && button.Capture &&
            Control.MouseButtons.HasFlag(MouseButtons.Left);
        var background = !button.Enabled
            ? Color.FromArgb(31, 31, 34)
            : pressed
                ? Color.FromArgb(125, 12, 17)
                : hovered ? AccentDarkColor : button.BackColor;
        using var backgroundBrush = new SolidBrush(background);
        e.Graphics.FillRectangle(backgroundBrush, bounds);

        if (button.FlatAppearance.BorderSize > 0)
        {
            using var borderPen = new Pen(
                button.FlatAppearance.BorderColor,
                button.FlatAppearance.BorderSize);
            var border = Rectangle.Inflate(bounds, -1, -1);
            e.Graphics.DrawRectangle(borderPen, border);
        }

        var isNavigation = button.Width >= 220 && button.Height >= 70;
        var textBounds = Rectangle.FromLTRB(
            button.Padding.Left,
            button.Padding.Top,
            Math.Max(button.Padding.Left, button.Width - button.Padding.Right),
            Math.Max(button.Padding.Top, button.Height - button.Padding.Bottom));
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                    TextFormatFlags.WordBreak |
                    (isNavigation
                        ? TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                        : TextFormatFlags.HorizontalCenter |
                          TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, button.Text, button.Font,
            textBounds,
            button.Enabled ? button.ForeColor : Color.FromArgb(174, 174, 180),
            flags);
        if (button.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics,
                Rectangle.Inflate(textBounds, -2, -2));
    }

    private static void StylePrimaryButton(Button button)
    {
        button.BackColor = Color.FromArgb(38, 15, 17);
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = AccentColor;
    }

    private static void DrawDarkTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs ||
            e.Index < 0 || e.Index >= tabs.TabPages.Count)
        {
            return;
        }

        var selected = e.Index == tabs.SelectedIndex;
        var background = selected ? AccentDarkColor : SurfaceColor;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            tabs.TabPages[e.Index].Text,
            tabs.Font,
            e.Bounds,
            selected ? Color.White : MutedTextColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

        if (selected)
        {
            using var pen = new Pen(AccentColor, 2);
            e.Graphics.DrawLine(
                pen,
                e.Bounds.Left,
                e.Bounds.Bottom - 2,
                e.Bounds.Right,
                e.Bounds.Bottom - 2);
        }
    }

    private void ShowSection(string section)
    {
        var showRaid = section == "raid";
        var showModeration = section == "moderation";
        var showMinigame = section == "minigame";
        var showMusic = section == "music";
        var showStreamCheck = section == "stream-check";
        var showClipDiscord = section == "clip-discord";
        var showGiveaways = section == "giveaways";

        _raidPage.Visible = showRaid;
        _moderationPage.Visible = showModeration;
        _minigamePage.Visible = showMinigame;
        _musicPage.Visible = showMusic;
        _streamCheckPage.Visible = showStreamCheck;
        _clipDiscordPage.Visible = showClipDiscord;
        _giveawayPage.Visible = showGiveaways;

        if (showModeration)
            _moderationPage.BringToFront();
        else if (showMinigame)
            _minigamePage.BringToFront();
        else if (showMusic)
            _musicPage.BringToFront();
        else if (showStreamCheck)
            _streamCheckPage.BringToFront();
        else if (showClipDiscord)
            _clipDiscordPage.BringToFront();
        else if (showGiveaways)
            _giveawayPage.BringToFront();
        else
            _raidPage.BringToFront();

        SetNavigationTileState(_raidClipNavButton, showRaid);
        SetNavigationTileState(_moderationNavButton, showModeration);
        SetNavigationTileState(_minigameNavButton, showMinigame);
        SetNavigationTileState(_musicNavButton, showMusic);
        SetNavigationTileState(_streamCheckNavButton, showStreamCheck);
        SetNavigationTileState(_clipDiscordNavButton, showClipDiscord);
        SetNavigationTileState(_giveawayNavButton, showGiveaways);
        if (showMusic) _ = RefreshMusicGridAsync();
        if (showMinigame) _ = RefreshMinigameDashboardAsync();
    }

    private static void SetNavigationTileState(Button button, bool active)
    {
        button.BackColor = active ? AccentDarkColor : SidebarColor;
        button.ForeColor = active ? Color.White : TextColor;
        button.FlatAppearance.BorderSize = active ? 1 : 0;
        button.FlatAppearance.BorderColor = AccentColor;
    }

    private void BuildLayout()
    {
        var title = new Label
        {
            Text = "Raid Clip",
            AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = Padding.Empty
        };
        var titleAccent = new Label
        {
            Text = $" Plugin {_updateService.CurrentDisplayVersion}",
            AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = AccentColor,
            Margin = Padding.Empty
        };
        var titleRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        titleRow.Controls.Add(title);
        titleRow.Controls.Add(titleAccent);

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
        header.Controls.Add(titleRow);
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
            Name = "SurfacePanel",
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Dock = DockStyle.Fill,
            AutoSize = false,
            AutoScroll = true,
            Padding = new Padding(4, 6, 4, 4),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        updatePanel.Controls.Add(_versionLabel);
        updatePanel.Controls.Add(updateActions);

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerRow.Controls.Add(header, 0, 0);

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
            Name = "SurfacePanel",
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

        var raidSettingsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("UI-Theme", _uiThemeBox));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Twitch-Kanal", _twitchChannelBox));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("OBS Host", _obsHostBox));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("OBS Port", _obsPortControl));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("OBS Passwort", _obsPasswordBox));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Clip-Lookback (Tage)", _lookbackControl));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Max. Retries", _retryControl));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Max. Clipdauer (Sek.)", _durationControl));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Lautstärke (%)", _volumeControl));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Raid-Cooldown (Min.)", _cooldownControl));
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Clip-Blacklist", _blacklistBox));
        raidSettingsFlow.Controls.Add(_sendRaidMessageCheck);
        raidSettingsFlow.Controls.Add(_sendShoutoutCheck);
        raidSettingsFlow.Controls.Add(_autoUpdateCheck);
        raidSettingsFlow.Controls.Add(
            CreateSettingEditor("Raid-Chatnachricht", _chatTemplateBox));
        raidSettingsFlow.Controls.Add(_saveSettingsButton);
        _settingsGroup.Controls.Add(raidSettingsFlow);

        var moderationSettingsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };
        moderationSettingsFlow.Controls.Add(_moderationEnabledCheck);
        moderationSettingsFlow.Controls.Add(_chatLogCheck);
        moderationSettingsFlow.Controls.Add(_autoFilterCheck);
        moderationSettingsFlow.Controls.Add(_modVipWhitelistCheck);
        moderationSettingsFlow.Controls.Add(
            CreateSettingEditor(
                "Timeout-Dauer (Sek.)",
                _moderationTimeoutControl));
        moderationSettingsFlow.Controls.Add(
            CreateSettingEditor("Gesperrte Wörter", _blockedWordsBox));
        moderationSettingsFlow.Controls.Add(_saveModerationSettingsButton);
        _moderationSettingsGroup.Controls.Add(moderationSettingsFlow);

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
        var raidTabs = new TabControl
        {
            Dock = DockStyle.Fill
        };
        raidTabs.TabPages.Add(logPage);
        raidTabs.TabPages.Add(historyPage);

        var raidLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20)
        };
        raidLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        raidLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        raidLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        raidLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        raidLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        raidLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raidLayout.Controls.Add(headerRow, 0, 0);
        raidLayout.Controls.Add(updatePanel, 0, 1);
        raidLayout.Controls.Add(indicators, 0, 2);
        raidLayout.Controls.Add(actions, 0, 3);
        raidLayout.Controls.Add(_settingsGroup, 0, 4);
        raidLayout.Controls.Add(raidTabs, 0, 5);
        _raidPage.Controls.Add(raidLayout);

        var moderationTitle = new Label
        {
            Text = "Chat-Moderation",
            AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 39, 47)
        };
        var moderationSubtitle = new Label
        {
            Text = "Chat überwachen, Nachrichten löschen und Nutzer moderieren",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(2, 3, 0, 0)
        };
        var moderationHeaderText = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 0, 0, 0)
        };
        moderationHeaderText.Controls.Add(moderationTitle);
        moderationHeaderText.Controls.Add(moderationSubtitle);

        var moderationHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        moderationHeader.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));
        moderationHeader.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));
        moderationHeader.Controls.Add(moderationHeaderText, 0, 0);
        moderationHeader.Controls.Add(_moderationStatusLabel, 1, 0);

        var moderationHint = new Label
        {
            Text = "Moderation läuft unabhängig von Raid-Clips. " +
                   "Der Wortfilter löscht Treffer, führt aber keine automatischen Bans aus.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Padding = new Padding(8, 0, 0, 0)
        };

        BuildChatDiagnosticsPanel();
        var moderationLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20)
        };
        moderationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        moderationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        moderationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        moderationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 235));
        moderationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        moderationLayout.Controls.Add(moderationHeader, 0, 0);
        moderationLayout.Controls.Add(moderationHint, 0, 1);
        moderationLayout.Controls.Add(_moderationSettingsGroup, 0, 2);
        moderationLayout.Controls.Add(_chatDiagnosticsGroup, 0, 3);
        moderationLayout.Controls.Add(_chatGrid, 0, 4);
        _moderationPage.Controls.Add(moderationLayout);

        var minigameTitle = new Label
        {
            Text = "Chat-Minigame",
            AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 39, 47)
        };
        var minigameSubtitle = new Label
        {
            Text = "Punkte, !give, !gamble all und Mod-Commands",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(2, 3, 0, 0)
        };
        var minigameHeaderText = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 0, 0, 0)
        };
        minigameHeaderText.Controls.Add(minigameTitle);
        minigameHeaderText.Controls.Add(minigameSubtitle);

        var minigameHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        minigameHeader.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));
        minigameHeader.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));
        minigameHeader.Controls.Add(minigameHeaderText, 0, 0);
        minigameHeader.Controls.Add(_minigameStatusLabel, 1, 0);

        var overviewFlow = CreateMinigameFlow();
        overviewFlow.Controls.Add(_minigameEnabledCheck);
        overviewFlow.Controls.Add(_pointsEnabledCheck);
        overviewFlow.Controls.Add(_jackpotValueLabel);
        overviewFlow.Controls.Add(_saveMinigameSettingsButton);

        var pointsFlow = CreateMinigameFlow();
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Währung – Einzahl", _currencySingularBox));
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Währung – Mehrzahl", _currencyPluralBox));
        pointsFlow.Controls.Add(_currencyPreviewLabel);
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Aktive Zuschauer pro Intervall", _pointsPerIntervalControl));
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Stille Zuschauer/Lurker pro Intervall",
            _lurkerPointsPerIntervalControl));
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Intervall (Min.)", _pointsIntervalControl));
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Mindestpunkte", _minimumPointsControl));
        pointsFlow.Controls.Add(_chatPointsCheck);
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Punkte je Chatnachricht", _chatPointsControl));
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Chat-Cooldown (Sek.)", _chatPointsCooldownControl));
        pointsFlow.Controls.Add(_followPointsCheck);
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Punkte je Follow", _followPointsControl));
        pointsFlow.Controls.Add(_subPointsCheck);
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Punkte je Sub", _subPointsControl));
        pointsFlow.Controls.Add(_raidPointsCheck);
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Punkte je Raid", _raidPointsControl));
        pointsFlow.Controls.Add(_rewardPointsCheck);
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Punkte je Channel Reward", _rewardPointsControl));
        pointsFlow.Controls.Add(CreateSettingEditor(
            "Punkte-Blacklist", _pointsBlacklistInput));
        pointsFlow.Controls.Add(_addPointsBlacklistButton);
        pointsFlow.Controls.Add(_pointsBlacklistList);
        pointsFlow.Controls.Add(_removePointsBlacklistButton);
        var savePointsButton = NewActionButton("Einstellungen speichern");
        savePointsButton.Click += (_, _) => SaveSettingsFromControls();
        pointsFlow.Controls.Add(savePointsButton);

        var commandsFlow = CreateMinigameFlow();
        commandsFlow.Controls.Add(new Label
        {
            Text = "Commands für Punkteabfrage", AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(8, 24, 8, 4)
        });
        commandsFlow.Controls.Add(_pointsCommandPunkteCheck);
        commandsFlow.Controls.Add(_pointsCommandPointsCheck);
        commandsFlow.Controls.Add(_pointsCommandPerlenCheck);
        commandsFlow.Controls.Add(CreateSettingEditor(
            "Eigener Command", _customPointsCommandBox));
        commandsFlow.Controls.Add(_dailyCheck);
        commandsFlow.Controls.Add(CreateSettingEditor(
            "Daily-Bonus", _dailyPointsControl));
        commandsFlow.Controls.Add(CreateSettingEditor(
            "!punkte Cooldown", _pointsCommandCooldownControl));
        commandsFlow.Controls.Add(_leaderboardCheck);
        commandsFlow.Controls.Add(CreateSettingEditor(
            "Maximale Top-Anzahl", _maximumTopControl));
        commandsFlow.Controls.Add(CreateSettingEditor(
            "Ranglisten-Cooldown", _leaderboardCooldownControl));
        commandsFlow.Controls.Add(_profileCheck);
        commandsFlow.Controls.Add(CreateSettingEditor(
            "Profil-Cooldown", _profileCooldownControl));
        commandsFlow.Controls.Add(CreateSettingEditor(
            "Globaler Cooldown", _globalCommandCooldownControl));
        var saveCommandsButton = NewActionButton("Einstellungen speichern");
        saveCommandsButton.Click += (_, _) => SaveSettingsFromControls();
        commandsFlow.Controls.Add(saveCommandsButton);

        var rangeTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5,
            Padding = new Padding(4), AutoScroll = true
        };
        rangeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        rangeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        rangeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        rangeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var heading in new[] { "Von", "Bis", "Multiplikator", "Chattext" })
        {
            rangeTable.Controls.Add(new Label
            {
                Text = heading, Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            }, rangeTable.Controls.Count, 0);
        }
        for (var index = 0; index < 4; index++)
        {
            rangeTable.Controls.Add(_gambleFromControls[index], 0, index + 1);
            rangeTable.Controls.Add(_gambleToControls[index], 1, index + 1);
            rangeTable.Controls.Add(
                _gambleMultiplierControls[index], 2, index + 1);
            rangeTable.Controls.Add(_gambleTextBoxes[index], 3, index + 1);
        }

        var casinoFlow = CreateMinigameFlow();
        casinoFlow.Controls.Add(_gambleEnabledCheck);
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Gamble Min.", _minimumBetControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Gamble Max.", _maximumBetControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Gamble-Cooldown", _gambleCooldownControl));
        casinoFlow.Controls.Add(_coinflipEnabledCheck);
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Coinflip Multiplikator", _coinflipMultiplierControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Coinflip Min.", _coinflipMinControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Coinflip Max.", _coinflipMaxControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Coinflip-Cooldown", _coinflipCooldownControl));
        casinoFlow.Controls.Add(_slotsEnabledCheck);
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Slot-Symbole", _slotSymbolsBox));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "3 gleiche", _slotsThreeControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "2 gleiche", _slotsTwoControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "3 × 7", _slotsSevenControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Slots Min.", _slotsMinControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Slots Max.", _slotsMaxControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Slots-Cooldown", _slotsCooldownControl));
        casinoFlow.Controls.Add(_rouletteEnabledCheck);
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Roulette 1:1 Multiplikator", _rouletteEvenMoneyControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Roulette Zahl Multiplikator", _rouletteNumberControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Roulette Min.", _rouletteMinControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Roulette Max.", _rouletteMaxControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Roulette-Cooldown", _rouletteCooldownControl));
        casinoFlow.Controls.Add(_jackpotEnabledCheck);
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Jackpot-Startwert", _jackpotStartControl));
        casinoFlow.Controls.Add(CreateSettingEditor(
            "Anteil Verluste (%)", _jackpotContributionControl));

        var casinoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1
        };
        casinoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        casinoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        casinoLayout.Controls.Add(casinoFlow, 0, 0);
        casinoLayout.Controls.Add(rangeTable, 0, 1);

        _minigameTopList.Columns.Add("Rang", 75);
        _minigameTopList.Columns.Add("Nutzer", 260);
        _minigameTopList.Columns.Add("Punkte", 140);
        _minigameTopList.Columns.Add("Watchtime", 140);
        _minigameHistoryList.Columns.Add("Zeit", 145);
        _minigameHistoryList.Columns.Add("Nutzer", 170);
        _minigameHistoryList.Columns.Add("Spiel", 110);
        _minigameHistoryList.Columns.Add("Aktion", 280);
        _minigameHistoryList.Columns.Add("Änderung", 100);
        _minigameHistoryList.Columns.Add("Stand", 100);

        var historyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1
        };
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var historyOptions = CreateMinigameFlow();
        historyOptions.AutoScroll = false;
        historyOptions.Controls.Add(_historyEnabledCheck);
        historyOptions.Controls.Add(CreateSettingEditor(
            "Max. Einträge", _historyLimitControl));
        historyOptions.Controls.Add(_exportMinigameButton);
        historyOptions.Controls.Add(_importMinigameButton);
        historyOptions.Controls.Add(_resetPointsButton);
        historyLayout.Controls.Add(historyOptions, 0, 0);
        historyLayout.Controls.Add(_minigameHistoryList, 0, 1);

        var limitsFlow = CreateMinigameFlow();
        limitsFlow.Controls.Add(_maximumAccountCheck);
        limitsFlow.Controls.Add(CreateSettingEditor(
            "Maximale Kontopunkte", _maximumAccountControl));
        limitsFlow.Controls.Add(_dailyGamesCheck);
        limitsFlow.Controls.Add(CreateSettingEditor(
            "Spiele pro Tag", _dailyGamesControl));
        limitsFlow.Controls.Add(_dailyLossCheck);
        limitsFlow.Controls.Add(CreateSettingEditor(
            "Verlust pro Tag", _dailyLossControl));
        limitsFlow.Controls.Add(_dailyWinCheck);
        limitsFlow.Controls.Add(CreateSettingEditor(
            "Gewinn pro Tag", _dailyWinControl));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddMinigameTab(tabs, "Übersicht", overviewFlow);
        AddMinigameTab(tabs, "Punkte & Währung", pointsFlow);
        AddMinigameTab(tabs, "Chat-Commands", commandsFlow);
        AddMinigameTab(tabs, "Casino-Spiele", casinoLayout);
        AddMinigameTab(tabs, "Limits & Fairness", limitsFlow);
        AddMinigameTab(tabs, "Rangliste", _minigameTopList);
        AddMinigameTab(tabs, "Historie & Daten", historyLayout);
        _minigameSettingsGroup.Controls.Add(tabs);

        var minigameHint = new Label
        {
            Text = "Aktive Zuschauer erhalten den normalen Satz. Stille Chatnutzer " +
                   "und Nutzer mit !lurk erhalten den Lurker-Satz; mit !unlurk " +
                   "wechseln sie zurück.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Padding = new Padding(8, 0, 0, 0)
        };

        var minigameLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        minigameLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        minigameLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        minigameLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        minigameLayout.Controls.Add(minigameHeader, 0, 0);
        minigameLayout.Controls.Add(minigameHint, 0, 1);
        minigameLayout.Controls.Add(_minigameSettingsGroup, 0, 2);
        _minigamePage.Controls.Add(minigameLayout);
        BuildMusicRequestPage();
        BuildStreamCheckPage();
        BuildClipDiscordPage();
        BuildGiveawayPage();

        var brand = new PictureBox
        {
            Image = LoadBrandImage(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 238,
            Height = 205,
            Margin = new Padding(0, 4, 0, 14),
            BackColor = SidebarColor
        };

        var navigation = new FlowLayoutPanel
        {
            Name = "SidebarNavigation",
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = SidebarColor,
            Padding = new Padding(6),
            AutoScroll = true
        };
        navigation.Controls.Add(brand);
        navigation.Controls.Add(_raidClipNavButton);
        navigation.Controls.Add(_moderationNavButton);
        navigation.Controls.Add(_minigameNavButton);
        navigation.Controls.Add(_musicNavButton);
        navigation.Controls.Add(_clipDiscordNavButton);
        navigation.Controls.Add(_giveawayNavButton);
        navigation.Controls.Add(_streamCheckNavButton);

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundColor
        };
        contentHost.Controls.Add(_giveawayPage);
        contentHost.Controls.Add(_streamCheckPage);
        contentHost.Controls.Add(_clipDiscordPage);
        contentHost.Controls.Add(_musicPage);
        contentHost.Controls.Add(_minigamePage);
        contentHost.Controls.Add(_moderationPage);
        contentHost.Controls.Add(_raidPage);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 268));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.Controls.Add(navigation, 0, 0);
        rootLayout.Controls.Add(contentHost, 1, 0);

        Controls.Add(rootLayout);
        ApplyRaidClipTheme(this);
        StylePrimaryButton(_startButton);
        StylePrimaryButton(_createObsSourceButton);
        StylePrimaryButton(_saveSettingsButton);
        StylePrimaryButton(_saveModerationSettingsButton);
        StylePrimaryButton(_saveMinigameSettingsButton);
        StylePrimaryButton(_saveMusicSettingsButton);
        StylePrimaryButton(_startStreamButton);
        StylePrimaryButton(_saveStreamCheckButton);
        StylePrimaryButton(_saveClipDiscordButton);
        StylePrimaryButton(_giveawaySaveButton);
        StylePrimaryButton(_giveawayStartButton);
        StylePrimaryButton(_giveawayDrawButton);
        StylePrimaryButton(_updateButton);
        _resetPointsButton.BackColor = Color.FromArgb(72, 14, 17);
        _resetPointsButton.FlatAppearance.BorderColor = AccentColor;
        ShowSection("raid");
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
            _activeConfig = config;
            SetConnectionSettingsEditingEnabled(false);
            _resetPointsButton.Enabled = false;
            _importMinigameButton.Enabled = false;
            _testConnectionsButton.Enabled = false;

            SetServiceStatus(_playerIndicator, "Player", "Startet …", WaitingColor);
            _player = new LocalPlayerServer(config.Player.Port);
            _playerTask = _player.RunAsync(cancellationToken);
            SetServiceStatus(_playerIndicator, "Player", "Läuft", ActiveColor);

            SetServiceStatus(_obsIndicator, "OBS", "Verbindet …", WaitingColor);
            _obs = new ObsService(config);
            await Task.Run(_obs.Connect, cancellationToken);
            var sourceSetup = await Task.Run(
                () => _obs.EnsureBrowserSourceInCurrentScene(_player.IdleUrl),
                cancellationToken);
            _obs.SetBrowserUrl(_player.IdleUrl);
            if (sourceSetup.CreatedInput || sourceSetup.AddedToScene)
            {
                AppendLog(
                    $"OBS-Quelle {sourceSetup.SourceName} wurde in Szene " +
                    $"{sourceSetup.SceneName} automatisch eingerichtet.");
            }
            SetServiceStatus(_obsIndicator, "OBS", "Verbunden", ActiveColor);

            SetServiceStatus(_twitchIndicator, "Twitch", "Anmeldung …", WaitingColor);
            AppendLog("Prüfe Twitch-Anmeldung …");
            var session = await new AuthenticationService(config)
                .GetSessionAsync(cancellationToken);

            var twitch = new TwitchService(
                config.Twitch.ClientId,
                session.AccessToken);
            _twitch = twitch;
            twitch.ChatMessageSent += UpdateChatLastSent;
            _twitchSession = session;

            _broadcaster = await twitch.GetUserAsync(
                config.Twitch.BroadcasterLogin,
                cancellationToken);

            if (_broadcaster is null)
            {
                throw new InvalidOperationException(
                    "Der konfigurierte Twitch-Kanal wurde nicht gefunden.");
            }

            UpdateChatAuthenticationDiagnostics(session, _broadcaster);
            AppendLog(
                $"Twitch-Token validiert: {session.Login} ({session.UserId}); " +
                $"Broadcaster: {_broadcaster.DisplayName} ({_broadcaster.Id}); " +
                "Scopes: " + string.Join(", ", session.Scopes ?? Array.Empty<string>()));
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
                if (_minigame is not null)
                {
                    await _minigame.ProcessPassiveEventAsync(
                        new MinigamePassiveEvent(
                            MinigamePassiveEventKind.Raid,
                            raid.FromBroadcasterId, raid.FromBroadcasterName),
                        cancellationToken);
                }

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

            await StartMusicRequestsAsync(
                config, session, twitch, _broadcaster, cancellationToken);
            await StartClipCommandAsync(
                config, session, twitch, _broadcaster, cancellationToken);
            await StartGiveawayModuleAsync(
                config, session, twitch, _broadcaster, cancellationToken);

            var startChatTransport = true;
            if (startChatTransport)
            {
                _chatModeration = new ChatModerationService(
                    config.Twitch.ClientId,
                    session.AccessToken,
                    _broadcaster.Id,
                    session.UserId);
                _chatModeration.StatusChanged +=
                    UpdateChatConnectionDiagnostics;
                _chatModeration.Activated += () =>
                    AppendLog("Chatbot verbunden: WebSocket und channel.chat.message aktiv.");

                if (config.Moderation.Enabled)
                {
                    SetModerationStatus("Startet …", WaitingColor);
                    _chatModeration.Activated += () =>
                        SetModerationStatus("Aktiv", ActiveColor);
                    _chatModeration.MessageReceived += message =>
                        HandleChatMessageAsync(
                            message,
                            config,
                            cancellationToken);
                }
                else
                {
                    SetModerationStatus("Deaktiviert", InactiveColor);
                }

                if (config.MusicRequests.Enabled && _musicRequests is not null)
                {
                    _chatModeration.MessageReceived += message =>
                        _musicRequests.ProcessChatMessageAsync(
                            message, cancellationToken);
                }

                if (config.ClipCommand.Enabled && _clipCommandService is not null)
                {
                    _chatModeration.MessageReceived += message =>
                        _clipCommandService.HandleMessageAsync(
                            message, cancellationToken);
                }

                if (_discordInviteService is not null)
                {
                    _chatModeration.MessageReceived += message =>
                        _discordInviteService.ProcessMessageAsync(
                            message, cancellationToken);
                }

                if (config.Giveaways.Enabled && _giveawayService is not null)
                {
                    _chatModeration.MessageReceived += message =>
                        _giveawayService.ProcessMessageAsync(message, cancellationToken);
                }

                if (ChatMinigameService.ShouldRun(config.Minigame))
                {
                    SetMinigameStatus("Startet …", WaitingColor);
                    _minigame = new ChatMinigameService(
                        _broadcaster.Id,
                        session.UserId,
                        config.Minigame,
                        twitch,
                        _viewerPoints);
                    _chatModeration.Activated += () =>
                        SetMinigameStatus(
                            config.Minigame.Enabled && config.Minigame.PointsEnabled
                                ? "Spiele & Punktesystem aktiv"
                                : config.Minigame.PointsEnabled
                                    ? "Punktesystem aktiv"
                                    : "Spiele aktiv",
                            ActiveColor);
                    _minigame.PointsAwarded += (users, _) =>
                        SetMinigameStatus(
                            $"Anwesenheit · {users} Nutzer belohnt",
                            ActiveColor);
                    _minigame.DataChanged += () =>
                        _ = RefreshMinigameDashboardAsync();
                    _chatModeration.MessageReceived += message =>
                        _minigame.ProcessMessageAsync(
                            message,
                            cancellationToken);
                    _minigameTask = _minigame.RunAsync(cancellationToken);
                    ObserveMinigameTask(_minigameTask);

                }
                else
                {
                    SetMinigameStatus("Deaktiviert", InactiveColor);
                }

                _chatModerationTask =
                    _chatModeration.RunAsync(cancellationToken);
                ObserveChatModerationTask(_chatModerationTask);

                if ((config.Minigame.PointsEnabled &&
                     (config.Minigame.FollowPointsEnabled ||
                      config.Minigame.SubPointsEnabled ||
                      config.Minigame.ChannelRewardPointsEnabled)) ||
                    config.MusicRequests.Enabled)
                {
                    _minigameEvents = new MinigameEventService(
                        config.Twitch.ClientId, session.AccessToken,
                        _broadcaster.Id, session.UserId, config.Minigame,
                        config.MusicRequests.Enabled
                            ? config.MusicRequests.SelectedRewardId : "");
                    if (_minigame is not null)
                    {
                        _minigameEvents.EventReceived += passiveEvent =>
                            _minigame.ProcessPassiveEventAsync(
                                passiveEvent, cancellationToken);
                    }
                    if (_musicRequests is not null)
                    {
                        _minigameEvents.MusicRedemptionReceived += redemption =>
                            _musicRequests.EnqueueAsync(
                                redemption, cancellationToken);
                        _minigameEvents.Activated += () =>
                            SetSpotifyStatus(_spotify?.IsConnected == true
                                ? "Verbunden · EventSub aktiv"
                                : "EventSub aktiv · Spotify fehlt",
                                _spotify?.IsConnected == true
                                    ? ActiveColor : WaitingColor);
                    }
                    _minigameEventTask = _minigameEvents.RunAsync(
                        cancellationToken);
                    ObserveMinigameTask(_minigameEventTask);
                }
            }
            else
            {
                SetModerationStatus("Deaktiviert", InactiveColor);
                SetMinigameStatus("Deaktiviert", InactiveColor);
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
            if (IsMinigameConfigurationError(exception.Message))
            {
                SetMinigameStatus("Einstellungen ungültig", ErrorColor);
                ShowSection("minigame");
            }
            else if (IsMusicConfigurationError(exception.Message))
            {
                SetSpotifyStatus("Einstellungen ungültig", ErrorColor);
                ShowSection("music");
            }
            else if (IsClipConfigurationError(exception.Message))
            {
                _clipLastErrorStatus.Text = "Letzter Fehler: " + exception.Message;
                _clipLastErrorStatus.ForeColor = ErrorColor;
                ShowSection("clip-discord");
            }
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

    private async void ObserveMinigameTask(Task task)
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
            AppendLog("Chat-Minigame wurde beendet: " + exception.Message);
            SetMinigameStatus("Fehler", ErrorColor);
        }
    }

    private void SetMinigameStatus(string status, Color color)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => SetMinigameStatus(status, color)));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        _minigameStatusLabel.Text = $"● Minigame: {status}";
        _minigameStatusLabel.ForeColor = color;
    }

    private async Task RefreshMinigameDashboardAsync()
    {
        try
        {
            var token = _shutdown?.Token ?? CancellationToken.None;
            var top = await _viewerPoints.GetTopAsync(10, token);
            var history = await _viewerPoints.GetHistoryAsync(100, token);
            var jackpot = await _viewerPoints.GetJackpotAsync(
                decimal.ToInt32(_jackpotStartControl.Value), token);

            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                    PopulateMinigameDashboard(top, history, jackpot)));
                return;
            }
            PopulateMinigameDashboard(top, history, jackpot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppendLog("Minigame-Übersicht konnte nicht geladen werden: " +
                exception.Message);
        }
    }

    private void PopulateMinigameDashboard(
        IReadOnlyList<ViewerPointEntry> top,
        IReadOnlyList<MinigameHistoryEntry> history, int jackpot)
    {
        _minigameTopList.BeginUpdate();
        _minigameTopList.Items.Clear();
        for (var index = 0; index < top.Count; index++)
        {
            var entry = top[index];
            _minigameTopList.Items.Add(new ListViewItem(new[]
            {
                $"#{index + 1}", entry.DisplayName, $"{entry.Points:N0}",
                $"{entry.WatchMinutes / 60}h {entry.WatchMinutes % 60}m"
            }));
        }
        _minigameTopList.EndUpdate();

        _minigameHistoryList.BeginUpdate();
        _minigameHistoryList.Items.Clear();
        foreach (var entry in history)
        {
            _minigameHistoryList.Items.Add(new ListViewItem(new[]
            {
                entry.Timestamp.ToLocalTime().ToString("dd.MM. HH:mm:ss"),
                entry.DisplayName, entry.Game, entry.Action,
                entry.Change.ToString("+0;-0;0"), entry.Balance.ToString("N0")
            }));
        }
        _minigameHistoryList.EndUpdate();
        var currency = _activeConfig?.Minigame.CurrencyPlural ??
            (_currencyPluralBox.Text.Trim().Length > 0
                ? _currencyPluralBox.Text.Trim() : "Punkte");
        _jackpotValueLabel.Text =
            $"Aktueller Jackpot: {jackpot:N0} {currency}";
    }

    private async Task ExportMinigameDataAsync()
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON-Datei (*.json)|*.json",
                FileName = $"RaidClip-Minispiel-{DateTime.Now:yyyy-MM-dd}.json",
                Title = "Minigame-Daten exportieren"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var config = ReadSettingsFromControls();
            await _viewerPoints.ExportAsync(
                dialog.FileName, config.Minigame,
                _shutdown?.Token ?? CancellationToken.None);
            AppendLog("Minigame-Daten wurden exportiert: " + dialog.FileName);
        }
        catch (Exception exception)
        {
            AppendLog("Minigame-Export fehlgeschlagen: " + exception.Message);
        }
    }

    private async Task ImportMinigameDataAsync()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "JSON-Datei (*.json)|*.json",
                Title = "Minigame-Daten importieren"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (MessageBox.Show(
                    "Importieren? Vorher wird automatisch ein Backup erstellt.",
                    "Minigame-Daten importieren", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;

            var config = ReadSettingsFromControls();
            var settings = await _viewerPoints.ImportAsync(
                dialog.FileName, config.Minigame,
                _shutdown?.Token ?? CancellationToken.None);
            config.Minigame = settings;
            _configurationService.SaveGuiSettings(config);
            LoadSettingsIntoControls();
            await RefreshMinigameDashboardAsync();
            AppendLog("Minigame-Daten wurden importiert. Ein Backup wurde erstellt.");
        }
        catch (Exception exception)
        {
            AppendLog("Minigame-Import fehlgeschlagen: " + exception.Message);
            MessageBox.Show(exception.Message, "Import fehlgeschlagen",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ResetPointDataAsync()
    {
        var confirmation = MessageBox.Show(
            "Wirklich alle lokal gespeicherten Zuschauerpunkte löschen?",
            "Punktedaten zurücksetzen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _viewerPoints.ResetAsync(
                _shutdown?.Token ?? CancellationToken.None);
            AppendLog("Alle Minigame-Punktedaten wurden zurückgesetzt.");
            SetMinigameStatus("Punktedaten zurückgesetzt", ActiveColor);
            await RefreshMinigameDashboardAsync();
        }
        catch (Exception exception)
        {
            AppendLog(
                "Minigame-Punktedaten konnten nicht zurückgesetzt werden: " +
                exception.Message);
            SetMinigameStatus("Reset fehlgeschlagen", ErrorColor);
        }
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
        _moderationSettingsGroup.Enabled = false;
        SetMinigameSettingsEditingEnabled(false);
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
            _moderationSettingsGroup.Enabled = true;
            SetMinigameSettingsEditingEnabled(true);
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
            SetConnectionSettingsEditingEnabled(true);
            _resetPointsButton.Enabled = true;
            _importMinigameButton.Enabled = true;
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

        var tasks = new[]
        {
            _playerTask,
            _eventSubTask,
            _chatModerationTask,
            _minigameTask,
            _minigameEventTask,
            _musicRequestTask,
            _musicEventSubTask,
            _clipCommandTask,
            _giveawayTask
        }
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

        StopMusicRequests();
        StopClipCommand();
        StopGiveawayModule();
        _minigameEvents?.Dispose();
        _minigame?.Dispose();
        _chatModeration?.Dispose();
        _obs?.Dispose();
        _player?.Dispose();
        shutdown.Dispose();

        _shutdown = null;
        _activeConfig = null;
        _player = null;
        _obs = null;
        _playback = null;
        _broadcaster = null;
        _twitch = null;
        _twitchSession = null;
        _eventSub = null;
        _chatModeration = null;
        _minigame = null;
        _minigameEvents = null;
        _chatModerationTask = null;
        _minigameTask = null;
        _minigameEventTask = null;
        _playerTask = null;
        _eventSubTask = null;
        _clipCommandTask = null;
        _giveawayTask = null;

        ResetServiceIndicators();
        ResetChatDiagnosticConnection();
        SetModerationStatus("Deaktiviert", InactiveColor);
        SetMinigameStatus("Deaktiviert", InactiveColor);
        _testChannelBox.Enabled = true;
        _startButton.Enabled = true;
        SetConnectionSettingsEditingEnabled(true);
        _resetPointsButton.Enabled = true;
        _importMinigameButton.Enabled = true;
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

    private static FlowLayoutPanel CreateMinigameFlow() => new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8)
    };

    private void SetConnectionSettingsEditingEnabled(bool enabled)
    {
        _twitchChannelBox.Enabled = enabled;
        _obsHostBox.Enabled = enabled;
        _obsPortControl.Enabled = enabled;
        _obsPasswordBox.Enabled = enabled;
    }

    private void SetMinigameSettingsEditingEnabled(bool enabled)
    {
        SetEditableChildrenEnabled(_minigameSettingsGroup, enabled);
    }

    private static void SetEditableChildrenEnabled(
        Control parent, bool enabled)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TabControl or Panel or GroupBox)
            {
                control.Enabled = true;
                SetEditableChildrenEnabled(control, enabled);
                continue;
            }

            if (control is Label or ListView)
            {
                continue;
            }

            control.Enabled = enabled;
        }
    }

    private static void AddMinigameTab(
        TabControl tabs, string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
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

    private void AddPointsBlacklistEntry()
    {
        var name = _pointsBlacklistInput.Text.Trim().TrimStart('@')
            .ToLowerInvariant();
        if (name.Length == 0) return;
        if (!_pointsBlacklistList.Items.Cast<object>().Any(item =>
                string.Equals(item.ToString(), name,
                    StringComparison.OrdinalIgnoreCase)))
            _pointsBlacklistList.Items.Add(name);
        _pointsBlacklistInput.Clear();
    }

    private void RemoveSelectedPointsBlacklistEntry()
    {
        while (_pointsBlacklistList.SelectedIndices.Count > 0)
            _pointsBlacklistList.Items.RemoveAt(
                _pointsBlacklistList.SelectedIndices[0]);
    }

    private void UpdateCurrencyPreview()
    {
        var singular = string.IsNullOrWhiteSpace(_currencySingularBox.Text)
            ? "Punkt" : _currencySingularBox.Text.Trim();
        var plural = string.IsNullOrWhiteSpace(_currencyPluralBox.Text)
            ? "Punkte" : _currencyPluralBox.Text.Trim();
        _currencyPreviewLabel.Text =
            $"Beispiel: Du besitzt 1 {singular}.  Du besitzt 250 {plural}.";
    }

    private void LoadSettingsIntoControls()
    {
        try
        {
            var config = _configurationService.Load();
            SelectUiTheme(config.UiTheme);
            ApplyUiTheme(config.UiTheme);
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
            _minigameEnabledCheck.Checked = config.Minigame.Enabled;
            _pointsEnabledCheck.Checked = config.Minigame.PointsEnabled;
            _gambleEnabledCheck.Checked = config.Minigame.GambleEnabled;
            _currencySingularBox.Text = config.Minigame.CurrencySingular;
            _currencyPluralBox.Text = config.Minigame.CurrencyPlural;
            _pointsCommandPunkteCheck.Checked =
                config.Minigame.PointsCommandPunkteEnabled;
            _pointsCommandPointsCheck.Checked =
                config.Minigame.PointsCommandPointsEnabled;
            _pointsCommandPerlenCheck.Checked =
                config.Minigame.PointsCommandPerlenEnabled;
            _customPointsCommandBox.Text = config.Minigame.CustomPointsCommand;
            _pointsBlacklistList.Items.Clear();
            _pointsBlacklistList.Items.AddRange(
                config.Minigame.PointsBlacklist.Cast<object>().ToArray());
            UpdateCurrencyPreview();
            if (_minigameTopList.Columns.Count >= 3)
                _minigameTopList.Columns[2].Text = config.Minigame.CurrencyPlural;
            SetNumericValue(_pointsPerIntervalControl,
                config.Minigame.PointsPerInterval);
            SetNumericValue(_lurkerPointsPerIntervalControl,
                config.Minigame.LurkerPointsPerInterval);
            SetNumericValue(_pointsIntervalControl,
                config.Minigame.IntervalMinutes);
            SetNumericValue(_minimumPointsControl,
                config.Minigame.MinimumPoints);
            SetNumericValue(_pointsCommandCooldownControl,
                config.Minigame.PointsCommandCooldownSeconds);
            SetNumericValue(_gambleCooldownControl,
                config.Minigame.GambleCooldownSeconds);
            SetNumericValue(_globalCommandCooldownControl,
                config.Minigame.GlobalCommandCooldownSeconds);
            SetNumericValue(_minimumBetControl,
                config.Minigame.MinimumBet);
            SetNumericValue(_maximumBetControl,
                config.Minigame.MaximumBet);
            for (var index = 0; index < 4; index++)
            {
                var range = config.Minigame.GambleRanges[index];
                SetNumericValue(_gambleFromControls[index], range.From);
                SetNumericValue(_gambleToControls[index], range.To);
                _gambleMultiplierControls[index].Value = Math.Clamp(
                    range.Multiplier,
                    _gambleMultiplierControls[index].Minimum,
                    _gambleMultiplierControls[index].Maximum);
                _gambleTextBoxes[index].Text = range.ChatText;
            }
            _chatPointsCheck.Checked = config.Minigame.ChatPointsEnabled;
            SetNumericValue(_chatPointsControl, config.Minigame.ChatMessagePoints);
            SetNumericValue(_chatPointsCooldownControl,
                config.Minigame.ChatMessagePointsCooldownSeconds);
            _followPointsCheck.Checked = config.Minigame.FollowPointsEnabled;
            SetNumericValue(_followPointsControl, config.Minigame.FollowPoints);
            _subPointsCheck.Checked = config.Minigame.SubPointsEnabled;
            SetNumericValue(_subPointsControl, config.Minigame.SubPoints);
            _raidPointsCheck.Checked = config.Minigame.RaidPointsEnabled;
            SetNumericValue(_raidPointsControl, config.Minigame.RaidPoints);
            _rewardPointsCheck.Checked = config.Minigame.ChannelRewardPointsEnabled;
            SetNumericValue(_rewardPointsControl, config.Minigame.ChannelRewardPoints);
            _dailyCheck.Checked = config.Minigame.DailyEnabled;
            SetNumericValue(_dailyPointsControl, config.Minigame.DailyBonusPoints);
            _leaderboardCheck.Checked = config.Minigame.LeaderboardEnabled;
            SetNumericValue(_maximumTopControl, config.Minigame.MaximumTopEntries);
            SetNumericValue(_leaderboardCooldownControl, config.Minigame.LeaderboardCooldownSeconds);
            _profileCheck.Checked = config.Minigame.ProfileEnabled;
            SetNumericValue(_profileCooldownControl, config.Minigame.ProfileCooldownSeconds);
            _historyEnabledCheck.Checked = config.Minigame.HistoryEnabled;
            SetNumericValue(_historyLimitControl, config.Minigame.HistoryLimit);
            _coinflipEnabledCheck.Checked = config.Minigame.CoinflipEnabled;
            _coinflipMultiplierControl.Value = config.Minigame.CoinflipMultiplier;
            SetNumericValue(_coinflipMinControl, config.Minigame.CoinflipMinimumBet);
            SetNumericValue(_coinflipMaxControl, config.Minigame.CoinflipMaximumBet);
            SetNumericValue(_coinflipCooldownControl, config.Minigame.CoinflipCooldownSeconds);
            _slotsEnabledCheck.Checked = config.Minigame.SlotsEnabled;
            _slotSymbolsBox.Text = config.Minigame.SlotSymbols;
            _slotsThreeControl.Value = config.Minigame.SlotsThreeMultiplier;
            _slotsTwoControl.Value = config.Minigame.SlotsTwoMultiplier;
            _slotsSevenControl.Value = config.Minigame.SlotsSevenMultiplier;
            SetNumericValue(_slotsMinControl, config.Minigame.SlotsMinimumBet);
            SetNumericValue(_slotsMaxControl, config.Minigame.SlotsMaximumBet);
            SetNumericValue(_slotsCooldownControl, config.Minigame.SlotsCooldownSeconds);
            _rouletteEnabledCheck.Checked = config.Minigame.RouletteEnabled;
            _rouletteEvenMoneyControl.Value = config.Minigame.RouletteEvenMoneyMultiplier;
            _rouletteNumberControl.Value = config.Minigame.RouletteNumberMultiplier;
            SetNumericValue(_rouletteMinControl, config.Minigame.RouletteMinimumBet);
            SetNumericValue(_rouletteMaxControl, config.Minigame.RouletteMaximumBet);
            SetNumericValue(_rouletteCooldownControl, config.Minigame.RouletteCooldownSeconds);
            _jackpotEnabledCheck.Checked = config.Minigame.JackpotEnabled;
            SetNumericValue(_jackpotStartControl, config.Minigame.JackpotStartValue);
            _jackpotContributionControl.Value = config.Minigame.JackpotContributionPercent;
            _maximumAccountCheck.Checked = config.Minigame.MaximumAccountEnabled;
            SetNumericValue(_maximumAccountControl, config.Minigame.MaximumAccountPoints);
            _dailyGamesCheck.Checked = config.Minigame.DailyGambleLimitEnabled;
            SetNumericValue(_dailyGamesControl, config.Minigame.DailyGambleLimit);
            _dailyLossCheck.Checked = config.Minigame.DailyLossLimitEnabled;
            SetNumericValue(_dailyLossControl, config.Minigame.DailyLossLimit);
            _dailyWinCheck.Checked = config.Minigame.DailyWinLimitEnabled;
            SetNumericValue(_dailyWinControl, config.Minigame.DailyWinLimit);
            _chatTemplateBox.Text = config.Chat.RaidMessageTemplate;
            LoadMusicRequestSettings(config.MusicRequests);
            LoadClipDiscordSettings(config);
            LoadGiveawaySettings(config.Giveaways);
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
        config.UiTheme = ThemeKeyFromSelection();
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
        config.Minigame.Enabled = _minigameEnabledCheck.Checked;
        config.Minigame.PointsEnabled = _pointsEnabledCheck.Checked;
        config.Minigame.GambleEnabled = _gambleEnabledCheck.Checked;
        config.Minigame.CurrencySingular = _currencySingularBox.Text.Trim();
        config.Minigame.CurrencyPlural = _currencyPluralBox.Text.Trim();
        config.Minigame.PointsCommandPunkteEnabled =
            _pointsCommandPunkteCheck.Checked;
        config.Minigame.PointsCommandPointsEnabled =
            _pointsCommandPointsCheck.Checked;
        config.Minigame.PointsCommandPerlenEnabled =
            _pointsCommandPerlenCheck.Checked;
        config.Minigame.CustomPointsCommand =
            _customPointsCommandBox.Text.Trim();
        config.Minigame.PointsBlacklist = _pointsBlacklistList.Items
            .Cast<object>()
            .Select(item => item.ToString() ?? "")
            .Where(item => item.Length > 0)
            .ToList();
        config.Minigame.PointsPerInterval =
            decimal.ToInt32(_pointsPerIntervalControl.Value);
        config.Minigame.LurkerPointsPerInterval =
            decimal.ToInt32(_lurkerPointsPerIntervalControl.Value);
        config.Minigame.IntervalMinutes =
            decimal.ToInt32(_pointsIntervalControl.Value);
        config.Minigame.MinimumPoints =
            decimal.ToInt32(_minimumPointsControl.Value);
        config.Minigame.PointsCommandCooldownSeconds =
            decimal.ToInt32(_pointsCommandCooldownControl.Value);
        config.Minigame.GambleCooldownSeconds =
            decimal.ToInt32(_gambleCooldownControl.Value);
        config.Minigame.GlobalCommandCooldownSeconds =
            decimal.ToInt32(_globalCommandCooldownControl.Value);
        config.Minigame.MinimumBet =
            decimal.ToInt32(_minimumBetControl.Value);
        config.Minigame.MaximumBet =
            decimal.ToInt32(_maximumBetControl.Value);
        config.Minigame.GambleRanges = Enumerable.Range(0, 4)
            .Select(index => new GambleRangeConfig
            {
                From = decimal.ToInt32(_gambleFromControls[index].Value),
                To = decimal.ToInt32(_gambleToControls[index].Value),
                Multiplier = _gambleMultiplierControls[index].Value,
                ChatText = _gambleTextBoxes[index].Text.Trim()
            })
            .ToList();
        config.Minigame.ChatPointsEnabled = _chatPointsCheck.Checked;
        config.Minigame.ChatMessagePoints = decimal.ToInt32(_chatPointsControl.Value);
        config.Minigame.ChatMessagePointsCooldownSeconds = decimal.ToInt32(_chatPointsCooldownControl.Value);
        config.Minigame.FollowPointsEnabled = _followPointsCheck.Checked;
        config.Minigame.FollowPoints = decimal.ToInt32(_followPointsControl.Value);
        config.Minigame.SubPointsEnabled = _subPointsCheck.Checked;
        config.Minigame.SubPoints = decimal.ToInt32(_subPointsControl.Value);
        config.Minigame.RaidPointsEnabled = _raidPointsCheck.Checked;
        config.Minigame.RaidPoints = decimal.ToInt32(_raidPointsControl.Value);
        config.Minigame.ChannelRewardPointsEnabled = _rewardPointsCheck.Checked;
        config.Minigame.ChannelRewardPoints = decimal.ToInt32(_rewardPointsControl.Value);
        config.Minigame.DailyEnabled = _dailyCheck.Checked;
        config.Minigame.DailyBonusPoints = decimal.ToInt32(_dailyPointsControl.Value);
        config.Minigame.LeaderboardEnabled = _leaderboardCheck.Checked;
        config.Minigame.MaximumTopEntries = decimal.ToInt32(_maximumTopControl.Value);
        config.Minigame.LeaderboardCooldownSeconds = decimal.ToInt32(_leaderboardCooldownControl.Value);
        config.Minigame.ProfileEnabled = _profileCheck.Checked;
        config.Minigame.ProfileCooldownSeconds = decimal.ToInt32(_profileCooldownControl.Value);
        config.Minigame.HistoryEnabled = _historyEnabledCheck.Checked;
        config.Minigame.HistoryLimit = decimal.ToInt32(_historyLimitControl.Value);
        config.Minigame.CoinflipEnabled = _coinflipEnabledCheck.Checked;
        config.Minigame.CoinflipMultiplier = _coinflipMultiplierControl.Value;
        config.Minigame.CoinflipMinimumBet = decimal.ToInt32(_coinflipMinControl.Value);
        config.Minigame.CoinflipMaximumBet = decimal.ToInt32(_coinflipMaxControl.Value);
        config.Minigame.CoinflipCooldownSeconds = decimal.ToInt32(_coinflipCooldownControl.Value);
        config.Minigame.SlotsEnabled = _slotsEnabledCheck.Checked;
        config.Minigame.SlotSymbols = _slotSymbolsBox.Text.Trim();
        config.Minigame.SlotsThreeMultiplier = _slotsThreeControl.Value;
        config.Minigame.SlotsTwoMultiplier = _slotsTwoControl.Value;
        config.Minigame.SlotsSevenMultiplier = _slotsSevenControl.Value;
        config.Minigame.SlotsMinimumBet = decimal.ToInt32(_slotsMinControl.Value);
        config.Minigame.SlotsMaximumBet = decimal.ToInt32(_slotsMaxControl.Value);
        config.Minigame.SlotsCooldownSeconds = decimal.ToInt32(_slotsCooldownControl.Value);
        config.Minigame.RouletteEnabled = _rouletteEnabledCheck.Checked;
        config.Minigame.RouletteEvenMoneyMultiplier = _rouletteEvenMoneyControl.Value;
        config.Minigame.RouletteNumberMultiplier = _rouletteNumberControl.Value;
        config.Minigame.RouletteMinimumBet = decimal.ToInt32(_rouletteMinControl.Value);
        config.Minigame.RouletteMaximumBet = decimal.ToInt32(_rouletteMaxControl.Value);
        config.Minigame.RouletteCooldownSeconds = decimal.ToInt32(_rouletteCooldownControl.Value);
        config.Minigame.JackpotEnabled = _jackpotEnabledCheck.Checked;
        config.Minigame.JackpotStartValue = decimal.ToInt32(_jackpotStartControl.Value);
        config.Minigame.JackpotContributionPercent = _jackpotContributionControl.Value;
        config.Minigame.MaximumAccountEnabled = _maximumAccountCheck.Checked;
        config.Minigame.MaximumAccountPoints = decimal.ToInt32(_maximumAccountControl.Value);
        config.Minigame.DailyGambleLimitEnabled = _dailyGamesCheck.Checked;
        config.Minigame.DailyGambleLimit = decimal.ToInt32(_dailyGamesControl.Value);
        config.Minigame.DailyLossLimitEnabled = _dailyLossCheck.Checked;
        config.Minigame.DailyLossLimit = decimal.ToInt32(_dailyLossControl.Value);
        config.Minigame.DailyWinLimitEnabled = _dailyWinCheck.Checked;
        config.Minigame.DailyWinLimit = decimal.ToInt32(_dailyWinControl.Value);
        config.Chat.RaidMessageTemplate = _chatTemplateBox.Text.Trim();
        ReadMusicRequestSettings(config);
        return config;
    }

    private void SaveSettingsFromControls()
    {
        try
        {
            var config = ReadSettingsFromControls();
            _configurationService.SaveGuiSettings(config);
            ApplyRuntimeSettings(config);
            AppendLog("Einstellungen wurden gespeichert.");
            SetOverallStatus("Einstellungen gespeichert", ActiveColor);
        }
        catch (Exception exception)
        {
            AppendLog(
                "Einstellungen konnten nicht gespeichert werden: " +
                exception.Message);
            SetOverallStatus("Einstellungsfehler", ErrorColor);
            if (IsMinigameConfigurationError(exception.Message))
            {
                SetMinigameStatus("Einstellungen ungültig", ErrorColor);
                ShowSection("minigame");
            }
            else if (IsMusicConfigurationError(exception.Message))
            {
                SetSpotifyStatus("Einstellungen ungültig", ErrorColor);
                ShowSection("music");
            }
            else if (IsClipConfigurationError(exception.Message))
            {
                _clipLastErrorStatus.Text = "Letzter Fehler: " + exception.Message;
                _clipLastErrorStatus.ForeColor = ErrorColor;
                ShowSection("clip-discord");
            }
        }
    }

    private void ApplyRuntimeSettings(AppConfig updated)
    {
        if (_activeConfig is null)
        {
            return;
        }

        var moduleRestartRequired =
            _activeConfig.Minigame.Enabled != updated.Minigame.Enabled ||
            _activeConfig.Minigame.PointsEnabled != updated.Minigame.PointsEnabled ||
            _activeConfig.Moderation.Enabled != updated.Moderation.Enabled ||
            _activeConfig.MusicRequests.Enabled != updated.MusicRequests.Enabled ||
            _activeConfig.ClipCommand.Enabled != updated.ClipCommand.Enabled ||
            _activeConfig.DiscordClips.Enabled != updated.DiscordClips.Enabled ||
            _activeConfig.Giveaways.Enabled != updated.Giveaways.Enabled ||
            _activeConfig.ClipCommand.MaximumQueueSize !=
                updated.ClipCommand.MaximumQueueSize;
        _activeConfig.Chat = updated.Chat;
        _activeConfig.Moderation = updated.Moderation;
        _activeConfig.Minigame = updated.Minigame;
        _activeConfig.MusicRequests = updated.MusicRequests;
        _activeConfig.ClipCommand = updated.ClipCommand;
        _activeConfig.DiscordClips = updated.DiscordClips;
        _activeConfig.Giveaways = updated.Giveaways;
        _activeConfig.Player.DurationSeconds = updated.Player.DurationSeconds;
        _activeConfig.Player.VolumePercent = updated.Player.VolumePercent;
        _activeConfig.Player.BlacklistedClipIds =
            updated.Player.BlacklistedClipIds;
        _activeConfig.Twitch.ClipLookbackDays =
            updated.Twitch.ClipLookbackDays;
        _activeConfig.Twitch.ClipRetryAttempts =
            updated.Twitch.ClipRetryAttempts;
        _activeConfig.Twitch.RaidCooldownMinutes =
            updated.Twitch.RaidCooldownMinutes;
        _minigame?.UpdateConfig(updated.Minigame);
        _musicRequests?.UpdateConfig(updated.MusicRequests);
        _spotify?.UpdateConfig(updated.MusicRequests);
        _clipCommandService?.UpdateConfig(updated.ClipCommand);
        _discordClipService?.UpdateConfig(updated.DiscordClips);
        _discordInviteService?.UpdateConfig(updated.DiscordClips);
        ApplyUiTheme(updated.UiTheme);
        _giveawayService?.UpdateConfig(updated.Giveaways, updated.Minigame);
        UpdateCurrencyPreview();
        if (_minigameTopList.Columns.Count >= 3)
            _minigameTopList.Columns[2].Text = updated.Minigame.CurrencyPlural;

        AppendLog("Laufende Chat- und Minigame-Einstellungen wurden übernommen.");
        if (moduleRestartRequired)
        {
            AppendLog(
                "Das Ein- oder Ausschalten eines ganzen Moduls wird nach " +
                "dem nächsten Neustart der Plugin-Verbindung wirksam.");
        }
    }

    private static bool IsClipConfigurationError(string message) =>
        message.Contains("Clip-Command", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Clip-Dauer", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Clip-Titel", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Clip-Cooldown", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Clip-Limit", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Clip-Warteschlange", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Discord", StringComparison.OrdinalIgnoreCase);

    private static bool IsMusicConfigurationError(string message) =>
        message.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Musikwunsch", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Musik-Command", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Twitch-Musikwunsch", StringComparison.OrdinalIgnoreCase);

    private static bool IsMinigameConfigurationError(string message) =>
        message.Contains("Gamble", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Punkte", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Einsatz", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Command-Cooldown", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Währung", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Punkteabfrage", StringComparison.OrdinalIgnoreCase);

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
        _spotify?.Dispose();
        base.OnFormClosing(e);
    }
}
