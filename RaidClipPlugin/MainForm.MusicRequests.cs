using System.Diagnostics;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;


namespace RaidClipPlugin;


public sealed partial class MainForm
{
    private readonly Button _musicNavButton = CreateNavigationTile(
        "🎵  Musikwünsche", "Twitch-Kanalpunkte und Spotify");
    private readonly Panel _musicPage = new()
        { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _spotifyStatusLabel = new()
    {
        Text = "● Spotify: Nicht verbunden", AutoSize = true,
        ForeColor = InactiveColor,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Padding = new Padding(4)
    };
    private readonly CheckBox _musicEnabledCheck =
        NewCheck("Musikwünsche aktivieren", false);
    private readonly TextBox _spotifyClientIdBox = new()
        { Width = 300, MaxLength = 80 };
    private readonly Button _spotifyConnectButton =
        NewActionButton("Mit Spotify verbinden");
    private readonly Button _spotifyDisconnectButton =
        NewActionButton("Spotify trennen");
    private readonly Label _spotifyAccountLabel = new()
        { Text = "Konto: –", AutoSize = true, Margin = new Padding(8, 24, 8, 4) };
    private readonly Button _refreshDevicesButton =
        NewActionButton("Geräte aktualisieren");
    private readonly ComboBox _spotifyDeviceBox = new()
        { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _useActiveDeviceCheck =
        NewCheck("Aktives Spotify-Gerät automatisch verwenden", true);
    private readonly CheckBox _activateDeviceCheck =
        NewCheck("Ausgewähltes Gerät bei Bedarf aktivieren", true);
    private readonly ComboBox _playbackModeBox = new()
        { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _refreshRewardsButton =
        NewActionButton("Belohnungen aktualisieren");
    private readonly ComboBox _rewardBox = new()
        { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _rewardIdBox = new()
        { Width = 330, MaxLength = 80 };
    private readonly Label _rewardIdLabel = new()
        { Text = "Belohnungs-ID: –", AutoSize = true, Margin = new Padding(8, 24, 8, 4) };
    private readonly NumericUpDown _maxSongDurationControl =
        CreateIntegerControl(10, 1, 180);
    private readonly NumericUpDown _maxMusicQueueControl =
        CreateIntegerControl(25, 1, 500);
    private readonly NumericUpDown _musicCooldownControl =
        CreateIntegerControl(5, 0, 1440);
    private readonly NumericUpDown _maxRequestsPerUserControl =
        CreateIntegerControl(2, 1, 100);
    private readonly CheckBox _allowExplicitCheck =
        NewCheck("Explizite Songs erlauben", false);
    private readonly CheckBox _allowDuplicatesCheck =
        NewCheck("Doppelte Songs erlauben", false);
    private readonly CheckBox _allowSpotifyLinksCheck =
        NewCheck("Spotify-Track-Links erlauben", true);
    private readonly CheckBox _allowTextSearchCheck =
        NewCheck("Songname und Künstler als Suchtext erlauben", true);
    private readonly CheckBox _autoFulfillCheck =
        NewCheck("Erfolgreiche Einlösungen automatisch erfüllen", true);
    private readonly CheckBox _autoCancelCheck =
        NewCheck("Endgültig abgelehnte Einlösungen stornieren", true);
    private readonly TextBox _musicUserBlacklistBox = NewMusicListBox();
    private readonly TextBox _musicArtistBlacklistBox = NewMusicListBox();
    private readonly TextBox _musicTrackIdBlacklistBox = NewMusicListBox();
    private readonly TextBox _musicTitleBlacklistBox = NewMusicListBox();
    private readonly TextBox _musicBlockedTermsBox = NewMusicListBox();
    private readonly TextBox _musicQueuedMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicPlayingMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicNotFoundMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicNoDeviceMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicTooLongMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicExplicitMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicCooldownMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicQueueFullMessageBox = NewMusicMessageBox();
    private readonly TextBox _musicBlacklistMessageBox = NewMusicMessageBox();
    private readonly CheckBox _songCommandCheck = NewCheck("!song aktiv", true);
    private readonly CheckBox _skipCommandCheck = NewCheck("!skip aktiv", true);
    private readonly CheckBox _queueCommandCheck = NewCheck("!musicqueue aktiv", true);
    private readonly CheckBox _removeSongCommandCheck = NewCheck("!removesong aktiv", true);
    private readonly CheckBox _pauseCommandCheck = NewCheck("!musicpause aktiv", true);
    private readonly CheckBox _resumeCommandCheck = NewCheck("!musicresume aktiv", true);
    private readonly TextBox _songCommandBox = NewCommandBox("!song");
    private readonly TextBox _skipCommandBox = NewCommandBox("!skip");
    private readonly TextBox _queueCommandBox = NewCommandBox("!musicqueue");
    private readonly TextBox _removeSongCommandBox = NewCommandBox("!removesong");
    private readonly TextBox _pauseCommandBox = NewCommandBox("!musicpause");
    private readonly TextBox _resumeCommandBox = NewCommandBox("!musicresume");
    private readonly Button _saveMusicSettingsButton =
        NewActionButton("Einstellungen speichern");
    private readonly Button _playMusicEntryButton =
        NewActionButton("Song abspielen");
    private readonly Button _skipMusicButton =
        NewActionButton("Song überspringen");
    private readonly Button _retryMusicButton =
        NewActionButton("Erneut versuchen");
    private readonly Button _removeMusicEntryButton =
        NewActionButton("Eintrag entfernen");
    private readonly Button _openSpotifyLinkButton =
        NewActionButton("Spotify-Link öffnen");
    private readonly Button _blacklistMusicUserButton =
        NewActionButton("Nutzer sperren");
    private readonly Button _blacklistMusicTrackButton =
        NewActionButton("Song sperren");
    private readonly Button _blacklistMusicArtistButton =
        NewActionButton("Künstler sperren");
    private readonly Button _clearMusicQueueButton =
        NewActionButton("Warteschlange leeren");
    private readonly DataGridView _musicGrid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false
    };


    private SpotifyService? _spotify;
    private readonly MusicRequestStore _musicStore = new();
    private MusicRequestService? _musicRequests;
    private MusicRequestEventSubService? _musicEventSub;
    private Task? _musicRequestTask;
    private Task? _musicEventSubTask;


    private static TextBox NewMusicMessageBox() => new()
    {
        Width = 430, Height = 50, Multiline = true,
        ScrollBars = ScrollBars.Vertical, MaxLength = 500
    };


    private static TextBox NewCommandBox(string text) => new()
        { Width = 150, Text = text, MaxLength = 30 };


    private static TextBox NewMusicListBox() => new()
    {
        Width = 280, Height = 72, Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        PlaceholderText = "Ein Eintrag pro Zeile oder mit Komma getrennt"
    };


    private void InitializeMusicRequestEvents()
    {
        _musicNavButton.Click += (_, _) => ShowSection("music");
        _saveMusicSettingsButton.Click += (_, _) => SaveMusicRequestSettingsFromControls();
        _spotifyConnectButton.Click += async (_, _) =>
            await ConnectSpotifyAsync();
        _spotifyDisconnectButton.Click += (_, _) => DisconnectSpotify();
        _refreshDevicesButton.Click += async (_, _) =>
            await RefreshSpotifyDevicesAsync();
        _refreshRewardsButton.Click += async (_, _) =>
            await RefreshMusicRewardsAsync();
        _rewardBox.SelectedIndexChanged += (_, _) =>
        {
            if (_rewardBox.SelectedItem is TwitchCustomReward reward)
            {
                _rewardIdBox.Text = reward.Id;
                _rewardIdLabel.Text = "Belohnungs-ID: " + reward.Id;
            }
        };
        _playMusicEntryButton.Click += async (_, _) =>
            await PlaySelectedMusicEntryAsync();
        _skipMusicButton.Click += async (_, _) => await SkipSpotifyAsync();
        _retryMusicButton.Click += async (_, _) =>
            await RetrySelectedMusicEntryAsync();
        _removeMusicEntryButton.Click += async (_, _) =>
            await RemoveSelectedMusicEntryAsync();
        _openSpotifyLinkButton.Click += (_, _) => OpenSelectedSpotifyLink();
        _blacklistMusicUserButton.Click += (_, _) => BlacklistSelectedMusicUser();
        _blacklistMusicTrackButton.Click += (_, _) => BlacklistSelectedMusicTrack();
        _blacklistMusicArtistButton.Click += (_, _) => BlacklistSelectedMusicArtist();
        _clearMusicQueueButton.Click += async (_, _) =>
            await ClearMusicQueueAsync();
    }


    private void BuildMusicRequestPage()
    {
        _playbackModeBox.Items.AddRange(new object[]
        {
            "Zur Warteschlange hinzufügen", "Sofort abspielen"
        });
        _playbackModeBox.SelectedIndex = 0;


        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titleFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill,
            WrapContents = false
        };
        titleFlow.Controls.Add(new Label
        {
            Text = "Musikwünsche", AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.White
        });
        titleFlow.Controls.Add(new Label
        {
            Text = "Twitch-Kanalpunkte mit Spotify Connect verbinden",
            AutoSize = true, ForeColor = Color.DimGray
        });
        header.Controls.Add(titleFlow, 0, 0);
        header.Controls.Add(_spotifyStatusLabel, 1, 0);


        var connection = CreateMinigameFlow();
        connection.Controls.Add(_musicEnabledCheck);
        connection.Controls.Add(CreateSettingEditor(
            "Spotify Client-ID", _spotifyClientIdBox));
        connection.Controls.Add(_spotifyConnectButton);
        connection.Controls.Add(_spotifyDisconnectButton);
        connection.Controls.Add(_spotifyAccountLabel);
        connection.Controls.Add(_refreshDevicesButton);
        connection.Controls.Add(CreateSettingEditor(
            "Spotify-Gerät", _spotifyDeviceBox));
        connection.Controls.Add(_useActiveDeviceCheck);
        connection.Controls.Add(_activateDeviceCheck);
        connection.Controls.Add(CreateSettingEditor(
            "Wiedergabemodus", _playbackModeBox));
        connection.Controls.Add(_refreshRewardsButton);
        connection.Controls.Add(CreateSettingEditor(
            "Twitch-Belohnung", _rewardBox));
        connection.Controls.Add(CreateSettingEditor(
            "Belohnungs-ID (manuell)", _rewardIdBox));
        connection.Controls.Add(_rewardIdLabel);
        connection.Controls.Add(_saveMusicSettingsButton);


        var filters = CreateMinigameFlow();
        filters.Controls.Add(CreateSettingEditor(
            "Maximale Songdauer (Min.)", _maxSongDurationControl));
        filters.Controls.Add(CreateSettingEditor(
            "Maximale offene Wünsche", _maxMusicQueueControl));
        filters.Controls.Add(CreateSettingEditor(
            "Nutzer-Cooldown (Min.)", _musicCooldownControl));
        filters.Controls.Add(CreateSettingEditor(
            "Offene Wünsche pro Nutzer", _maxRequestsPerUserControl));
        filters.Controls.Add(_allowExplicitCheck);
        filters.Controls.Add(_allowDuplicatesCheck);
        filters.Controls.Add(_allowSpotifyLinksCheck);
        filters.Controls.Add(_allowTextSearchCheck);
        filters.Controls.Add(_autoFulfillCheck);
        filters.Controls.Add(_autoCancelCheck);
        var filterSave = NewActionButton("Einstellungen speichern");
        filterSave.Click += (_, _) => SaveMusicRequestSettingsFromControls();
        filters.Controls.Add(filterSave);


        var blacklists = CreateMinigameFlow();
        blacklists.Controls.Add(CreateSettingEditor(
            "Twitch-Nutzer", _musicUserBlacklistBox));
        blacklists.Controls.Add(CreateSettingEditor(
            "Spotify-Künstler", _musicArtistBlacklistBox));
        blacklists.Controls.Add(CreateSettingEditor(
            "Spotify-Track-IDs", _musicTrackIdBlacklistBox));
        blacklists.Controls.Add(CreateSettingEditor(
            "Songtitel", _musicTitleBlacklistBox));
        blacklists.Controls.Add(CreateSettingEditor(
            "Begriffe im Songtitel", _musicBlockedTermsBox));
        var blacklistSave = NewActionButton("Einstellungen speichern");
        blacklistSave.Click += (_, _) => SaveMusicRequestSettingsFromControls();
        blacklists.Controls.Add(blacklistSave);


        ConfigureMusicGrid();
        var queueLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1
        };
        queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        queueLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var queueActions = CreateMinigameFlow();
        queueActions.AutoScroll = true;
        queueActions.WrapContents = true;
        queueActions.Controls.Add(_playMusicEntryButton);
        queueActions.Controls.Add(_skipMusicButton);
        queueActions.Controls.Add(_retryMusicButton);
        queueActions.Controls.Add(_removeMusicEntryButton);
        queueActions.Controls.Add(_openSpotifyLinkButton);
        queueActions.Controls.Add(_blacklistMusicUserButton);
        queueActions.Controls.Add(_blacklistMusicTrackButton);
        queueActions.Controls.Add(_blacklistMusicArtistButton);
        queueActions.Controls.Add(_clearMusicQueueButton);
        queueLayout.Controls.Add(queueActions, 0, 0);
        queueLayout.Controls.Add(_musicGrid, 0, 1);


        var messages = CreateMinigameFlow();
        messages.Controls.Add(CreateSettingEditor("Erfolg · Warteschlange", _musicQueuedMessageBox));
        messages.Controls.Add(CreateSettingEditor("Erfolg · Sofort", _musicPlayingMessageBox));
        messages.Controls.Add(CreateSettingEditor("Nicht gefunden", _musicNotFoundMessageBox));
        messages.Controls.Add(CreateSettingEditor("Kein Gerät", _musicNoDeviceMessageBox));
        messages.Controls.Add(CreateSettingEditor("Song zu lang", _musicTooLongMessageBox));
        messages.Controls.Add(CreateSettingEditor("Explicit gesperrt", _musicExplicitMessageBox));
        messages.Controls.Add(CreateSettingEditor("Cooldown", _musicCooldownMessageBox));
        messages.Controls.Add(CreateSettingEditor("Warteschlange voll", _musicQueueFullMessageBox));
        messages.Controls.Add(CreateSettingEditor("Blacklist", _musicBlacklistMessageBox));
        var messageSave = NewActionButton("Einstellungen speichern");
        messageSave.Click += (_, _) => SaveMusicRequestSettingsFromControls();
        messages.Controls.Add(messageSave);


        var commands = CreateMinigameFlow();
        foreach (var pair in new (CheckBox Enabled, TextBox Command)[]
        {
            (_songCommandCheck, _songCommandBox),
            (_skipCommandCheck, _skipCommandBox),
            (_queueCommandCheck, _queueCommandBox),
            (_removeSongCommandCheck, _removeSongCommandBox),
            (_pauseCommandCheck, _pauseCommandBox),
            (_resumeCommandCheck, _resumeCommandBox)
        })
        {
            commands.Controls.Add(pair.Enabled);
            commands.Controls.Add(CreateSettingEditor("Command", pair.Command));
        }
        var commandSave = NewActionButton("Einstellungen speichern");
        commandSave.Click += (_, _) => SaveMusicRequestSettingsFromControls();
        commands.Controls.Add(commandSave);


        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddMinigameTab(tabs, "Verbindung", connection);
        AddMinigameTab(tabs, "Filter & Limits", filters);
        AddMinigameTab(tabs, "Blacklists", blacklists);
        AddMinigameTab(tabs, "Chattexte", messages);
        AddMinigameTab(tabs, "Mod-Commands", commands);
        AddMinigameTab(tabs, "Warteschlange", queueLayout);


        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            Padding = new Padding(20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(tabs, 0, 1);
        _musicPage.Controls.Add(layout);
    }


    private void ConfigureMusicGrid()
    {
        foreach (var (name, header, width) in new[]
        {
            ("Status", "Status", 110), ("Track", "Song", 220),
            ("Artist", "Künstler", 170), ("Duration", "Dauer", 75),
            ("User", "Twitch-Nutzer", 130), ("Time", "Zeit", 80),
            ("Mode", "Modus", 110), ("Link", "Spotify-Link", 210),
            ("Error", "Fehlergrund", 240)
        })
            _musicGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = width
            });
    }


