using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public partial class MainForm
{
    private readonly Button _clipDiscordNavButton = CreateNavigationTile(
        "◎  Clips", "!clip und Discord-Veröffentlichung");
    private readonly Panel _clipDiscordPage = new()
        { Dock = DockStyle.Fill, Visible = false };
    private readonly CheckBox _clipCommandEnabledCheck =
        NewCheck("Clip-Command aktivieren", false);
    private readonly TextBox _clipCommandBox = new()
        { Text = "!clip", Width = 180, MaxLength = 30 };
    private readonly TextBox _clipAliasesBox = new()
        { Text = "!createclip", Width = 360 };
    private readonly TextBox _clipDefaultTitleBox = new()
        { Text = "Clip von {username}", Width = 420 };
    private readonly NumericUpDown _clipDurationControl =
        CreateIntegerControl(30, 5, 60);
    private readonly NumericUpDown _clipTitleLengthControl =
        CreateIntegerControl(100, 1, 140);
    private readonly CheckBox _clipChatResponsesCheck =
        NewCheck("Chat-Rückmeldungen aktivieren", true);
    private readonly CheckBox _clipBroadcasterCheck = NewCheck("Broadcaster", true);
    private readonly CheckBox _clipModeratorsCheck = NewCheck("Moderatoren", true);
    private readonly CheckBox _clipVipsCheck = NewCheck("VIPs", true);
    private readonly CheckBox _clipSubscribersCheck = NewCheck("Subscriber", false);
    private readonly CheckBox _clipFollowersCheck = NewCheck("Follower", false);
    private readonly CheckBox _clipEveryoneCheck = NewCheck("Alle Zuschauer", false);
    private readonly TextBox _clipAllowedUsersBox = new()
        { Width = 520, Multiline = true, Height = 70 };
    private readonly TextBox _clipBlockedUsersBox = new()
        { Width = 520, Multiline = true, Height = 70 };
    private readonly NumericUpDown _clipGlobalCooldownControl =
        CreateIntegerControl(30, 0, 86400);
    private readonly NumericUpDown _clipUserCooldownControl =
        CreateIntegerControl(120, 0, 86400);
    private readonly NumericUpDown _clipMaximumStreamControl =
        CreateIntegerControl(50, 1, 1000);
    private readonly NumericUpDown _clipMaximumUserControl =
        CreateIntegerControl(5, 1, 1000);
    private readonly CheckBox _clipQueueCheck =
        NewCheck("Warteschlange aktivieren", false);
    private readonly NumericUpDown _clipQueueSizeControl =
        CreateIntegerControl(5, 1, 100);
    private readonly CheckBox _discordClipsEnabledCheck =
        NewCheck("Discord-Veröffentlichung aktivieren", false);
    private readonly CheckBox _discordInviteEnabledCheck =
        NewCheck("Discord-Einladungsbefehl aktivieren", false);
    private readonly TextBox _discordInviteCommandBox = new()
        { Text = "!raidpluginjoindc", Width = 210, MaxLength = 30 };
    private readonly TextBox _discordInviteUrlBox = new()
        { Width = 360, PlaceholderText = "https://discord.gg/..." };
    private readonly TextBox _discordInviteMessageBox = new()
        { Width = 560, Text = "@{username}, komm auf unseren Discord: {inviteUrl}" };
    private readonly NumericUpDown _discordInviteCooldownControl =
        CreateIntegerControl(30, 0, 86400);
    private readonly TextBox _discordBotTokenBox = new()
        { Width = 420, UseSystemPasswordChar = true };
    private readonly TextBox _discordGuildIdBox = new()
        { Width = 260, MaxLength = 30 };
    private readonly CheckBox _discordEmbedCheck = NewCheck("Embed verwenden", true);
    private readonly CheckBox _discordThumbnailCheck =
        NewCheck("Vorschaubild verwenden", true);
    private readonly TextBox _discordColorBox = new()
        { Width = 100, Text = "#9146FF", MaxLength = 7 };
    private readonly TextBox _discordFooterBox = new()
        { Width = 240, Text = "RaidClipPlugin", MaxLength = 200 };
    private readonly TextBox _discordRoleBox = new()
        { Width = 240, MaxLength = 30 };
    private readonly TextBox _discordMessageTemplateBox = new()
        { Width = 650, Height = 100, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly DataGridView _discordChannelsGrid = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoGenerateColumns = false,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };
    private readonly DataGridView _clipMessagesGrid = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoGenerateColumns = false,
        RowHeadersVisible = false
    };
    private readonly Button _addDiscordChannelButton = NewActionButton("Channel hinzufügen");
    private readonly Button _removeDiscordChannelButton = NewActionButton("Auswahl entfernen");
    private readonly Button _validateDiscordButton = NewActionButton("Channels prüfen");
    private readonly Button _testDiscordButton = NewActionButton("Testnachricht senden");
    private readonly Button _previewDiscordButton = NewActionButton("Embed-Vorschau");
    private readonly Button _saveClipDiscordButton = NewActionButton("Clips & Discord speichern");
    private readonly Label _clipTwitchStatus = NewClipStatus("Twitch: Nicht verbunden");
    private readonly Label _clipScopeStatus = NewClipStatus("clips:edit: Unbekannt");
    private readonly Label _clipDiscordStatus = NewClipStatus("Discord: Nicht geprüft");
    private readonly Label _clipChannelCountStatus = NewClipStatus("Channels: 0");
    private readonly Label _clipLastSuccessStatus = NewClipStatus("Letzter Clip: –");
    private readonly Label _clipLastDiscordStatus = NewClipStatus("Letzter Discord-Versand: –");
    private readonly Label _clipLastErrorStatus = NewClipStatus("Letzter Fehler: –");
    private readonly DiscordCredentialStore _discordCredentialStore = new();
    private DiscordCredentials _discordCredentials = new();
    private ClipCommandService? _clipCommandService;
    private DiscordClipService? _discordClipService;
    private DiscordInviteCommandService? _discordInviteService;
    private Task? _clipCommandTask;

    private static Label NewClipStatus(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = MutedTextColor,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        Margin = new Padding(8, 8, 16, 8)
    };

    private void InitializeClipDiscordEvents()
    {
        _clipDiscordNavButton.Click += (_, _) => ShowSection("clip-discord");
        _addDiscordChannelButton.Click += (_, _) => AddDiscordChannelRow();
        _removeDiscordChannelButton.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _discordChannelsGrid.SelectedRows)
                if (!row.IsNewRow) _discordChannelsGrid.Rows.Remove(row);
            UpdateClipChannelCount();
        };
        _validateDiscordButton.Click += async (_, _) => await ValidateDiscordAsync();
        _testDiscordButton.Click += async (_, _) => await TestDiscordAsync();
        _previewDiscordButton.Click += (_, _) => ShowDiscordPreview();
        _discordChannelsGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_discordChannelsGrid.IsCurrentCellDirty)
                _discordChannelsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _discordChannelsGrid.CellValueChanged += (_, _) => UpdateClipChannelCount();
        _saveClipDiscordButton.Click += async (_, _) =>
            await SaveClipDiscordSettingsAsync();
    }

    private void BuildClipDiscordPage()
    {
        ConfigureDiscordGrids();
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var heading = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        heading.Controls.Add(new Label
        {
            Text = "Clips & Discord", AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = TextColor
        });
        heading.Controls.Add(new Label
        {
            Text = "Twitch-Clips per Chat erstellen und automatisch veröffentlichen",
            AutoSize = true, ForeColor = MutedTextColor
        });
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(_saveClipDiscordButton, 1, 0);

        var status = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true
        };
        status.Controls.AddRange(new Control[]
        {
            _clipTwitchStatus, _clipScopeStatus, _clipDiscordStatus,
            _clipChannelCountStatus, _clipLastSuccessStatus,
            _clipLastDiscordStatus, _clipLastErrorStatus
        });

        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddClipTab(tabs, "Clip-Command", BuildClipCommandTab());
        AddClipTab(tabs, "Berechtigungen", BuildClipPermissionsTab());
        AddClipTab(tabs, "Cooldowns & Limits", BuildClipLimitsTab());
        AddClipTab(tabs, "Discord", BuildDiscordTab());
        AddClipTab(tabs, "Chatmeldungen", _clipMessagesGrid);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(tabs, 0, 2);
        _clipDiscordPage.Controls.Add(layout);
    }

    private Control BuildClipCommandTab()
    {
        var flow = NewClipFlow();
        flow.Controls.Add(_clipCommandEnabledCheck);
        flow.Controls.Add(CreateSettingEditor("Command", _clipCommandBox));
        flow.Controls.Add(CreateSettingEditor("Aliase (Komma/Zeile)", _clipAliasesBox));
        flow.Controls.Add(CreateSettingEditor("Standardtitel", _clipDefaultTitleBox));
        flow.Controls.Add(CreateSettingEditor("Clip-Dauer (Sek.)", _clipDurationControl));
        flow.Controls.Add(CreateSettingEditor("Max. Titellänge", _clipTitleLengthControl));
        flow.Controls.Add(_clipChatResponsesCheck);
        flow.Controls.Add(new Label
        {
            Text = "Platzhalter: {username}, {channel}, {game}, {date}, {time}",
            AutoSize = true, ForeColor = MutedTextColor, Margin = new Padding(8, 24, 8, 8)
        });
        return flow;
    }

    private Control BuildClipPermissionsTab()
    {
        var flow = NewClipFlow();
        flow.Controls.Add(_clipBroadcasterCheck);
        flow.Controls.Add(_clipModeratorsCheck);
        flow.Controls.Add(_clipVipsCheck);
        flow.Controls.Add(_clipSubscribersCheck);
        flow.Controls.Add(_clipFollowersCheck);
        flow.Controls.Add(_clipEveryoneCheck);
        flow.Controls.Add(CreateSettingEditor("Erlaubte Nutzer", _clipAllowedUsersBox));
        flow.Controls.Add(CreateSettingEditor("Nutzer-Blacklist", _clipBlockedUsersBox));
        return flow;
    }

    private Control BuildClipLimitsTab()
    {
        var flow = NewClipFlow();
        flow.Controls.Add(CreateSettingEditor("Globaler Cooldown", _clipGlobalCooldownControl));
        flow.Controls.Add(CreateSettingEditor("Nutzer-Cooldown", _clipUserCooldownControl));
        flow.Controls.Add(CreateSettingEditor("Max. Clips je Stream", _clipMaximumStreamControl));
        flow.Controls.Add(CreateSettingEditor("Max. Clips je Nutzer", _clipMaximumUserControl));
        flow.Controls.Add(_clipQueueCheck);
        flow.Controls.Add(CreateSettingEditor("Max. Warteschlange", _clipQueueSizeControl));
        return flow;
    }

    private Control BuildDiscordTab()
    {
        var options = NewClipFlow();
        options.Controls.Add(_discordInviteEnabledCheck);
        options.Controls.Add(CreateSettingEditor("Twitch-Command", _discordInviteCommandBox));
        options.Controls.Add(CreateSettingEditor("Discord-Einladungslink", _discordInviteUrlBox));
        options.Controls.Add(CreateSettingEditor("Einladungs-Chattext", _discordInviteMessageBox));
        options.Controls.Add(CreateSettingEditor("Nutzer-Cooldown (Sek.)", _discordInviteCooldownControl));
        options.Controls.Add(_discordClipsEnabledCheck);
        options.Controls.Add(CreateSettingEditor("Bot-Token (verschlüsselt)", _discordBotTokenBox));
        options.Controls.Add(CreateSettingEditor("Server-ID", _discordGuildIdBox));
        options.Controls.Add(_discordEmbedCheck);
        options.Controls.Add(_discordThumbnailCheck);
        options.Controls.Add(CreateSettingEditor("Embed-Farbe", _discordColorBox));
        options.Controls.Add(CreateSettingEditor("Footer", _discordFooterBox));
        options.Controls.Add(CreateSettingEditor("Rollen-ID (optional)", _discordRoleBox));
        options.Controls.Add(CreateSettingEditor("Nachrichtenformat", _discordMessageTemplateBox));
        options.Controls.Add(_addDiscordChannelButton);
        options.Controls.Add(_removeDiscordChannelButton);
        options.Controls.Add(_validateDiscordButton);
        options.Controls.Add(_testDiscordButton);
        options.Controls.Add(_previewDiscordButton);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 390));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(options, 0, 0);
        layout.Controls.Add(_discordChannelsGrid, 0, 1);
        return layout;
    }

    private static FlowLayoutPanel NewClipFlow() => new()
    {
        Dock = DockStyle.Fill, AutoScroll = true,
        FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
        Padding = new Padding(8)
    };

    private static void AddClipTab(TabControl tabs, string text, Control control)
    {
        var page = new TabPage(text) { BackColor = SurfaceColor, Padding = new Padding(8) };
        control.Dock = DockStyle.Fill;
        page.Controls.Add(control);
        tabs.TabPages.Add(page);
    }

    private void ConfigureDiscordGrids()
    {
        if (_discordChannelsGrid.Columns.Count == 0)
        {
            _discordChannelsGrid.Columns.Add(new DataGridViewCheckBoxColumn
                { Name = "Enabled", HeaderText = "Aktiv", Width = 55 });
            _discordChannelsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "ChannelId", HeaderText = "Channel-ID", Width = 180 });
            _discordChannelsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "Name", HeaderText = "Name", Width = 150 });
            _discordChannelsGrid.Columns.Add(new DataGridViewCheckBoxColumn
                { Name = "Webhook", HeaderText = "Webhook", Width = 75 });
            _discordChannelsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "WebhookUrl", HeaderText = "Webhook-URL (verschlüsselt)", Width = 260 });
            _discordChannelsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "Template", HeaderText = "Eigenes Nachrichtenformat", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        }
        if (_clipMessagesGrid.Columns.Count == 0)
        {
            _clipMessagesGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "Key", HeaderText = "Ereignis", ReadOnly = true, Width = 150 });
            _clipMessagesGrid.Columns.Add(new DataGridViewCheckBoxColumn
                { Name = "Enabled", HeaderText = "Aktiv", Width = 60 });
            _clipMessagesGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "Text", HeaderText = "Chatnachricht", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        }
    }

    private void AddDiscordChannelRow(DiscordClipChannelConfig? channel = null)
    {
        var index = _discordChannelsGrid.Rows.Add(
            channel?.Enabled ?? true, channel?.ChannelId ?? "",
            channel?.Name ?? "", channel?.UseWebhook ?? false,
            channel is not null && _discordCredentials.WebhookUrls.TryGetValue(
                channel.ChannelId, out var webhook) ? webhook : "",
            channel?.MessageTemplate ?? "");
        _discordChannelsGrid.Rows[index].Selected = true;
        UpdateClipChannelCount();
    }

    private void LoadClipDiscordSettings(AppConfig config)
    {
        var clip = config.ClipCommand;
        var discord = config.DiscordClips;
        _discordCredentials = _discordCredentialStore.Load();
        _clipCommandEnabledCheck.Checked = clip.Enabled;
        _clipCommandBox.Text = clip.Command;
        _clipAliasesBox.Text = string.Join(", ", clip.Aliases);
        _clipDefaultTitleBox.Text = clip.DefaultTitle;
        SetNumericValue(_clipDurationControl, clip.DurationSeconds);
        SetNumericValue(_clipTitleLengthControl, clip.MaximumTitleLength);
        _clipChatResponsesCheck.Checked = clip.ChatResponsesEnabled;
        _clipBroadcasterCheck.Checked = clip.AllowedRoles.Broadcaster;
        _clipModeratorsCheck.Checked = clip.AllowedRoles.Moderators;
        _clipVipsCheck.Checked = clip.AllowedRoles.Vips;
        _clipSubscribersCheck.Checked = clip.AllowedRoles.Subscribers;
        _clipFollowersCheck.Checked = clip.AllowedRoles.Followers;
        _clipEveryoneCheck.Checked = clip.AllowedRoles.Everyone;
        _clipAllowedUsersBox.Text = string.Join(", ", clip.AllowedUsers);
        _clipBlockedUsersBox.Text = string.Join(", ", clip.BlockedUsers);
        SetNumericValue(_clipGlobalCooldownControl, clip.GlobalCooldownSeconds);
        SetNumericValue(_clipUserCooldownControl, clip.UserCooldownSeconds);
        SetNumericValue(_clipMaximumStreamControl, clip.MaximumClipsPerStream);
        SetNumericValue(_clipMaximumUserControl, clip.MaximumClipsPerUserPerStream);
        _clipQueueCheck.Checked = clip.QueueEnabled;
        SetNumericValue(_clipQueueSizeControl, clip.MaximumQueueSize);
        _discordInviteEnabledCheck.Checked = discord.InviteCommandEnabled;
        _discordInviteCommandBox.Text = discord.InviteCommand;
        _discordInviteUrlBox.Text = discord.InviteUrl;
        _discordInviteMessageBox.Text = discord.InviteMessage;
        SetNumericValue(_discordInviteCooldownControl,
            discord.InviteCooldownSeconds);
        _discordClipsEnabledCheck.Checked = discord.Enabled;
        _discordBotTokenBox.Text = _discordCredentials.BotToken;
        _discordGuildIdBox.Text = discord.GuildId;
        _discordEmbedCheck.Checked = discord.UseEmbed;
        _discordThumbnailCheck.Checked = discord.UseThumbnail;
        _discordColorBox.Text = discord.EmbedColor;
        _discordFooterBox.Text = discord.FooterText;
        _discordRoleBox.Text = discord.MentionRoleId ?? "";
        _discordMessageTemplateBox.Text = discord.MessageTemplate;
        _discordChannelsGrid.Rows.Clear();
        foreach (var channel in discord.Channels) AddDiscordChannelRow(channel);
        LoadClipMessageRows(clip.ChatMessages);
        UpdateClipChannelCount();
    }

    private void ReadClipDiscordSettings(AppConfig config)
    {
        var clip = config.ClipCommand;
        clip.Enabled = _clipCommandEnabledCheck.Checked;
        clip.Command = _clipCommandBox.Text;
        clip.Aliases = SplitValues(_clipAliasesBox.Text);
        clip.DefaultTitle = _clipDefaultTitleBox.Text;
        clip.DurationSeconds = decimal.ToInt32(_clipDurationControl.Value);
        clip.MaximumTitleLength = decimal.ToInt32(_clipTitleLengthControl.Value);
        clip.ChatResponsesEnabled = _clipChatResponsesCheck.Checked;
        clip.AllowedRoles.Broadcaster = _clipBroadcasterCheck.Checked;
        clip.AllowedRoles.Moderators = _clipModeratorsCheck.Checked;
        clip.AllowedRoles.Vips = _clipVipsCheck.Checked;
        clip.AllowedRoles.Subscribers = _clipSubscribersCheck.Checked;
        clip.AllowedRoles.Followers = _clipFollowersCheck.Checked;
        clip.AllowedRoles.Everyone = _clipEveryoneCheck.Checked;
        clip.AllowedUsers = SplitValues(_clipAllowedUsersBox.Text);
        clip.BlockedUsers = SplitValues(_clipBlockedUsersBox.Text);
        clip.GlobalCooldownSeconds = decimal.ToInt32(_clipGlobalCooldownControl.Value);
        clip.UserCooldownSeconds = decimal.ToInt32(_clipUserCooldownControl.Value);
        clip.MaximumClipsPerStream = decimal.ToInt32(_clipMaximumStreamControl.Value);
        clip.MaximumClipsPerUserPerStream = decimal.ToInt32(_clipMaximumUserControl.Value);
        clip.QueueEnabled = _clipQueueCheck.Checked;
        clip.MaximumQueueSize = decimal.ToInt32(_clipQueueSizeControl.Value);
        ReadClipMessageRows(clip.ChatMessages);

        var discord = config.DiscordClips;
        discord.InviteCommandEnabled = _discordInviteEnabledCheck.Checked;
        discord.InviteCommand = _discordInviteCommandBox.Text.Trim();
        discord.InviteUrl = _discordInviteUrlBox.Text.Trim();
        discord.InviteMessage = _discordInviteMessageBox.Text.Trim();
        discord.InviteCooldownSeconds =
            decimal.ToInt32(_discordInviteCooldownControl.Value);
        discord.Enabled = _discordClipsEnabledCheck.Checked;
        discord.GuildId = _discordGuildIdBox.Text;
        discord.UseEmbed = _discordEmbedCheck.Checked;
        discord.UseThumbnail = _discordThumbnailCheck.Checked;
        discord.EmbedColor = _discordColorBox.Text;
        discord.FooterText = _discordFooterBox.Text;
        discord.MentionRoleId = _discordRoleBox.Text;
        discord.MessageTemplate = _discordMessageTemplateBox.Text;
        discord.Channels = _discordChannelsGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow && Cell(row, "ChannelId").Length > 0)
            .Select(row => new DiscordClipChannelConfig
            {
                Enabled = CellBool(row, "Enabled"),
                ChannelId = Cell(row, "ChannelId"),
                Name = Cell(row, "Name"),
                UseWebhook = CellBool(row, "Webhook"),
                MessageTemplate = Cell(row, "Template")
            }).ToList();
        _discordCredentials.BotToken = _discordBotTokenBox.Text.Trim();
        _discordCredentials.WebhookUrls = _discordChannelsGrid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow && Cell(row, "ChannelId").Length > 0 &&
                          Cell(row, "WebhookUrl").Length > 0)
            .GroupBy(row => Cell(row, "ChannelId"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => Cell(group.Last(), "WebhookUrl"), StringComparer.Ordinal);
    }

    private async Task SaveClipDiscordSettingsAsync()
    {
        try
        {
            var config = ReadSettingsFromControls();
            ReadClipDiscordSettings(config);
            ConfigurationService.ValidateClipSettings(
                config.ClipCommand, config.DiscordClips);
            if (config.DiscordClips.Enabled)
            {
                using var validationService = new DiscordClipService(
                    config.DiscordClips,
                    _discordCredentials,
                    new DiscordRestClient(_discordCredentials.BotToken),
                    new ClipTemplateService());
                var validations = await validationService
                    .ValidateConfiguredChannelsAsync(CancellationToken.None);
                var invalid = validations.FirstOrDefault(item => !item.IsValid);
                if (invalid is not null)
                    throw new InvalidOperationException(
                        $"Discord-Channel {invalid.ChannelId}: {invalid.Error}");
            }
            _configurationService.SaveGuiSettings(config);
            await _discordCredentialStore.SaveAsync(_discordCredentials);
            ApplyRuntimeSettings(config);
            AppendLog("Clips-&-Discord-Einstellungen wurden gespeichert.");
            SetClipDiscordStatus(config, _twitchSession);
        }
        catch (Exception exception)
        {
            AppendLog("Clips & Discord konnten nicht gespeichert werden: " + exception.Message);
            _clipLastErrorStatus.Text = "Letzter Fehler: " + exception.Message;
            ShowSection("clip-discord");
        }
    }

    private async Task ValidateDiscordAsync()
    {
        try
        {
            var config = ReadSettingsFromControls();
            ReadClipDiscordSettings(config);
            ConfigurationService.ValidateClipSettings(
                config.ClipCommand, config.DiscordClips);
            using var client = new DiscordRestClient(_discordCredentials.BotToken);
            using var service = new DiscordClipService(
                config.DiscordClips, _discordCredentials, client,
                new ClipTemplateService());
            var results = await service.ValidateConfiguredChannelsAsync(CancellationToken.None);
            foreach (var result in results)
            {
                var row = _discordChannelsGrid.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(item => Cell(item, "ChannelId") == result.ChannelId);
                if (row is not null)
                {
                    row.Cells["Name"].Value = result.Name;
                    row.DefaultCellStyle.BackColor = result.IsValid
                        ? Color.FromArgb(28, 70, 45) : Color.FromArgb(75, 28, 30);
                }
                AppendLog(result.IsValid
                    ? $"Discord-Channel {result.ChannelId} ist bereit."
                    : $"Discord-Channel {result.ChannelId}: {result.Error}");
            }
            _clipDiscordStatus.Text = results.All(item => item.IsValid)
                ? "Discord: Verbunden" : "Discord: Prüfung fehlgeschlagen";
            _clipDiscordStatus.ForeColor = results.All(item => item.IsValid)
                ? ActiveColor : ErrorColor;
        }
        catch (Exception exception)
        {
            _clipDiscordStatus.Text = "Discord: Fehler";
            _clipDiscordStatus.ForeColor = ErrorColor;
            AppendLog("Discord-Prüfung fehlgeschlagen: " + exception.Message);
        }
    }

    private async Task TestDiscordAsync()
    {
        if (_discordChannelsGrid.SelectedRows.Count != 1)
        {
            AppendLog("Bitte genau einen Discord-Channel auswählen.");
            return;
        }
        try
        {
            var config = ReadSettingsFromControls();
            ReadClipDiscordSettings(config);
            using var client = new DiscordRestClient(_discordCredentials.BotToken);
            using var service = new DiscordClipService(
                config.DiscordClips, _discordCredentials, client,
                new ClipTemplateService());
            var channelId = Cell(_discordChannelsGrid.SelectedRows[0], "ChannelId");
            await service.SendTestMessageAsync(channelId, CancellationToken.None);
            _clipLastDiscordStatus.Text = "Letzter Discord-Versand: " + DateTime.Now.ToString("HH:mm:ss");
            AppendLog("Discord-Testnachricht wurde gesendet.");
        }
        catch (Exception exception)
        {
            AppendLog("Discord-Testnachricht fehlgeschlagen: " + exception.Message);
            _clipLastErrorStatus.Text = "Letzter Fehler: " + exception.Message;
        }
    }

    private void ShowDiscordPreview()
    {
        var config = ReadSettingsFromControls();
        ReadClipDiscordSettings(config);
        var context = new ClipDiscordContext(
            new PublishedClip("Beispiel", "https://clips.twitch.tv/Beispiel", "", "", 30),
            "Beispielclip ohne @everyone-Erwähnung", "Zuschauer", config.Twitch.BroadcasterLogin,
            "Just Chatting", DateTimeOffset.Now);
        var payload = DiscordClipService.BuildPayload(
            config.DiscordClips, config.DiscordClips.MessageTemplate,
            context, new ClipTemplateService());
        MessageBox.Show(this,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            "Discord-Embed-Vorschau", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LoadClipMessageRows(ClipChatMessages messages)
    {
        _clipMessagesGrid.Rows.Clear();
        AddMessageRow("Beginn", messages.Starting);
        AddMessageRow("Erfolg", messages.Success);
        AddMessageRow("Erfolg Discord", messages.SuccessDiscord);
        AddMessageRow("Cooldown", messages.Cooldown);
        AddMessageRow("Offline", messages.Offline);
        AddMessageRow("Keine Berechtigung", messages.Forbidden);
        AddMessageRow("Twitch-Fehler", messages.TwitchError);
        AddMessageRow("Discord teilweise", messages.PartialDiscord);
        AddMessageRow("Warteschlange voll", messages.QueueFull);
        AddMessageRow("Beschäftigt", messages.Busy);
        AddMessageRow("Limit", messages.LimitReached);
        AddMessageRow("Scope fehlt", messages.MissingScope);
    }

    private void ReadClipMessageRows(ClipChatMessages messages)
    {
        var map = _clipMessagesGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .ToDictionary(row => Cell(row, "Key"), row => new ClipChatMessage(
                CellBool(row, "Enabled"), Cell(row, "Text")), StringComparer.Ordinal);
        messages.Starting = map.GetValueOrDefault("Beginn", messages.Starting);
        messages.Success = map.GetValueOrDefault("Erfolg", messages.Success);
        messages.SuccessDiscord = map.GetValueOrDefault("Erfolg Discord", messages.SuccessDiscord);
        messages.Cooldown = map.GetValueOrDefault("Cooldown", messages.Cooldown);
        messages.Offline = map.GetValueOrDefault("Offline", messages.Offline);
        messages.Forbidden = map.GetValueOrDefault("Keine Berechtigung", messages.Forbidden);
        messages.TwitchError = map.GetValueOrDefault("Twitch-Fehler", messages.TwitchError);
        messages.PartialDiscord = map.GetValueOrDefault("Discord teilweise", messages.PartialDiscord);
        messages.QueueFull = map.GetValueOrDefault("Warteschlange voll", messages.QueueFull);
        messages.Busy = map.GetValueOrDefault("Beschäftigt", messages.Busy);
        messages.LimitReached = map.GetValueOrDefault("Limit", messages.LimitReached);
        messages.MissingScope = map.GetValueOrDefault("Scope fehlt", messages.MissingScope);
    }

    private void AddMessageRow(string key, ClipChatMessage message) =>
        _clipMessagesGrid.Rows.Add(key, message.Enabled, message.Text);

    private static List<string> SplitValues(string text) =>
        (text ?? "").Split(new[] { ',', ';', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value.Trim()).Where(value => value.Length > 0).ToList();

    private static string Cell(DataGridViewRow row, string name) =>
        row.Cells[name].Value?.ToString()?.Trim() ?? "";

    private static bool CellBool(DataGridViewRow row, string name) =>
        row.Cells[name].Value is bool value && value;

    private void UpdateClipChannelCount() =>
        _clipChannelCountStatus.Text = "Channels: " +
            _discordChannelsGrid.Rows.Cast<DataGridViewRow>()
                .Count(row => !row.IsNewRow && CellBool(row, "Enabled"));

    private void SetClipDiscordStatus(AppConfig config, TwitchSession? session)
    {
        _clipTwitchStatus.Text = session is null
            ? "Twitch: Nicht verbunden" : "Twitch: Verbunden";
        _clipTwitchStatus.ForeColor = session is null ? InactiveColor : ActiveColor;
        var hasScope = session?.HasScope("clips:edit") == true;
        _clipScopeStatus.Text = hasScope ? "clips:edit: Vorhanden" : "clips:edit: Twitch neu verbinden";
        _clipScopeStatus.ForeColor = hasScope ? ActiveColor : WaitingColor;
        _clipDiscordStatus.Text = config.DiscordClips.Enabled
            ? "Discord: Konfiguriert" : "Discord: Deaktiviert";
        _clipDiscordStatus.ForeColor = config.DiscordClips.Enabled
            ? WaitingColor : InactiveColor;
        UpdateClipChannelCount();
    }

    private void HandleClipCommandStatus(ClipCommandStatus status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => HandleClipCommandStatus(status)));
            return;
        }
        if (!string.IsNullOrWhiteSpace(status.ClipUrl))
        {
            _clipLastSuccessStatus.Text = "Letzter Clip: " + status.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            _clipLastSuccessStatus.ForeColor = ActiveColor;
        }
        if (status.DiscordSuccessCount > 0)
        {
            _clipLastDiscordStatus.Text =
                $"Letzter Discord-Versand: {status.Timestamp.ToLocalTime():HH:mm:ss} ({status.DiscordSuccessCount})";
            _clipLastDiscordStatus.ForeColor = ActiveColor;
        }
        if (!string.IsNullOrWhiteSpace(status.Error))
        {
            _clipLastErrorStatus.Text = "Letzter Fehler: " + status.Error;
            _clipLastErrorStatus.ForeColor = ErrorColor;
        }
    }
    private async Task StartClipCommandAsync(
        AppConfig config,
        TwitchSession session,
        TwitchService twitch,
        TwitchUser broadcaster,
        CancellationToken cancellationToken)
    {
        SetClipDiscordStatus(config, session);
        _discordInviteService = config.DiscordClips.InviteCommandEnabled
            ? new DiscordInviteCommandService(
                broadcaster.Id, session.UserId,
                config.DiscordClips, twitch)
            : null;
        if (_discordInviteService is not null)
            AppendLog(
                "Discord-Einladungsbefehl ist aktiv: " +
                config.DiscordClips.InviteCommand);
        if (!config.ClipCommand.Enabled)
            return;

        _discordCredentials = _discordCredentialStore.Load();
        _discordClipService = null;
        if (config.DiscordClips.Enabled)
        {
            var discordClient = new DiscordRestClient(
                _discordCredentials.BotToken);
            _discordClipService = new DiscordClipService(
                config.DiscordClips,
                _discordCredentials,
                discordClient,
                new ClipTemplateService());
        }

        var templates = new ClipTemplateService();
        var cooldowns = new ClipCooldownService();
        _clipCommandService = new ClipCommandService(
            broadcaster.Id,
            session.UserId,
            broadcaster.DisplayName,
            broadcaster.ProfileImageUrl,
            session.HasScope("clips:edit"),
            config.ClipCommand,
            twitch,
            twitch,
            new TwitchClipService(twitch),
            new ClipPermissionService(twitch),
            cooldowns,
            templates,
            _discordClipService);
        _clipCommandService.StatusChanged += HandleClipCommandStatus;
        _clipCommandTask = _clipCommandService.RunAsync(cancellationToken);
        AppendLog(
            "Clip-Command ist aktiv. Command: " + config.ClipCommand.Command);

        if (_discordClipService is not null)
        {
            try
            {
                var validation = await _discordClipService
                    .ValidateConfiguredChannelsAsync(cancellationToken);
                var ready = validation.Count > 0 && validation.All(item => item.IsValid);
                _clipDiscordStatus.Text = ready
                    ? "Discord: Verbunden" : "Discord: Nicht vollständig bereit";
                _clipDiscordStatus.ForeColor = ready ? ActiveColor : WaitingColor;
                foreach (var failed in validation.Where(item => !item.IsValid))
                    AppendLog(
                        $"Discord-Channel {failed.ChannelId}: {failed.Error}");
            }
            catch (Exception exception)
            {
                _clipDiscordStatus.Text = "Discord: Fehler";
                _clipDiscordStatus.ForeColor = ErrorColor;
                AppendLog("Discord-Startprüfung fehlgeschlagen: " + exception.Message);
            }
        }
    }

    private void StopClipCommand()
    {
        _clipCommandService?.Dispose();
        _clipCommandService = null;
        _discordClipService = null;
        _discordInviteService = null;
        _clipCommandTask = null;
        _clipTwitchStatus.Text = "Twitch: Nicht verbunden";
        _clipTwitchStatus.ForeColor = InactiveColor;
        _clipScopeStatus.Text = "clips:edit: Unbekannt";
        _clipScopeStatus.ForeColor = InactiveColor;
    }

}
