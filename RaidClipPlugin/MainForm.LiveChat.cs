using System.Collections.Concurrent;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private LiveChatService? _liveChat;
    private EmoteCatalogService? _emoteCatalog;
    private EmoteCacheService? _emoteCache;
    private ChatMessageRenderer? _chatRenderer;
    private readonly ConcurrentQueue<LiveChatMessage> _liveChatQueue = new();
    private readonly System.Windows.Forms.Timer _liveChatTimer = new() { Interval = 100 };
    private readonly FlowLayoutPanel _liveChatList = new() { Dock = DockStyle.Fill, AutoScroll = true,
        FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(12, 12, 14), Padding = new Padding(4) };
    private readonly CheckBox _liveChatEnabledCheck = NewCheck("Livechat aktiv", true);
    private readonly CheckBox _liveChatAutoScrollCheck = NewCheck("Auto-Scroll", true);
    private readonly CheckBox _liveChatTimestampsCheck = NewCheck("Zeitstempel", true);
    private readonly CheckBox _liveChatBadgesCheck = NewCheck("Badges", true);
    private readonly CheckBox _liveChatUserColorsCheck = NewCheck("Benutzerfarben", true);
    private readonly CheckBox _liveChatHideCommandsCheck = NewCheck("Commands ausblenden", false);
    private readonly CheckBox _liveChatHideBotsCheck = NewCheck("Botnachrichten ausblenden", false);
    private readonly CheckBox _liveChatSystemCheck = NewCheck("Systemnachrichten anzeigen", true);
    private readonly CheckBox _liveChatTwitchEmotesCheck = NewCheck("Twitch-Emotes", true);
    private readonly CheckBox _liveChatBttvCheck = NewCheck("BTTV-Emotes", false);
    private readonly CheckBox _liveChatSevenTvCheck = NewCheck("7TV-Emotes", false);
    private readonly CheckBox _liveChatAnimatedCheck = NewCheck("Animierte Emotes", true);
    private readonly CheckBox _liveChatCacheCheck = NewCheck("Emote-Cache", true);
    private readonly TextBox _liveChatSearchBox = new() { Width = 230, PlaceholderText = "Benutzer oder Nachricht suchen" };
    private readonly NumericUpDown _liveChatMaxControl = NewNumber(1000, 100, 10000);
    private readonly NumericUpDown _liveChatEmoteSizeControl = NewNumber(28, 16, 64);
    private readonly Button _liveChatPauseButton = NewHeistActionButton("Pausieren", 120);
    private readonly Button _liveChatClearButton = NewHeistActionButton("Chat leeren", 120);
    private readonly Button _liveChatSaveButton = NewHeistActionButton("Einstellungen speichern", 190);
    private readonly Label _liveChatStatusLabel = new() { AutoSize = true, ForeColor = MutedTextColor,
        Text = "Sichtbar: 0 · Gespeichert: 0 · Status: Deaktiviert" };

    private TabPage BuildLiveChatTab()
    {
        var page = new TabPage("Livechat") { BackColor = SurfaceColor, ForeColor = TextColor, Padding = new Padding(6) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(2) };
        top.Controls.AddRange(new Control[] { _liveChatEnabledCheck, _liveChatAutoScrollCheck,
            _liveChatPauseButton, _liveChatClearButton, _liveChatSearchBox,
            CreateSettingEditor("Max. Nachrichten", _liveChatMaxControl), _liveChatSaveButton });
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(2) };
        filters.Controls.AddRange(new Control[] { _liveChatTimestampsCheck, _liveChatBadgesCheck,
            _liveChatUserColorsCheck, _liveChatHideCommandsCheck, _liveChatHideBotsCheck,
            _liveChatSystemCheck, _liveChatTwitchEmotesCheck, _liveChatBttvCheck,
            _liveChatSevenTvCheck, _liveChatAnimatedCheck, _liveChatCacheCheck,
            CreateSettingEditor("Emote-Größe", _liveChatEmoteSizeControl) });
        layout.Controls.Add(top, 0, 0); layout.Controls.Add(filters, 0, 1);
        layout.Controls.Add(_liveChatList, 0, 2); layout.Controls.Add(_liveChatStatusLabel, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private void InitializeLiveChatEvents()
    {
        _liveChatTimer.Tick += (_, _) => FlushLiveChatQueue();
        _liveChatTimer.Start();
        _liveChatPauseButton.Click += (_, _) =>
        {
            if (_liveChat is null) return;
            _liveChat.SetPaused(!_liveChat.IsPaused);
            _liveChatPauseButton.Text = _liveChat.IsPaused ? "Fortsetzen" : "Pausieren";
            UpdateLiveChatStatus();
        };
        _liveChatClearButton.Click += (_, _) =>
        {
            _liveChat?.Clear();
            ClearLiveChatControls();
        };
        _liveChatSearchBox.TextChanged += async (_, _) => await RenderLiveChatSnapshotAsync();
        foreach (var check in new[] { _liveChatEnabledCheck, _liveChatAutoScrollCheck,
            _liveChatTimestampsCheck, _liveChatBadgesCheck, _liveChatUserColorsCheck,
            _liveChatHideCommandsCheck, _liveChatHideBotsCheck, _liveChatSystemCheck,
            _liveChatTwitchEmotesCheck, _liveChatBttvCheck, _liveChatSevenTvCheck,
            _liveChatAnimatedCheck, _liveChatCacheCheck })
            check.CheckedChanged += async (_, _) => { ApplyLiveChatControls(); await RenderLiveChatSnapshotAsync(); };
        _liveChatMaxControl.ValueChanged += (_, _) => ApplyLiveChatControls();
        _liveChatEmoteSizeControl.ValueChanged += async (_, _) => { ApplyLiveChatControls(); await RenderLiveChatSnapshotAsync(); };
        _liveChatBttvCheck.CheckedChanged += async (_, _) => await ReloadExternalEmotesAsync();
        _liveChatSevenTvCheck.CheckedChanged += async (_, _) => await ReloadExternalEmotesAsync();
        _liveChatSaveButton.Click += (_, _) => SaveSettingsFromControls();
    }

    private async Task ReloadExternalEmotesAsync()
    {
        if (_emoteCatalog is null || _broadcaster is null) return;
        await _emoteCatalog.InitializeAsync(_broadcaster.Id, ReadLiveChatConfig(),
            _shutdown?.Token ?? CancellationToken.None);
        await RenderLiveChatSnapshotAsync();
    }

    private async Task StartLiveChatAsync(AppConfig config, string botUserId,
        string channelId, CancellationToken cancellationToken)
    {
        StopLiveChat();
        _liveChat = new LiveChatService(config.LiveChat, botUserId,
            config.Minigame.PointsBlacklist, _commandRegistry);
        _liveChat.MessageAdded += message => _liveChatQueue.Enqueue(message);
        _liveChat.HistoryChanged += () =>
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(new Action(UpdateLiveChatStatus));
            else UpdateLiveChatStatus();
        };
        _emoteCache = new EmoteCacheService();
        _chatRenderer = new ChatMessageRenderer(_emoteCache);
        _emoteCatalog = new EmoteCatalogService();
        await _emoteCatalog.InitializeAsync(channelId, config.LiveChat, cancellationToken);
        UpdateLiveChatStatus();
    }

    private Task HandleLiveChatMessageAsync(ChatMessage message)
    {
        _liveChat?.ProcessMessage(message);
        return Task.CompletedTask;
    }

    private void FlushLiveChatQueue()
    {
        if (_liveChat is null) { UpdateLiveChatStatus(); return; }
        var processed = 0;
        while (processed < 40 && _liveChatQueue.TryDequeue(out var message))
        {
            processed++;
            if (_liveChat.IsVisible(message, _liveChatSearchBox.Text)) _ = RenderLiveChatMessageAsync(message);
        }
        UpdateLiveChatStatus();
    }

    private async Task RenderLiveChatMessageAsync(LiveChatMessage message)
    {
        if (_chatRenderer is null) return;
        try
        {
            var config = ReadLiveChatConfig();
            var control = await _chatRenderer.RenderAsync(message, config,
                _emoteCatalog?.Emotes ?? new Dictionary<string, ExternalEmote>(),
                _shutdown?.Token ?? CancellationToken.None);
            if (IsDisposed || control.IsDisposed) return;
            _liveChatList.SuspendLayout();
            _liveChatList.Controls.Add(control);
            while (_liveChatList.Controls.Count > config.MaxMessages)
            {
                var oldest = _liveChatList.Controls[0];
                _liveChatList.Controls.RemoveAt(0); oldest.Dispose();
            }
            _liveChatList.ResumeLayout();
            if (config.AutoScroll && _liveChatList.Controls.Count > 0)
                _liveChatList.ScrollControlIntoView(control);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { AppendLog("Fehler beim Rendern einer Livechat-Nachricht: " + exception.Message); }
    }

    private async Task RenderLiveChatSnapshotAsync()
    {
        if (_liveChat is null) { ClearLiveChatControls(); return; }
        var snapshot = _liveChat.GetVisibleSnapshot(_liveChatSearchBox.Text);
        ClearLiveChatControls();
        foreach (var message in snapshot) await RenderLiveChatMessageAsync(message);
        UpdateLiveChatStatus();
    }

    private void ClearLiveChatControls()
    {
        _liveChatList.SuspendLayout();
        for (var index = _liveChatList.Controls.Count - 1; index >= 0; index--)
        {
            var control = _liveChatList.Controls[index];
            _liveChatList.Controls.RemoveAt(index);
            control.Dispose();
        }
        _liveChatList.ResumeLayout();
        UpdateLiveChatStatus();
    }

    private void ApplyLiveChatControls() => _liveChat?.UpdateConfig(ReadLiveChatConfig());

    private LiveChatConfig ReadLiveChatConfig() => LiveChatService.NormalizeConfig(new LiveChatConfig
    {
        Enabled = _liveChatEnabledCheck.Checked,
        ShowTimestamps = _liveChatTimestampsCheck.Checked,
        ShowBadges = _liveChatBadgesCheck.Checked,
        ShowUserColors = _liveChatUserColorsCheck.Checked,
        HideCommands = _liveChatHideCommandsCheck.Checked,
        HideBotMessages = _liveChatHideBotsCheck.Checked,
        ShowSystemMessages = _liveChatSystemCheck.Checked,
        AutoScroll = _liveChatAutoScrollCheck.Checked,
        MaxMessages = (int)_liveChatMaxControl.Value,
        EnableTwitchEmotes = _liveChatTwitchEmotesCheck.Checked,
        EnableBttvEmotes = _liveChatBttvCheck.Checked,
        EnableSevenTvEmotes = _liveChatSevenTvCheck.Checked,
        EnableAnimatedEmotes = _liveChatAnimatedCheck.Checked,
        EmoteSize = (int)_liveChatEmoteSizeControl.Value,
        CacheEmotes = _liveChatCacheCheck.Checked
    });

    private void LoadLiveChatSettings(LiveChatConfig config)
    {
        config = LiveChatService.NormalizeConfig(config);
        _liveChatEnabledCheck.Checked = config.Enabled;
        _liveChatAutoScrollCheck.Checked = config.AutoScroll;
        _liveChatTimestampsCheck.Checked = config.ShowTimestamps;
        _liveChatBadgesCheck.Checked = config.ShowBadges;
        _liveChatUserColorsCheck.Checked = config.ShowUserColors;
        _liveChatHideCommandsCheck.Checked = config.HideCommands;
        _liveChatHideBotsCheck.Checked = config.HideBotMessages;
        _liveChatSystemCheck.Checked = config.ShowSystemMessages;
        _liveChatTwitchEmotesCheck.Checked = config.EnableTwitchEmotes;
        _liveChatBttvCheck.Checked = config.EnableBttvEmotes;
        _liveChatSevenTvCheck.Checked = config.EnableSevenTvEmotes;
        _liveChatAnimatedCheck.Checked = config.EnableAnimatedEmotes;
        _liveChatCacheCheck.Checked = config.CacheEmotes;
        SetNumericValue(_liveChatMaxControl, config.MaxMessages);
        SetNumericValue(_liveChatEmoteSizeControl, config.EmoteSize);
    }

    private void ReadLiveChatSettings(AppConfig config)
    {
        config.LiveChat = ReadLiveChatConfig();
    }

    private void UpdateLiveChatStatus()
    {
        var visible = _liveChat?.GetVisibleSnapshot(_liveChatSearchBox.Text).Count ?? 0;
        var stored = _liveChat?.StoredCount ?? 0;
        var state = !_liveChatEnabledCheck.Checked ? "Deaktiviert" : _liveChat?.IsPaused == true ? "Pausiert" : "Aktiv";
        _liveChatStatusLabel.Text = $"Sichtbar: {visible} · Gespeichert: {stored} · Status: {state}";
    }

    private void StopLiveChat()
    {
        while (_liveChatQueue.TryDequeue(out _)) { }
        _liveChat = null;
        _emoteCatalog?.Dispose(); _emoteCatalog = null;
        ClearLiveChatControls();
        _emoteCache?.Dispose(); _emoteCache = null;
        _chatRenderer = null;
    }
}