    private void LoadMusicRequestSettings(MusicRequestConfig config)
    {
        ApplyMusicMessageDefaults(config.ChatMessages);
        _musicEnabledCheck.Checked = config.Enabled;
        _spotifyClientIdBox.Text = config.SpotifyClientId;
        _rewardIdBox.Text = config.SelectedRewardId;
        _rewardIdLabel.Text = "Belohnungs-ID: " +
            (config.SelectedRewardId.Length > 0 ? config.SelectedRewardId : "–");
        _playbackModeBox.SelectedIndex =
            config.PlaybackMode == MusicPlaybackMode.PlayImmediately ? 1 : 0;
        _useActiveDeviceCheck.Checked = config.UseActiveDevice;
        _activateDeviceCheck.Checked = config.ActivateSelectedDevice;
        SetNumericValue(_maxSongDurationControl,
            config.MaximumTrackDurationMinutes);
        SetNumericValue(_maxMusicQueueControl, config.MaximumQueueLength);
        SetNumericValue(_musicCooldownControl, config.UserCooldownMinutes);
        SetNumericValue(_maxRequestsPerUserControl,
            config.MaximumRequestsPerUser);
        _allowExplicitCheck.Checked = config.AllowExplicitTracks;
        _allowDuplicatesCheck.Checked = config.AllowDuplicateTracks;
        _allowSpotifyLinksCheck.Checked = config.AllowSpotifyLinks;
        _allowTextSearchCheck.Checked = config.AllowTextSearch;
        _autoFulfillCheck.Checked = config.AutoFulfillRedemptions;
        _autoCancelCheck.Checked = config.AutoCancelRejectedRedemptions;
        SetMusicList(_musicUserBlacklistBox, config.UserBlacklist);
        SetMusicList(_musicArtistBlacklistBox, config.ArtistBlacklist);
        SetMusicList(_musicTrackIdBlacklistBox, config.TrackBlacklist);
        SetMusicList(_musicTitleBlacklistBox, config.SongTitleBlacklist);
        SetMusicList(_musicBlockedTermsBox, config.BlockedTitleTerms);
        _musicQueuedMessageBox.Text = config.ChatMessages.Queued;
        _musicPlayingMessageBox.Text = config.ChatMessages.Playing;
        _musicNotFoundMessageBox.Text = config.ChatMessages.NotFound;
        _musicNoDeviceMessageBox.Text = config.ChatMessages.NoDevice;
        _musicTooLongMessageBox.Text = config.ChatMessages.TooLong;
        _musicExplicitMessageBox.Text = config.ChatMessages.ExplicitBlocked;
        _musicCooldownMessageBox.Text = config.ChatMessages.Cooldown;
        _musicQueueFullMessageBox.Text = config.ChatMessages.QueueFull;
        _musicBlacklistMessageBox.Text = config.ChatMessages.Blacklisted;
        var commands = config.ModeratorCommands;
        _songCommandCheck.Checked = commands.SongEnabled;
        _skipCommandCheck.Checked = commands.SkipEnabled;
        _queueCommandCheck.Checked = commands.QueueEnabled;
        _removeSongCommandCheck.Checked = commands.RemoveEnabled;
        _pauseCommandCheck.Checked = commands.PauseEnabled;
        _resumeCommandCheck.Checked = commands.ResumeEnabled;
        _songCommandBox.Text = commands.Song;
        _skipCommandBox.Text = commands.Skip;
        _queueCommandBox.Text = commands.Queue;
        _removeSongCommandBox.Text = commands.Remove;
        _pauseCommandBox.Text = commands.Pause;
        _resumeCommandBox.Text = commands.Resume;
        EnsureSpotify(config);
        UpdateSpotifyStatus();
        _ = RefreshMusicGridAsync();
    }


