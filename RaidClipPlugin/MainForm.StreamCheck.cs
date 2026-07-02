using System.Net.Http;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Button _streamCheckNavButton = CreateNavigationTile(
        "✅  Stream-Check", "Dienste und Einstellungen prüfen");
    private readonly Panel _streamCheckPage = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly ListView _streamCheckResults = NewDetailsList();
    private readonly CheckedListBox _streamCheckProfile = new()
    {
        Width = 330, Height = 250, CheckOnClick = true,
        IntegralHeight = false
    };
    private readonly Button _runStreamChecksButton = NewActionButton("Alle Prüfungen starten");
    private readonly Button _retryStreamChecksButton = NewActionButton("Fehlgeschlagene wiederholen");
    private readonly Button _copyStreamCheckButton = NewActionButton("Ergebnis kopieren");
    private readonly Button _exportStreamCheckButton = NewActionButton("Diagnose exportieren");
    private readonly Button _fixStreamCheckButton = NewActionButton("Fehler beheben");
    private readonly Button _startStreamButton = NewActionButton("Stream starten");
    private readonly Button _saveStreamCheckButton = NewActionButton("Profil speichern");
    private readonly Label _streamCheckSummary = new()
    {
        Text = "Noch nicht geprüft.", AutoSize = true,
        Font = new Font("Segoe UI", 12F, FontStyle.Bold),
        ForeColor = MutedTextColor, Margin = new Padding(8)
    };
    private readonly TextBox _streamStartSceneBox = new() { Width = 180 };
    private readonly TextBox _streamMicSourceBox = new() { Width = 180 };
    private readonly TextBox _streamDesktopSourceBox = new() { Width = 180 };
    private readonly TextBox _streamRecordingPathBox = new() { Width = 260 };
    private readonly NumericUpDown _streamMinimumSpaceControl =
        CreateIntegerControl(10, 1, 10000);
    private readonly CheckBox _streamSelectSceneCheck =
        NewCheck("Startszene auswählen", true);
    private readonly CheckBox _streamStartObsCheck =
        NewCheck("OBS-Streaming starten", true);
    private readonly CheckBox _streamStartServicesCheck =
        NewCheck("Plugin-Dienste aktivieren", true);
    private readonly List<StreamCheckResult> _lastStreamCheckResults = new();
    private CancellationTokenSource? _streamCheckCancellation;
    private TwitchService? _twitch;
    private TwitchSession? _twitchSession;

    private static readonly (string Key, string Name)[] StreamCheckOptions =
    {
        ("twitch", "Twitch verbunden"),
        ("chat", "Twitch-Chat verbunden"),
        ("eventsub", "EventSub verbunden"),
        ("obs", "OBS-WebSocket verbunden"),
        ("scene", "OBS-Szene vorhanden"),
        ("raid-source", "Raid-Clip-Quelle vorhanden"),
        ("player", "Browserquelle erreichbar"),
        ("microphone", "Mikrofonquelle vorhanden"),
        ("microphone-muted", "Mikrofon nicht stumm"),
        ("desktop-audio", "Desktop-Audio vorhanden"),
        ("storage", "Freier Aufnahmespeicher"),
        ("title", "Twitch-Streamtitel gesetzt"),
        ("category", "Twitch-Kategorie gesetzt"),
        ("spotify", "Spotify verbunden"),
        ("spotify-device", "Spotify-Gerät verfügbar"),
        ("chatbot", "Chatbot und Minigames bereit"),
        ("oauth", "OAuth-Berechtigungen vorhanden"),
        ("configuration", "Keine kritischen Konfigurationsfehler")
    };

    private void BuildStreamCheckPage()
    {
        _streamCheckResults.Columns.Add("Status", 90);
        _streamCheckResults.Columns.Add("Prüfung", 260);
        _streamCheckResults.Columns.Add("Ergebnis", 560);
        _streamCheckResults.Columns.Add("Dauer", 90);
        foreach (var option in StreamCheckOptions)
            _streamCheckProfile.Items.Add(new StreamCheckOption(option.Key, option.Name), true);

        var header = new Label
        {
            Text = "Stream-Start-Check",
            AutoSize = true,
            Font = new Font("Segoe UI", 23F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        };
        var subtitle = new Label
        {
            Text = "Prüft Twitch, OBS, Audio, Speicher, Spotify und Bot-Dienste vor dem Stream.",
            AutoSize = true, ForeColor = MutedTextColor
        };
        var titlePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true
        };
        titlePanel.Controls.Add(header);
        titlePanel.Controls.Add(subtitle);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
            Padding = new Padding(0, 4, 0, 4)
        };
        actions.Controls.AddRange(new Control[]
        {
            _runStreamChecksButton, _retryStreamChecksButton,
            _copyStreamCheckButton, _exportStreamCheckButton,
            _fixStreamCheckButton, _startStreamButton
        });

        var profileSettings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
            Padding = new Padding(8)
        };
        profileSettings.Controls.Add(_streamCheckProfile);
        profileSettings.Controls.Add(CreateSettingEditor("Startszene", _streamStartSceneBox));
        profileSettings.Controls.Add(CreateSettingEditor("Mikrofonquelle", _streamMicSourceBox));
        profileSettings.Controls.Add(CreateSettingEditor("Desktop-Audio", _streamDesktopSourceBox));
        profileSettings.Controls.Add(CreateSettingEditor("Aufnahmeordner", _streamRecordingPathBox));
        profileSettings.Controls.Add(CreateSettingEditor("Mindestens frei (GB)", _streamMinimumSpaceControl));
        profileSettings.Controls.Add(_streamSelectSceneCheck);
        profileSettings.Controls.Add(_streamStartObsCheck);
        profileSettings.Controls.Add(_streamStartServicesCheck);
        profileSettings.Controls.Add(_saveStreamCheckButton);

        var profileGroup = new GroupBox
        {
            Text = "Stream-Check-Profil", Dock = DockStyle.Fill,
            ForeColor = TextColor, Padding = new Padding(8)
        };
        profileGroup.Controls.Add(profileSettings);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        body.Controls.Add(_streamCheckResults, 0, 0);
        body.Controls.Add(profileGroup, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(20),
            ColumnCount = 1, RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(titlePanel, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        layout.Controls.Add(_streamCheckSummary, 0, 2);
        layout.Controls.Add(body, 0, 3);
        _streamCheckPage.Controls.Add(layout);
        LoadStreamCheckProfile();
    }

    private void InitializeStreamCheckEvents()
    {
        _streamCheckNavButton.Click += (_, _) => ShowSection("stream-check");
        _runStreamChecksButton.Click += async (_, _) => await RunStreamChecksAsync(false);
        _retryStreamChecksButton.Click += async (_, _) => await RunStreamChecksAsync(true);
        _copyStreamCheckButton.Click += (_, _) => CopyStreamCheckResult();
        _exportStreamCheckButton.Click += async (_, _) => await ExportStreamCheckAsync();
        _fixStreamCheckButton.Click += async (_, _) => await FixSelectedStreamCheckAsync();
        _saveStreamCheckButton.Click += (_, _) => SaveStreamCheckProfile();
        _startStreamButton.Click += async (_, _) => await StartStreamFromCheckAsync();
    }

    private void LoadStreamCheckProfile()
    {
        try
        {
            var profile = _configurationService.Load().StreamCheck;
            var disabled = profile.DisabledChecks.ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < _streamCheckProfile.Items.Count; index++)
            {
                var option = (StreamCheckOption)_streamCheckProfile.Items[index];
                _streamCheckProfile.SetItemChecked(index, !disabled.Contains(option.Key));
            }
            _streamStartSceneBox.Text = profile.StartScene;
            _streamMicSourceBox.Text = profile.MicrophoneSource;
            _streamDesktopSourceBox.Text = profile.DesktopAudioSource;
            _streamRecordingPathBox.Text = profile.RecordingPath;
            SetNumericValue(_streamMinimumSpaceControl, profile.MinimumFreeSpaceGb);
            _streamSelectSceneCheck.Checked = profile.SelectStartScene;
            _streamStartObsCheck.Checked = profile.StartObsStreaming;
            _streamStartServicesCheck.Checked = profile.StartPluginServices;
            if (!string.IsNullOrWhiteSpace(profile.LastSummary))
                _streamCheckSummary.Text = profile.LastSummary;
        }
        catch (Exception exception) { AppendLog("Stream-Check-Profil konnte nicht geladen werden: " + exception.Message); }
    }

    private StreamCheckConfig ApplyStreamCheckProfile(AppConfig config)
    {
        var profile = config.StreamCheck;
        profile.DisabledChecks = _streamCheckProfile.Items.Cast<StreamCheckOption>()
            .Where((_, index) => !_streamCheckProfile.GetItemChecked(index))
            .Select(option => option.Key).ToList();
        profile.StartScene = _streamStartSceneBox.Text.Trim();
        profile.MicrophoneSource = _streamMicSourceBox.Text.Trim();
        profile.DesktopAudioSource = _streamDesktopSourceBox.Text.Trim();
        profile.RecordingPath = _streamRecordingPathBox.Text.Trim();
        profile.MinimumFreeSpaceGb = decimal.ToInt32(_streamMinimumSpaceControl.Value);
        profile.SelectStartScene = _streamSelectSceneCheck.Checked;
        profile.StartObsStreaming = _streamStartObsCheck.Checked;
        profile.StartPluginServices = _streamStartServicesCheck.Checked;
        return profile;
    }

    private void SaveStreamCheckProfile()
    {
        try
        {
            var config = _configurationService.Load();
            ApplyStreamCheckProfile(config);
            _configurationService.SaveGuiSettings(config);
            if (_activeConfig is not null) _activeConfig.StreamCheck = config.StreamCheck;
            AppendLog("Stream-Check-Profil gespeichert.");
        }
        catch (Exception exception) { AppendLog("Stream-Check-Profil konnte nicht gespeichert werden: " + exception.Message); }
    }

    private async Task RunStreamChecksAsync(bool failedOnly)
    {
        if (_streamCheckCancellation is not null) return;
        _streamCheckCancellation = new CancellationTokenSource();
        SetStreamCheckButtons(false);
        try
        {
            var config = _activeConfig ?? _configurationService.Load();
            var profile = ApplyStreamCheckProfile(config);
            var checks = CreateStreamReadinessChecks(config);
            var service = new StreamCheckService(checks);
            HashSet<string>? only = failedOnly
                ? _lastStreamCheckResults.Where(result => result.IsFailure)
                    .Select(result => result.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null;
            if (failedOnly && (only is null || only.Count == 0))
            {
                AppendLog("Keine fehlgeschlagenen Stream-Checks zum Wiederholen.");
                return;
            }
            if (!failedOnly) { _lastStreamCheckResults.Clear(); _streamCheckResults.Items.Clear(); }
            var progress = new Progress<StreamCheckResult>(UpdateStreamCheckRow);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = await service.RunAsync(
                profile.DisabledChecks.ToHashSet(StringComparer.OrdinalIgnoreCase),
                only, progress, _streamCheckCancellation.Token);
            stopwatch.Stop();
            foreach (var result in results)
            {
                var index = _lastStreamCheckResults.FindIndex(item => item.Key == result.Key);
                if (index >= 0) _lastStreamCheckResults[index] = result;
                else _lastStreamCheckResults.Add(result);
            }
            var summary = StreamCheckService.CreateSummary(_lastStreamCheckResults);
            _streamCheckSummary.Text = summary;
            _streamCheckSummary.ForeColor = _lastStreamCheckResults.Any(result => result.IsFailure)
                ? ErrorColor : ActiveColor;
            profile.LastCheckUtc = DateTimeOffset.UtcNow;
            profile.LastDurationMilliseconds = (long)stopwatch.Elapsed.TotalMilliseconds;
            profile.LastSummary = summary;
            profile.LastFailedChecks = _lastStreamCheckResults.Where(result => result.IsFailure)
                .Select(result => result.Key).ToList();
            config.StreamCheck = profile;
            _configurationService.SaveGuiSettings(config);
            AppendLog("Stream-Check: " + summary);
        }
        catch (OperationCanceledException) { AppendLog("Stream-Check abgebrochen."); }
        catch (Exception exception) { AppendLog("Stream-Check fehlgeschlagen: " + exception.Message); }
        finally
        {
            _streamCheckCancellation.Dispose();
            _streamCheckCancellation = null;
            SetStreamCheckButtons(true);
        }
    }

    private IReadOnlyList<IStreamReadinessCheck> CreateStreamReadinessChecks(AppConfig config)
    {
        StreamCheckResult Result(string key, StreamCheckSeverity severity, string description,
            string error = "", string fix = "") => new(key,
            StreamCheckOptions.First(item => item.Key == key).Name,
            severity, description, error, fix);
        Task<StreamCheckResult> Done(StreamCheckResult result) => Task.FromResult(result);
        var checks = new List<IStreamReadinessCheck>();
        void Add(string key, bool critical, Func<CancellationToken, Task<StreamCheckResult>> run) =>
            checks.Add(new DelegateStreamReadinessCheck(key,
                StreamCheckOptions.First(item => item.Key == key).Name, critical, run));

        Add("twitch", true, token => Done(_twitch is not null && _broadcaster is not null
            ? Result("twitch", StreamCheckSeverity.Success, $"Twitch ist als {_broadcaster.DisplayName} verbunden.")
            : Result("twitch", StreamCheckSeverity.Error, "Twitch ist nicht verbunden.", "Plugin starten und Twitch-Anmeldung prüfen.", "raid")));
        Add("chat", true, token => Done(!config.Moderation.Enabled && !config.Minigame.Enabled && !config.MusicRequests.Enabled
            ? Result("chat", StreamCheckSeverity.Skipped, "Es sind keine Chatmodule aktiviert.")
            : _chatModerationTask is { IsCompleted: false }
                ? Result("chat", StreamCheckSeverity.Success, "Twitch-Chat ist verbunden.")
                : Result("chat", StreamCheckSeverity.Error, "Twitch-Chat ist nicht verbunden.", "Plugin-Dienste starten.", "raid")));
        Add("eventsub", true, token => Done(_eventSubTask is { IsCompleted: false }
            ? Result("eventsub", StreamCheckSeverity.Success, "EventSub ist aktiv.")
            : Result("eventsub", StreamCheckSeverity.Error, "EventSub ist nicht aktiv.", "Plugin-Dienste neu starten.", "raid")));
        Add("obs", true, token => Done(_obs?.IsConnected == true
            ? Result("obs", StreamCheckSeverity.Success, "OBS-WebSocket ist verbunden.")
            : Result("obs", StreamCheckSeverity.Error, "OBS ist nicht verbunden.", "OBS starten und WebSocket-Zugang prüfen.", "raid")));
        Add("scene", true, token => Done(_obs?.IsConnected == true && _obs.SceneExists(config.Player.Scene)
            ? Result("scene", StreamCheckSeverity.Success, $"OBS-Szene ‚{config.Player.Scene}‘ wurde gefunden.")
            : Result("scene", StreamCheckSeverity.Error, $"OBS-Szene ‚{config.Player.Scene}‘ wurde nicht gefunden.", "Szenennamen prüfen.", "raid")));
        Add("raid-source", true, token => Done(_obs?.IsConnected == true &&
            _obs.SourceExistsInScene(config.Player.Scene, config.Player.BrowserSource)
            ? Result("raid-source", StreamCheckSeverity.Success, $"Browserquelle ‚{config.Player.BrowserSource}‘ wurde gefunden.")
            : Result("raid-source", StreamCheckSeverity.Error, $"Browserquelle ‚{config.Player.BrowserSource}‘ wurde nicht gefunden.", "OBS-Quelle erstellen.", "create-source")));
        Add("player", true, async token =>
        {
            if (_player is null) return Result("player", StreamCheckSeverity.Error,
                "LocalPlayer läuft nicht.", "Plugin starten.", "raid");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http.GetAsync(_player.IdleUrl, token);
            return response.IsSuccessStatusCode
                ? Result("player", StreamCheckSeverity.Success, $"Browserquelle ist unter {_player.IdleUrl} erreichbar.")
                : Result("player", StreamCheckSeverity.Error, "Browserquelle antwortet nicht erfolgreich.", response.StatusCode.ToString());
        });
        Add("microphone", true, token => Done(string.IsNullOrWhiteSpace(config.StreamCheck.MicrophoneSource)
            ? Result("microphone", StreamCheckSeverity.Warning, "Keine Mikrofonquelle konfiguriert.")
            : _obs?.IsConnected == true && _obs.InputExists(config.StreamCheck.MicrophoneSource)
                ? Result("microphone", StreamCheckSeverity.Success, $"Mikrofon ‚{config.StreamCheck.MicrophoneSource}‘ ist vorhanden.")
                : Result("microphone", StreamCheckSeverity.Error, $"Mikrofon ‚{config.StreamCheck.MicrophoneSource}‘ wurde nicht gefunden.", "Quellennamen im Profil anpassen.")));
        Add("microphone-muted", true, token => Done(string.IsNullOrWhiteSpace(config.StreamCheck.MicrophoneSource)
            ? Result("microphone-muted", StreamCheckSeverity.Skipped, "Keine Mikrofonquelle konfiguriert.")
            : _obs?.IsConnected == true && _obs.InputExists(config.StreamCheck.MicrophoneSource) && !_obs.IsInputMuted(config.StreamCheck.MicrophoneSource)
                ? Result("microphone-muted", StreamCheckSeverity.Success, "Das Mikrofon ist nicht stumm.")
                : Result("microphone-muted", StreamCheckSeverity.Error, "Das Mikrofon ist stumm oder nicht erreichbar.", "Mikrofon in OBS aktivieren.")));
        Add("desktop-audio", true, token => Done(string.IsNullOrWhiteSpace(config.StreamCheck.DesktopAudioSource)
            ? Result("desktop-audio", StreamCheckSeverity.Warning, "Keine Desktop-Audioquelle konfiguriert.")
            : _obs?.IsConnected == true && _obs.InputExists(config.StreamCheck.DesktopAudioSource)
                ? Result("desktop-audio", StreamCheckSeverity.Success, $"Audioquelle ‚{config.StreamCheck.DesktopAudioSource}‘ ist vorhanden.")
                : Result("desktop-audio", StreamCheckSeverity.Error, $"Audioquelle ‚{config.StreamCheck.DesktopAudioSource}‘ wurde nicht gefunden.", "Quellennamen im Profil anpassen.")));
        Add("storage", false, token =>
        {
            var directory = config.StreamCheck.RecordingPath;
            if (string.IsNullOrWhiteSpace(directory) && _obs?.IsConnected == true)
                directory = _obs.GetRecordingDirectory();
            if (string.IsNullOrWhiteSpace(directory))
                directory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            var freeGb = new DriveInfo(root!).AvailableFreeSpace / 1024d / 1024d / 1024d;
            return Done(freeGb >= config.StreamCheck.MinimumFreeSpaceGb
                ? Result("storage", StreamCheckSeverity.Success, $"{freeGb:F1} GB Aufnahmespeicher sind frei.")
                : Result("storage", StreamCheckSeverity.Warning, $"Nur {freeGb:F1} GB sind frei.", $"Mindestens {config.StreamCheck.MinimumFreeSpaceGb} GB empfohlen."));
        });
        Add("title", false, async token =>
        {
            if (_twitch is null || _broadcaster is null) return Result("title", StreamCheckSeverity.Error, "Twitch ist nicht verbunden.");
            var channel = await _twitch.GetChannelInfoAsync(_broadcaster.Id, token);
            return !string.IsNullOrWhiteSpace(channel?.Title)
                ? Result("title", StreamCheckSeverity.Success, "Twitch-Streamtitel ist gesetzt: " + channel.Title)
                : Result("title", StreamCheckSeverity.Warning, "Der Twitch-Streamtitel ist leer.");
        });
        Add("category", false, async token =>
        {
            if (_twitch is null || _broadcaster is null) return Result("category", StreamCheckSeverity.Error, "Twitch ist nicht verbunden.");
            var channel = await _twitch.GetChannelInfoAsync(_broadcaster.Id, token);
            return !string.IsNullOrWhiteSpace(channel?.GameId)
                ? Result("category", StreamCheckSeverity.Success, "Twitch-Kategorie ist gesetzt: " + channel.GameName)
                : Result("category", StreamCheckSeverity.Warning, "Es ist keine Twitch-Kategorie gesetzt.");
        });
        Add("spotify", true, token => Done(!config.MusicRequests.Enabled
            ? Result("spotify", StreamCheckSeverity.Skipped, "Musikwünsche sind deaktiviert.")
            : _spotify?.IsConnected == true
                ? Result("spotify", StreamCheckSeverity.Success, "Spotify ist verbunden.")
                : Result("spotify", StreamCheckSeverity.Error, "Spotify ist nicht verbunden. Musikwünsche können nicht verwendet werden.", "Spotify im Bereich Musikwünsche verbinden.", "music")));
        Add("spotify-device", true, async token =>
        {
            if (!config.MusicRequests.Enabled) return Result("spotify-device", StreamCheckSeverity.Skipped, "Musikwünsche sind deaktiviert.");
            if (_spotify?.IsConnected != true) return Result("spotify-device", StreamCheckSeverity.Error, "Spotify ist nicht verbunden.");
            var devices = await _spotify.GetDevicesAsync(token);
            return devices.Count > 0
                ? Result("spotify-device", StreamCheckSeverity.Success, $"{devices.Count} Spotify-Gerät(e) verfügbar.")
                : Result("spotify-device", StreamCheckSeverity.Error, "Kein Spotify-Gerät verfügbar.", "Spotify auf einem Wiedergabegerät öffnen.", "music");
        });
        Add("chatbot", false, token => Done(!config.Moderation.Enabled && !config.Minigame.Enabled && !config.MusicRequests.Enabled
            ? Result("chatbot", StreamCheckSeverity.Skipped, "Chatmodule sind deaktiviert.")
            : _chatModerationTask is { IsCompleted: false } && (!config.Minigame.Enabled || _minigame is not null)
                ? Result("chatbot", StreamCheckSeverity.Success, "Chatbot und aktivierte Minigames sind betriebsbereit.")
                : Result("chatbot", StreamCheckSeverity.Error, "Chatbot oder Minigame ist nicht betriebsbereit.", "Plugin-Dienste neu starten.", "raid")));
        Add("oauth", true, token => Done(_twitchSession is not null
            ? Result("oauth", StreamCheckSeverity.Success, "Das Twitch-Token hat die erforderliche Berechtigungsprüfung bestanden.")
            : Result("oauth", StreamCheckSeverity.Error, "OAuth-Berechtigungen wurden noch nicht validiert.", "Twitch neu anmelden.", "raid")));
        Add("configuration", true, token =>
        {
            try { _ = _configurationService.Load(); return Done(Result("configuration", StreamCheckSeverity.Success, "Keine kritischen Konfigurationsfehler gefunden.")); }
            catch (Exception exception) { return Done(Result("configuration", StreamCheckSeverity.Error, "Die Konfiguration ist ungültig.", exception.Message)); }
        });
        return checks;
    }

    private void UpdateStreamCheckRow(StreamCheckResult result)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => UpdateStreamCheckRow(result))); return; }
        var item = _streamCheckResults.Items.Cast<ListViewItem>()
            .FirstOrDefault(row => string.Equals(row.Tag as string, result.Key, StringComparison.Ordinal));
        item ??= new ListViewItem { Tag = result.Key };
        if (item.ListView is null) _streamCheckResults.Items.Add(item);
        var status = result.Severity switch
        {
            StreamCheckSeverity.Running => "… Prüft",
            StreamCheckSeverity.Success => "✓ OK",
            StreamCheckSeverity.Warning => "⚠ Warnung",
            StreamCheckSeverity.Error => "✕ Fehler",
            StreamCheckSeverity.Skipped => "– Überspr.",
            _ => "○ Offen"
        };
        item.Text = status;
        while (item.SubItems.Count < 4) item.SubItems.Add("");
        item.SubItems[1].Text = result.Name;
        item.SubItems[2].Text = result.Description +
            (string.IsNullOrWhiteSpace(result.ErrorReason) ? "" : " " + result.ErrorReason);
        item.SubItems[3].Text = result.Duration == default ? "" : $"{result.Duration.TotalMilliseconds:F0} ms";
        item.ForeColor = result.Severity switch
        {
            StreamCheckSeverity.Success => ActiveColor,
            StreamCheckSeverity.Warning => WaitingColor,
            StreamCheckSeverity.Error => ErrorColor,
            _ => TextColor
        };
    }

    private void SetStreamCheckButtons(bool enabled)
    {
        _runStreamChecksButton.Enabled = enabled;
        _retryStreamChecksButton.Enabled = enabled;
        _startStreamButton.Enabled = enabled;
        _saveStreamCheckButton.Enabled = enabled;
    }

    private string FormatStreamCheckReport() =>
        _streamCheckSummary.Text + Environment.NewLine +
        string.Join(Environment.NewLine, _lastStreamCheckResults.Select(result =>
            $"[{result.Severity}] {result.Name}: {result.Description}" +
            (string.IsNullOrWhiteSpace(result.ErrorReason) ? "" : " – " + result.ErrorReason)));

    private void CopyStreamCheckResult()
    {
        if (_lastStreamCheckResults.Count == 0) return;
        Clipboard.SetText(FormatStreamCheckReport());
        AppendLog("Stream-Check-Ergebnis wurde kopiert.");
    }

    private async Task ExportStreamCheckAsync()
    {
        if (_lastStreamCheckResults.Count == 0) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON-Datei|*.json", FileName = $"RaidClip-Diagnose-{DateTime.Now:yyyy-MM-dd-HHmm}.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var export = new
        {
            GeneratedAt = DateTimeOffset.Now,
            Version = _updateService.CurrentDisplayVersion,
            Summary = _streamCheckSummary.Text,
            Results = _lastStreamCheckResults.Select(result => new
            {
                result.Key, result.Name, Status = result.Severity.ToString(),
                result.Description, result.ErrorReason,
                DurationMilliseconds = result.Duration.TotalMilliseconds
            })
        };
        await File.WriteAllTextAsync(dialog.FileName,
            JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
        AppendLog("Sichere Stream-Check-Diagnose exportiert: " + dialog.FileName);
    }

    private async Task FixSelectedStreamCheckAsync()
    {
        if (_streamCheckResults.SelectedItems.Count == 0) return;
        var key = _streamCheckResults.SelectedItems[0].Tag as string;
        var result = _lastStreamCheckResults.FirstOrDefault(item => item.Key == key);
        if (result?.FixAction == "create-source") { await CreateObsSourceAsync(); return; }
        if (result?.FixAction is "raid" or "music") ShowSection(result.FixAction);
        else MessageBox.Show(this, result?.ErrorReason ?? "Bitte die zugehörige Einstellung prüfen.",
            "Fehlerbehebung", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task StartStreamFromCheckAsync()
    {
        if (MessageBox.Show(this,
                "Soll RaidClip die Prüfung ausführen und anschließend den OBS-Stream starten?",
                "Stream starten", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            var config = _activeConfig ?? _configurationService.Load();
            var profile = ApplyStreamCheckProfile(config);
            if (profile.StartPluginServices && _shutdown is null) await StartPluginAsync();
            await RunStreamChecksAsync(false);
            if (_lastStreamCheckResults.Any(result => result.IsFailure))
            {
                MessageBox.Show(this, "Der Stream wurde wegen kritischer Fehler nicht gestartet.",
                    "Nicht streambereit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_obs?.IsConnected != true) throw new InvalidOperationException("OBS ist nicht verbunden.");
            if (profile.SelectStartScene && !string.IsNullOrWhiteSpace(profile.StartScene))
                _obs.SetCurrentScene(profile.StartScene);
            if (profile.StartObsStreaming) _obs.StartStreaming();
            AppendLog("OBS-Stream wurde nach erfolgreichem Stream-Check gestartet.");
        }
        catch (Exception exception) { AppendLog("Stream konnte nicht gestartet werden: " + exception.Message); }
    }

    private sealed record StreamCheckOption(string Key, string Name)
    {
        public override string ToString() => Name;
    }
}
