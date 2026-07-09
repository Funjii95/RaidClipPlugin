using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Button _autoDiscordClipPosterNavButton =
        CreateNavigationTile("📣  Discord Clip Poster",
            "Automatische Clip-Posts");
    private readonly Panel _autoDiscordClipPosterPage = new()
    {
        Dock = DockStyle.Fill,
        Visible = false,
        BackColor = BackgroundColor
    };

    private readonly CheckBox _autoPosterEnabledCheck =
        NewCheck("Auto Discord Clip Poster aktivieren", false);
    private readonly CheckBox _autoPosterCheckOnStartCheck =
        NewCheck("Beim Plugin-Start direkt prüfen", true);
    private readonly CheckBox _autoPosterEmbedCheck =
        NewCheck("Discord Embed verwenden", true);
    private readonly CheckBox _autoPosterThumbnailCheck =
        NewCheck("Thumbnail im Embed anzeigen", true);
    private readonly CheckBox _autoPosterIgnoreBotCheck =
        NewCheck("Clips vom Bot selbst ignorieren", false);

    private readonly TextBox _autoPosterBroadcasterBox = new()
        { Width = 220, MaxLength = 80 };
    private readonly TextBox _autoPosterWebhookBox = new()
        { Width = 520, UseSystemPasswordChar = true };
    private readonly TextBox _autoPosterChannelIdBox = new()
        { Width = 220, MaxLength = 40 };
    private readonly TextBox _autoPosterBotNameBox = new()
        { Width = 180, MaxLength = 80, Text = "raidclipplugin" };
    private readonly TextBox _autoPosterIgnoredCreatorsBox = new()
        { Width = 520 };

    private readonly NumericUpDown _autoPosterIntervalControl =
        CreateIntegerControl(5, 1, 1440);
    private readonly NumericUpDown _autoPosterMaxClipsControl =
        CreateIntegerControl(25, 1, 500);
    private readonly NumericUpDown _autoPosterMinDurationControl =
        CreateIntegerControl(0, 0, 600);
    private readonly NumericUpDown _autoPosterMinViewsControl =
        CreateIntegerControl(0, 0, 1_000_000);

    private readonly ComboBox _autoPosterTimeRangeBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 220
    };

    private readonly DateTimePicker _autoPosterCustomStartPicker = new()
    {
        Width = 220,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd.MM.yyyy HH:mm"
    };

    private readonly DateTimePicker _autoPosterCustomEndPicker = new()
    {
        Width = 220,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd.MM.yyyy HH:mm"
    };

    private readonly Button _autoPosterSaveButton =
        NewActionButton("Einstellungen speichern");
    private readonly Button _autoPosterCheckButton =
        NewActionButton("Zeitraum jetzt prüfen");
    private readonly Button _autoPosterCheck24Button =
        NewActionButton("Letzte 24h prüfen");
    private readonly Button _autoPosterCheck7Button =
        NewActionButton("Letzte 7 Tage prüfen");
    private readonly Button _autoPosterTestDiscordButton =
        NewActionButton("Testnachricht an Discord senden");

    private readonly Label _autoPosterStatusLabel = new()
    {
        Text = "Auto-Poster: Nicht gestartet",
        AutoSize = true,
        ForeColor = MutedTextColor,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Margin = new Padding(8)
    };

    private AutoDiscordClipPosterService? _autoDiscordClipPoster;
    private Task? _autoDiscordClipPosterTask;

    private void InitializeAutoDiscordClipPosterEvents()
    {
        _autoDiscordClipPosterNavButton.Click += (_, _) =>
            ShowSection("auto-discord-poster");
        _autoPosterSaveButton.Click += (_, _) => SaveSettingsFromControls();
        _autoPosterCheckButton.Click += async (_, _) =>
            await RunAutoDiscordClipPosterCheckAsync(null);
        _autoPosterCheck24Button.Click += async (_, _) =>
            await RunAutoDiscordClipPosterCheckAsync(
                ClipPosterTimeRange.Last24Hours);
        _autoPosterCheck7Button.Click += async (_, _) =>
            await RunAutoDiscordClipPosterCheckAsync(
                ClipPosterTimeRange.Last7Days);
        _autoPosterTestDiscordButton.Click += async (_, _) =>
            await SendAutoPosterTestMessageAsync();
        _autoPosterTimeRangeBox.SelectedIndexChanged += (_, _) =>
            UpdateAutoPosterCustomRangeVisibility();
    }

    private void BuildAutoDiscordClipPosterPage()
    {
        _autoPosterTimeRangeBox.Items.Clear();
        _autoPosterTimeRangeBox.Items.AddRange(new object[]
        {
            "Letzte 1 Stunde",
            "Letzte 6 Stunden",
            "Letzte 12 Stunden",
            "Letzte 24 Stunden",
            "Letzte 3 Tage",
            "Letzte 7 Tage",
            "Letzte 14 Tage",
            "Letzte 30 Tage",
            "Benutzerdefiniert"
        });
        _autoPosterTimeRangeBox.SelectedIndex = 3;

        StylePrimaryButton(_autoPosterSaveButton);
        StylePrimaryButton(_autoPosterCheckButton);
        StylePrimaryButton(_autoPosterCheck24Button);
        StylePrimaryButton(_autoPosterCheck7Button);
        StylePrimaryButton(_autoPosterTestDiscordButton);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(26),
            BackColor = BackgroundColor,
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            Text = "Discord Clip Poster",
            AutoSize = true,
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "Prüft Twitch-Clips in einem gewählten Zeitraum und postet neue Clips automatisch in Discord.",
            AutoSize = true,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 0, 0, 18)
        }, 0, 1);

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = SurfaceColor,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 14)
        };

        settings.Controls.Add(_autoPosterEnabledCheck);
        settings.Controls.Add(_autoPosterCheckOnStartCheck);
        settings.Controls.Add(_autoPosterEmbedCheck);
        settings.Controls.Add(_autoPosterThumbnailCheck);
        settings.Controls.Add(CreateSettingEditor("Twitch-Kanal", _autoPosterBroadcasterBox));
        settings.Controls.Add(CreateSettingEditor("Discord Webhook URL", _autoPosterWebhookBox));
        settings.Controls.Add(CreateSettingEditor("Discord Channel ID optional", _autoPosterChannelIdBox));
        settings.Controls.Add(CreateSettingEditor("Prüfintervall Minuten", _autoPosterIntervalControl));
        settings.Controls.Add(CreateSettingEditor("Clip-Zeitraum prüfen", _autoPosterTimeRangeBox));
        settings.Controls.Add(CreateSettingEditor("Startdatum / Startzeit", _autoPosterCustomStartPicker));
        settings.Controls.Add(CreateSettingEditor("Enddatum / Endzeit", _autoPosterCustomEndPicker));
        settings.Controls.Add(CreateSettingEditor("Max. Clips pro Check", _autoPosterMaxClipsControl));
        settings.Controls.Add(CreateSettingEditor("Mindestlänge Sekunden", _autoPosterMinDurationControl));
        settings.Controls.Add(CreateSettingEditor("Mindest-Viewcount", _autoPosterMinViewsControl));
        settings.Controls.Add(_autoPosterIgnoreBotCheck);
        settings.Controls.Add(CreateSettingEditor("Bot-Name", _autoPosterBotNameBox));
        settings.Controls.Add(CreateSettingEditor("Ignorierte Creator", _autoPosterIgnoredCreatorsBox));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = BackgroundColor,
            Margin = new Padding(0, 0, 0, 10)
        };
        actions.Controls.Add(_autoPosterSaveButton);
        actions.Controls.Add(_autoPosterCheckButton);
        actions.Controls.Add(_autoPosterCheck24Button);
        actions.Controls.Add(_autoPosterCheck7Button);
        actions.Controls.Add(_autoPosterTestDiscordButton);
        actions.Controls.Add(_autoPosterStatusLabel);

        root.Controls.Add(settings, 0, 2);
        root.Controls.Add(actions, 0, 3);
        _autoDiscordClipPosterPage.Controls.Add(root);
        UpdateAutoPosterCustomRangeVisibility();
    }

    private void LoadAutoDiscordClipPosterSettings(AppConfig config)
    {
        var poster = config.AutoDiscordClipPoster;
        _autoPosterEnabledCheck.Checked = poster.Enabled;
        _autoPosterBroadcasterBox.Text = poster.BroadcasterLogin;
        _autoPosterWebhookBox.Text = poster.WebhookUrl;
        _autoPosterChannelIdBox.Text = poster.DiscordChannelId;
        SetNumericValue(_autoPosterIntervalControl, poster.IntervalMinutes);
        SetNumericValue(_autoPosterMaxClipsControl, poster.MaxClipsPerCheck);
        SetNumericValue(_autoPosterMinDurationControl,
            (int)Math.Round(poster.MinimumDurationSeconds));
        SetNumericValue(_autoPosterMinViewsControl, poster.MinimumViewCount);
        _autoPosterIgnoreBotCheck.Checked = poster.IgnoreBotCreatedClips;
        _autoPosterBotNameBox.Text = poster.BotName;
        _autoPosterIgnoredCreatorsBox.Text = poster.IgnoredCreators;
        _autoPosterCheckOnStartCheck.Checked = poster.CheckOnPluginStart;
        _autoPosterEmbedCheck.Checked = poster.UseEmbed;
        _autoPosterThumbnailCheck.Checked = poster.UseThumbnail;
        _autoPosterTimeRangeBox.SelectedIndex = TimeRangeToIndex(poster.TimeRange);
        _autoPosterCustomStartPicker.Value =
            SafePickerDate(poster.CustomStart.LocalDateTime);
        _autoPosterCustomEndPicker.Value =
            SafePickerDate(poster.CustomEnd.LocalDateTime);
        UpdateAutoPosterCustomRangeVisibility();
    }

    private void ReadAutoDiscordClipPosterSettings(AppConfig config)
    {
        var poster = config.AutoDiscordClipPoster;
        poster.Enabled = _autoPosterEnabledCheck.Checked;
        poster.BroadcasterLogin = _autoPosterBroadcasterBox.Text.Trim();
        poster.WebhookUrl = _autoPosterWebhookBox.Text.Trim();
        poster.DiscordChannelId = _autoPosterChannelIdBox.Text.Trim();
        poster.IntervalMinutes = decimal.ToInt32(_autoPosterIntervalControl.Value);
        poster.MaxClipsPerCheck = decimal.ToInt32(_autoPosterMaxClipsControl.Value);
        poster.MinimumDurationSeconds = decimal.ToInt32(_autoPosterMinDurationControl.Value);
        poster.MinimumViewCount = decimal.ToInt32(_autoPosterMinViewsControl.Value);
        poster.IgnoreBotCreatedClips = _autoPosterIgnoreBotCheck.Checked;
        poster.BotName = _autoPosterBotNameBox.Text.Trim();
        poster.IgnoredCreators = _autoPosterIgnoredCreatorsBox.Text.Trim();
        poster.CheckOnPluginStart = _autoPosterCheckOnStartCheck.Checked;
        poster.UseEmbed = _autoPosterEmbedCheck.Checked;
        poster.UseThumbnail = _autoPosterThumbnailCheck.Checked;
        poster.TimeRange = IndexToTimeRange(_autoPosterTimeRangeBox.SelectedIndex);
        poster.CustomStart = new DateTimeOffset(_autoPosterCustomStartPicker.Value);
        poster.CustomEnd = new DateTimeOffset(_autoPosterCustomEndPicker.Value);
    }

    private async Task StartAutoDiscordClipPosterAsync(
        AppConfig config,
        TwitchService twitch,
        TwitchUser broadcaster,
        CancellationToken cancellationToken)
    {
        if (!config.AutoDiscordClipPoster.Enabled)
        {
            SetAutoPosterStatus("Auto-Poster deaktiviert", InactiveColor);
            return;
        }

        _autoDiscordClipPoster?.Dispose();
        _autoDiscordClipPoster = new AutoDiscordClipPosterService(
            config.AutoDiscordClipPoster, broadcaster, twitch, AppendLog);
        _autoDiscordClipPoster.StatusChanged += SetAutoPosterStatus;
        _autoDiscordClipPosterTask = _autoDiscordClipPoster.RunAsync(cancellationToken);
        SetAutoPosterStatus("Auto-Poster gestartet", ActiveColor);
        AppendLog("Auto Discord Clip Poster ist aktiv.");

        if (config.AutoDiscordClipPoster.CheckOnPluginStart)
            _ = _autoDiscordClipPoster.CheckNowAsync(cancellationToken);
    }

    private void StopAutoDiscordClipPoster()
    {
        _autoDiscordClipPoster?.Dispose();
        _autoDiscordClipPoster = null;
        _autoDiscordClipPosterTask = null;
        SetAutoPosterStatus("Auto-Poster gestoppt", InactiveColor);
    }

    private async Task RunAutoDiscordClipPosterCheckAsync(
        ClipPosterTimeRange? forcedRange)
    {
        try
        {
            var config = ReadSettingsFromControls();
            if (forcedRange is not null)
                config.AutoDiscordClipPoster.TimeRange = forcedRange.Value;

            if (_autoDiscordClipPoster is null)
            {
                if (_twitch is null || _broadcaster is null)
                    throw new InvalidOperationException(
                        "Bitte Plugin starten, bevor Clips geprüft werden.");
                _autoDiscordClipPoster = new AutoDiscordClipPosterService(
                    config.AutoDiscordClipPoster, _broadcaster, _twitch, AppendLog);
                _autoDiscordClipPoster.StatusChanged += SetAutoPosterStatus;
            }
            else
            {
                _autoDiscordClipPoster.UpdateConfig(
                    config.AutoDiscordClipPoster, _broadcaster);
            }

            await _autoDiscordClipPoster.CheckNowAsync(
                _shutdown?.Token ?? CancellationToken.None);
        }
        catch (Exception exception)
        {
            AppendLog("Auto Discord Clip Poster Check fehlgeschlagen: " +
                      exception.Message);
            SetAutoPosterStatus("Auto-Poster Fehler", ErrorColor);
        }
    }

    private async Task SendAutoPosterTestMessageAsync()
    {
        try
        {
            var config = ReadSettingsFromControls();
            using var service = new AutoDiscordClipPosterService(
                config.AutoDiscordClipPoster,
                _broadcaster ?? new TwitchUser("", "", config.Twitch.BroadcasterLogin),
                _twitch ?? throw new InvalidOperationException(
                    "Bitte Plugin starten, damit Twitch verfügbar ist."),
                AppendLog);
            await service.SendTestMessageAsync(CancellationToken.None);
            SetAutoPosterStatus("Testnachricht gesendet", ActiveColor);
        }
        catch (Exception exception)
        {
            AppendLog("Auto Discord Clip Poster Testnachricht fehlgeschlagen: " +
                      exception.Message);
            SetAutoPosterStatus("Test fehlgeschlagen", ErrorColor);
        }
    }

    private void SetAutoPosterStatus(string text, Color color)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetAutoPosterStatus(text, color)));
            return;
        }
        _autoPosterStatusLabel.Text = text;
        _autoPosterStatusLabel.ForeColor = color;
    }

    private void UpdateAutoPosterCustomRangeVisibility()
    {
        var custom = IndexToTimeRange(_autoPosterTimeRangeBox.SelectedIndex) ==
                     ClipPosterTimeRange.Custom;
        _autoPosterCustomStartPicker.Parent!.Visible = custom;
        _autoPosterCustomEndPicker.Parent!.Visible = custom;
    }

    private static int TimeRangeToIndex(ClipPosterTimeRange range) => range switch
    {
        ClipPosterTimeRange.Last1Hour => 0,
        ClipPosterTimeRange.Last6Hours => 1,
        ClipPosterTimeRange.Last12Hours => 2,
        ClipPosterTimeRange.Last24Hours => 3,
        ClipPosterTimeRange.Last3Days => 4,
        ClipPosterTimeRange.Last7Days => 5,
        ClipPosterTimeRange.Last14Days => 6,
        ClipPosterTimeRange.Last30Days => 7,
        ClipPosterTimeRange.Custom => 8,
        _ => 3
    };

    private static ClipPosterTimeRange IndexToTimeRange(int index) => index switch
    {
        0 => ClipPosterTimeRange.Last1Hour,
        1 => ClipPosterTimeRange.Last6Hours,
        2 => ClipPosterTimeRange.Last12Hours,
        3 => ClipPosterTimeRange.Last24Hours,
        4 => ClipPosterTimeRange.Last3Days,
        5 => ClipPosterTimeRange.Last7Days,
        6 => ClipPosterTimeRange.Last14Days,
        7 => ClipPosterTimeRange.Last30Days,
        8 => ClipPosterTimeRange.Custom,
        _ => ClipPosterTimeRange.Last24Hours
    };

    private static DateTime SafePickerDate(DateTime value)
    {
        if (value < DateTimePicker.MinimumDateTime)
            return DateTime.Now.AddDays(-1);
        if (value > DateTimePicker.MaximumDateTime)
            return DateTime.Now;
        return value;
    }
}