    private void ReadMusicRequestSettings(AppConfig config)
    {
        var music = config.MusicRequests;
        ApplyMusicMessageDefaults(music.ChatMessages);
        music.Enabled = _musicEnabledCheck.Checked;
        music.SpotifyClientId = _spotifyClientIdBox.Text.Trim();
        music.SelectedRewardId = _rewardIdBox.Text.Trim();
        music.SelectedRewardName =
            (_rewardBox.SelectedItem as TwitchCustomReward)?.Title ??
            music.SelectedRewardName;
        music.PlaybackMode = _playbackModeBox.SelectedIndex == 1
            ? MusicPlaybackMode.PlayImmediately
            : MusicPlaybackMode.AddToQueue;
        music.SelectedDeviceId =
            (_spotifyDeviceBox.SelectedItem as SpotifyDevice)?.Id ??
            music.SelectedDeviceId;
        music.UseActiveDevice = _useActiveDeviceCheck.Checked;
        music.ActivateSelectedDevice = _activateDeviceCheck.Checked;
        music.MaximumTrackDurationMinutes =
            decimal.ToInt32(_maxSongDurationControl.Value);
        music.MaximumQueueLength =
            decimal.ToInt32(_maxMusicQueueControl.Value);
        music.UserCooldownMinutes =
            decimal.ToInt32(_musicCooldownControl.Value);
        music.MaximumRequestsPerUser =
            decimal.ToInt32(_maxRequestsPerUserControl.Value);
        music.AllowExplicitTracks = _allowExplicitCheck.Checked;
        music.AllowDuplicateTracks = _allowDuplicatesCheck.Checked;
        music.AllowSpotifyLinks = _allowSpotifyLinksCheck.Checked;
        music.AllowTextSearch = _allowTextSearchCheck.Checked;
        music.AutoFulfillRedemptions = _autoFulfillCheck.Checked;
        music.AutoCancelRejectedRedemptions = _autoCancelCheck.Checked;
        music.UserBlacklist = ReadMusicList(_musicUserBlacklistBox, true);
        music.ArtistBlacklist = ReadMusicList(_musicArtistBlacklistBox);
        music.TrackBlacklist = ReadMusicList(_musicTrackIdBlacklistBox);
        music.SongTitleBlacklist = ReadMusicList(_musicTitleBlacklistBox);
        music.BlockedTitleTerms = ReadMusicList(_musicBlockedTermsBox);
        music.ChatMessages.Queued = ReadMusicMessage(_musicQueuedMessageBox, music.ChatMessages.Queued);
        music.ChatMessages.Playing = ReadMusicMessage(_musicPlayingMessageBox, music.ChatMessages.Playing);
        music.ChatMessages.NotFound = ReadMusicMessage(_musicNotFoundMessageBox, music.ChatMessages.NotFound);
        music.ChatMessages.NoDevice = ReadMusicMessage(_musicNoDeviceMessageBox, music.ChatMessages.NoDevice);
        music.ChatMessages.TooLong = ReadMusicMessage(_musicTooLongMessageBox, music.ChatMessages.TooLong);
        music.ChatMessages.ExplicitBlocked = ReadMusicMessage(_musicExplicitMessageBox, music.ChatMessages.ExplicitBlocked);
        music.ChatMessages.Cooldown = ReadMusicMessage(_musicCooldownMessageBox, music.ChatMessages.Cooldown);
        music.ChatMessages.QueueFull = ReadMusicMessage(_musicQueueFullMessageBox, music.ChatMessages.QueueFull);
        music.ChatMessages.Blacklisted = ReadMusicMessage(_musicBlacklistMessageBox, music.ChatMessages.Blacklisted);
        var commands = music.ModeratorCommands;
        commands.SongEnabled = _songCommandCheck.Checked;
        commands.SkipEnabled = _skipCommandCheck.Checked;
        commands.QueueEnabled = _queueCommandCheck.Checked;
        commands.RemoveEnabled = _removeSongCommandCheck.Checked;
        commands.PauseEnabled = _pauseCommandCheck.Checked;
        commands.ResumeEnabled = _resumeCommandCheck.Checked;
        commands.Song = _songCommandBox.Text.Trim();
        commands.Skip = _skipCommandBox.Text.Trim();
        commands.Queue = _queueCommandBox.Text.Trim();
        commands.Remove = _removeSongCommandBox.Text.Trim();
        commands.Pause = _pauseCommandBox.Text.Trim();
        commands.Resume = _resumeCommandBox.Text.Trim();
    }


    private void SaveMusicRequestSettingsFromControls()
    {
        try
        {
            var config = _configurationService.LoadForEditing();
            ReadMusicRequestSettings(config);
            _configurationService.SaveMusicRequestSettings(config.MusicRequests);


            var restartRequired = _activeConfig is not null &&
                _activeConfig.MusicRequests.Enabled != config.MusicRequests.Enabled;


            if (_activeConfig is not null)
            {
                _activeConfig.MusicRequests = config.MusicRequests;
            }


            _musicRequests?.UpdateConfig(config.MusicRequests);
            _spotify?.UpdateConfig(config.MusicRequests);
            EnsureSpotify(config.MusicRequests);
            SetSpotifyStatus("Einstellungen gespeichert", ActiveColor);
            SetOverallStatus("Musikwunsch-Einstellungen gespeichert", ActiveColor);
            AppendLog("Musikwunsch-Einstellungen wurden gespeichert.");


            if (restartRequired)
            {
                AppendLog(
                    "Das Ein- oder Ausschalten der Musikwünsche wird nach " +
                    "dem nächsten Neustart der Plugin-Verbindung wirksam.");
            }
        }
        catch (Exception exception)
        {
            AppendLog(
                "Musikwunsch-Einstellungen konnten nicht gespeichert werden: " +
                exception.Message);
            SetSpotifyStatus("Fehler: " + exception.Message, ErrorColor);
            SetOverallStatus("Musikwunsch-Einstellungsfehler", ErrorColor);
            ShowSection("music");
        }
    }


    private static void ApplyMusicMessageDefaults(MusicRequestChatMessages messages)
    {
        var defaults = new MusicRequestChatMessages();
        messages.Queued = string.IsNullOrWhiteSpace(messages.Queued) ? defaults.Queued : messages.Queued.Trim();
        messages.Playing = string.IsNullOrWhiteSpace(messages.Playing) ? defaults.Playing : messages.Playing.Trim();
        messages.NotFound = string.IsNullOrWhiteSpace(messages.NotFound) ? defaults.NotFound : messages.NotFound.Trim();
        messages.NoDevice = string.IsNullOrWhiteSpace(messages.NoDevice) ? defaults.NoDevice : messages.NoDevice.Trim();
        messages.TooLong = string.IsNullOrWhiteSpace(messages.TooLong) ? defaults.TooLong : messages.TooLong.Trim();
        messages.ExplicitBlocked = string.IsNullOrWhiteSpace(messages.ExplicitBlocked) ? defaults.ExplicitBlocked : messages.ExplicitBlocked.Trim();
        messages.Cooldown = string.IsNullOrWhiteSpace(messages.Cooldown) ? defaults.Cooldown : messages.Cooldown.Trim();
        messages.QueueFull = string.IsNullOrWhiteSpace(messages.QueueFull) ? defaults.QueueFull : messages.QueueFull.Trim();
        messages.Blacklisted = string.IsNullOrWhiteSpace(messages.Blacklisted) ? defaults.Blacklisted : messages.Blacklisted.Trim();
        messages.InvalidInput = string.IsNullOrWhiteSpace(messages.InvalidInput) ? defaults.InvalidInput : messages.InvalidInput.Trim();
    }


    private static string ReadMusicMessage(TextBox box, string fallback) =>
        string.IsNullOrWhiteSpace(box.Text) ? fallback : box.Text.Trim();


    private static void SetMusicList(TextBox box, IEnumerable<string> values) =>
        box.Text = string.Join(Environment.NewLine, values);


    private static List<string> ReadMusicList(TextBox box, bool twitch = false) =>
        box.Text.Split(new[] { ',', ';', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(value => twitch
                ? value.TrimStart('@').ToLowerInvariant() : value)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();


    private void EnsureSpotify(MusicRequestConfig config)
    {
        if (_spotify is null) _spotify = new SpotifyService(config);
        else _spotify.UpdateConfig(config);
    }


    private MusicRequestConfig ReadSpotifyConnectionSettings()
    {
        var config = _configurationService.LoadForEditing();
        ReadMusicRequestSettings(config);
        return config.MusicRequests;
    }


    private async Task ConnectSpotifyAsync()
    {
        _spotifyConnectButton.Enabled = false;
        try
        {
            var music = ReadSpotifyConnectionSettings();
            _configurationService.SaveSpotifyConnectionSettings(music);
            EnsureSpotify(music);
            SetSpotifyStatus("Anmeldung …", WaitingColor);
            await _spotify!.ConnectAsync(_shutdown?.Token ??
                CancellationToken.None);
            UpdateSpotifyStatus();
            await RefreshSpotifyDevicesAsync();
        }
        catch (Exception exception)
        {
            SetSpotifyFailureStatus(exception);
            AppendLog("Spotify-Anmeldung fehlgeschlagen: " + exception.Message);
        }
        finally
        {
            _spotifyConnectButton.Enabled = true;
        }
    }


    private void DisconnectSpotify()
    {
        _spotify?.Disconnect();
        UpdateSpotifyStatus();
    }


    private async Task RefreshSpotifyDevicesAsync()
    {
        try
        {
            var music = ReadSpotifyConnectionSettings();
            EnsureSpotify(music);
            var devices = await _spotify!.GetDevicesAsync(
                _shutdown?.Token ?? CancellationToken.None);
            _spotifyDeviceBox.DataSource = devices.ToList();
            var selectedIndex = devices.ToList().FindIndex(device =>
                device.Id.Equals(music.SelectedDeviceId,
                    StringComparison.Ordinal));
            if (selectedIndex >= 0) _spotifyDeviceBox.SelectedIndex = selectedIndex;
            if (devices.Count == 0)
                AppendLog("Kein Spotify-Gerät verfügbar. Spotify bitte auf einem Gerät starten.");
            else
                AppendLog($"{devices.Count} Spotify-Gerät(e) gefunden.");
            UpdateSpotifyStatus();
        }
        catch (Exception exception)
        {
            SetSpotifyFailureStatus(exception);
            AppendLog("Spotify-Geräte konnten nicht geladen werden: " +
                      exception.Message);
        }
    }


    private async Task RefreshMusicRewardsAsync()
    {
        try
        {
            var config = _configurationService.LoadForEditing();
            ReadMusicRequestSettings(config);
            if (!string.IsNullOrWhiteSpace(_twitchChannelBox.Text))
            {
                config.Twitch.BroadcasterLogin = _twitchChannelBox.Text.Trim();
            }
            var token = _shutdown?.Token ?? CancellationToken.None;
            var session = await new AuthenticationService(config)
                .GetSessionAsync(token);
            var twitch = new TwitchService(
                config.Twitch.ClientId, session.AccessToken);
            var broadcaster = await twitch.GetUserAsync(
                config.Twitch.BroadcasterLogin, token)
                ?? throw new InvalidOperationException(
                    "Twitch-Kanal wurde nicht gefunden.");
            var rewards = await twitch.GetCustomRewardsAsync(
                broadcaster.Id, token);
            var allRewards = rewards.ToList();
            _rewardBox.DataSource = allRewards;
            var selectedIndex = allRewards.FindIndex(reward =>
                reward.Id.Equals(config.MusicRequests.SelectedRewardId,
                    StringComparison.Ordinal));
            if (selectedIndex >= 0) _rewardBox.SelectedIndex = selectedIndex;


            var usableCount = allRewards.Count(reward =>
                reward.IsEnabled && reward.RequiresInput);
            if (allRewards.Count == 0)
            {
                AppendLog(
                    "Twitch meldet keine benutzerdefinierten Belohnungen. " +
                    "Der angemeldete Twitch-Nutzer muss der eingestellte " +
                    "Broadcaster sein und Affiliate oder Partner sein.");
            }
            else if (usableCount == 0)
            {
                AppendLog(
                    $"{allRewards.Count} Twitch-Belohnung(en) geladen, " +
                    "aber keine ist aktiviert und erlaubt Texteingaben. " +
                    "Alle Einträge werden zur Diagnose angezeigt.");
            }
            else
            {
                AppendLog(
                    $"{allRewards.Count} Twitch-Belohnung(en) geladen, " +
                    $"davon {usableCount} für Musikwünsche geeignet.");
            }
        }
        catch (Exception exception)
        {
            AppendLog("Twitch-Belohnungen konnten nicht geladen werden: " +
                      exception.Message);
        }
    }


    private void UpdateSpotifyStatus()
    {
        if (_spotify?.IsConnected == true)
        {
            SetSpotifyStatus("Verbunden", ActiveColor);
            _spotifyAccountLabel.Text = "Konto: " +
                (string.IsNullOrWhiteSpace(_spotify.AccountName)
                    ? "verbunden" : _spotify.AccountName);
        }
        else
        {
            SetSpotifyStatus("Nicht verbunden", InactiveColor);
            _spotifyAccountLabel.Text = "Konto: –";
        }
    }


    private void SetSpotifyFailureStatus(Exception exception)
    {
        SetSpotifyStatus(
            exception.Message.Contains("abgelaufen",
                StringComparison.OrdinalIgnoreCase)
                ? "Token abgelaufen" : "Fehler", ErrorColor);
    }


    private void SetSpotifyStatus(string state, Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetSpotifyStatus(state, color)));
            return;
        }
        _spotifyStatusLabel.Text = "● Spotify: " + state;
        _spotifyStatusLabel.ForeColor = color;
    }


    private async Task RefreshMusicGridAsync()
    {
        try
        {
            var entries = await _musicStore.GetEntriesAsync(
                _shutdown?.Token ?? CancellationToken.None);
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PopulateMusicGrid(entries)));
                return;
            }
            PopulateMusicGrid(entries);
        }
        catch (Exception exception)
        {
            AppendLog("Musikwunschliste konnte nicht geladen werden: " +
                      exception.Message);
        }
    }


    private void PopulateMusicGrid(IReadOnlyList<MusicRequestEntry> entries)
    {
        _musicGrid.Rows.Clear();
        foreach (var entry in entries)
        {
            var row = _musicGrid.Rows.Add(
                MusicStatusText(entry.Status), entry.Track?.Name ?? entry.UserInput,
                entry.Track?.Artist ?? "",
                entry.Track is null ? "" :
                    TimeSpan.FromMilliseconds(entry.Track.DurationMs)
                        .ToString("m\\:ss"),
                entry.DisplayName, entry.RedeemedAt.ToLocalTime().ToString("HH:mm"),
                entry.PlaybackMode == MusicPlaybackMode.AddToQueue
                    ? "Warteschlange" : "Sofort",
                entry.Track?.ExternalUrl ?? "", entry.FailureReason);
            _musicGrid.Rows[row].Tag = entry;
        }
    }


    private static string MusicStatusText(MusicRequestStatus status) =>
        status switch
        {
            MusicRequestStatus.Checking => "Wird geprüft",
            MusicRequestStatus.Accepted => "Angenommen",
            MusicRequestStatus.Queued => "In Warteschlange",
            MusicRequestStatus.Playing => "Wird abgespielt",
            MusicRequestStatus.Rejected => "Abgelehnt",
            MusicRequestStatus.Failed => "Fehlgeschlagen",
            MusicRequestStatus.Skipped => "Übersprungen",
            MusicRequestStatus.Completed => "Erledigt",
            _ => status.ToString()
        };


    private MusicRequestEntry? SelectedMusicEntry() =>
        _musicGrid.SelectedRows.Count == 1
            ? _musicGrid.SelectedRows[0].Tag as MusicRequestEntry : null;


    private async Task PlaySelectedMusicEntryAsync()
    {
        var entry = SelectedMusicEntry();
        if (entry?.Track is null || _spotify is null) return;
        try
        {
            await _spotify.PlayAsync(entry.Track,
                (_spotifyDeviceBox.SelectedItem as SpotifyDevice)?.Id,
                _shutdown?.Token ?? CancellationToken.None);
            entry.Status = MusicRequestStatus.Playing;
            await _musicStore.AddOrUpdateAsync(entry, true,
                _shutdown?.Token ?? CancellationToken.None);
            await RefreshMusicGridAsync();
        }
        catch (Exception exception)
        {
            AppendLog("Song konnte nicht abgespielt werden: " + exception.Message);
        }
    }


    private async Task SkipSpotifyAsync()
    {
        try
        {
            if (_spotify is not null)
                await _spotify.SkipAsync(
                    _shutdown?.Token ?? CancellationToken.None);
        }
        catch (Exception exception)
        {
            AppendLog("Spotify-Song konnte nicht übersprungen werden: " +
                      exception.Message);
        }
    }


    private async Task RetrySelectedMusicEntryAsync()
    {
        var entry = SelectedMusicEntry();
        if (entry is null || _musicRequests is null) return;
        await _musicRequests.RetryAsync(entry,
            _shutdown?.Token ?? CancellationToken.None);
    }


    private async Task RemoveSelectedMusicEntryAsync()
    {
        var entry = SelectedMusicEntry();
        if (entry is null) return;
        await _musicStore.RemoveAsync(entry.RedemptionId,
            _shutdown?.Token ?? CancellationToken.None);
        await RefreshMusicGridAsync();
    }


    private void OpenSelectedSpotifyLink()
    {
        var url = SelectedMusicEntry()?.Track?.ExternalUrl;
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("open.spotify.com",
                StringComparison.OrdinalIgnoreCase)) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            { UseShellExecute = true });
    }


    private void BlacklistSelectedMusicUser()
    {
        var entry = SelectedMusicEntry();
        if (entry is null) return;
        AddMusicListValue(_musicUserBlacklistBox, entry.UserLogin);
        SaveMusicRequestSettingsFromControls();
    }


    private void BlacklistSelectedMusicTrack()
    {
        var track = SelectedMusicEntry()?.Track;
        if (track is null) return;
        AddMusicListValue(_musicTrackIdBlacklistBox, track.Id);
        AddMusicListValue(_musicTitleBlacklistBox, track.Name);
        SaveMusicRequestSettingsFromControls();
    }


    private void BlacklistSelectedMusicArtist()
    {
        var artist = SelectedMusicEntry()?.Track?.Artist;
        if (string.IsNullOrWhiteSpace(artist)) return;
        AddMusicListValue(_musicArtistBlacklistBox, artist);
        SaveMusicRequestSettingsFromControls();
    }


    private static void AddMusicListValue(TextBox box, string value)
    {
        var values = ReadMusicList(box);
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value.Trim().TrimStart('@'));
        SetMusicList(box, values);
    }


    private async Task ClearMusicQueueAsync()
    {
        if (MessageBox.Show(this,
                "Alle offenen Musikwünsche aus der lokalen Warteschlange entfernen?",
                "Warteschlange leeren", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await _musicStore.ClearQueueAsync(
            _shutdown?.Token ?? CancellationToken.None);
        await RefreshMusicGridAsync();
    }


    private async Task StartMusicRequestsAsync(
        AppConfig config, TwitchSession session, TwitchService twitch,
        TwitchUser broadcaster, CancellationToken cancellationToken)
    {
        if (!config.MusicRequests.Enabled)
        {
            SetSpotifyStatus(_spotify?.IsConnected == true
                ? "Verbunden · Modul deaktiviert" : "Nicht verbunden",
                _spotify?.IsConnected == true ? ActiveColor : InactiveColor);
            return;
        }


        try
        {
            EnsureSpotify(config.MusicRequests);
            var rewards = await twitch.GetCustomRewardsAsync(
                broadcaster.Id, cancellationToken);
            var selectedReward = rewards.FirstOrDefault(reward => reward.Id.Equals(
                config.MusicRequests.SelectedRewardId, StringComparison.Ordinal));
            if (selectedReward is null)
                throw new InvalidOperationException(
                    "Die ausgewählte Twitch-Musikwunsch-Belohnung wurde nicht gefunden.");
            if (!selectedReward.IsEnabled)
                throw new InvalidOperationException(
                    "Die Twitch-Musikwunsch-Belohnung ist deaktiviert.");
            if (!selectedReward.RequiresInput)
                throw new InvalidOperationException(
                    "Die Twitch-Musikwunsch-Belohnung muss Texteingaben erlauben.");
            config.MusicRequests.SelectedRewardName = selectedReward.Title;
            _musicRequests = new MusicRequestService(
                broadcaster.Id, session.UserId, config.MusicRequests,
                twitch, _spotify!, _musicStore);
            _musicRequests.RequestUpdated += entry => _ = RefreshMusicGridAsync();
            _musicRequestTask = _musicRequests.RunAsync(cancellationToken);
            ObserveMinigameTask(_musicRequestTask!);
            SetSpotifyStatus(_spotify!.IsConnected
                ? "Verbunden · EventSub wird gemeinsam gestartet"
                : "EventSub wird gemeinsam gestartet · Spotify fehlt",
                _spotify.IsConnected ? ActiveColor : WaitingColor);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _musicRequests = null;
            SetSpotifyFailureStatus(exception);
            AppendLog("Musikwünsche wurden für diese Sitzung deaktiviert: " + exception.Message);
            AppendLog("RaidClip, Chat, Punkte, Minigames und Clips starten trotzdem weiter.");
        }
    }




    private void StopMusicRequests()
    {
        _musicEventSub?.Dispose();
        _musicEventSub = null;
        _musicRequests?.Dispose();
        _musicRequests = null;
        _musicRequestTask = null;
        _musicEventSubTask = null;
        UpdateSpotifyStatus();
    }
}


