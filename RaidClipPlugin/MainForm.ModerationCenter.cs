using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly CheckBox _linkFilterEnabledCheck = NewModerationCheck("Linkfilter aktivieren", false);
    private readonly CheckBox _linkFilterWarnCheck = NewModerationCheck("Bot-Warnung senden", true);
    private readonly CheckBox _linkFilterBareDomainsCheck = NewModerationCheck("Domains ohne http erkennen", true);
    private readonly CheckBox _linkFilterObfuscatedCheck = NewModerationCheck("Verschleierte Links erkennen", true);
    private readonly CheckBox _permitEnabledCheck = NewModerationCheck("!permit aktivieren", true);
    private readonly CheckBox _unpermitEnabledCheck = NewModerationCheck("!unpermit aktivieren", true);
    private readonly TextBox _permitCommandBox = new() { Width = 120, Text = "!permit" };
    private readonly TextBox _unpermitCommandBox = new() { Width = 120, Text = "!unpermit" };
    private readonly NumericUpDown _permitDefaultSecondsControl = NewModerationNumber(60, 1, 3600);
    private readonly NumericUpDown _permitMaxSecondsControl = NewModerationNumber(600, 1, 86400);
    private readonly ComboBox _permitModeBox = new() { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _linkFilterActionBox = new() { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _linkFilterTimeoutControl = NewModerationNumber(60, 1, 1209600);
    private readonly TextBox _linkWhitelistBox = new() { Width = 360, PlaceholderText = "twitch.tv, clips.twitch.tv" };
    private readonly TextBox _linkBlacklistBox = new() { Width = 360, PlaceholderText = "gefÃ¤hrliche Domains, durch Komma getrennt" };
    private readonly TextBox _moderationUserWhitelistBox = new() { Width = 360, PlaceholderText = "Usernamen, durch Komma getrennt" };
    private readonly TextBox _linkWarningTemplateBox = new() { Width = 620, Height = 48, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private PermitService? _permitService;
    private ModerationHistoryService? _moderationHistoryService;
    private LinkModerationService? _linkModerationService;

    private static CheckBox NewModerationCheck(string text, bool isChecked) => new() { Text = text, Checked = isChecked, AutoSize = true, Margin = new Padding(8, 14, 4, 4) };
    private static NumericUpDown NewModerationNumber(int value, int minimum, int maximum) => new() { Minimum = minimum, Maximum = maximum, Value = value, Width = 100 };

    private Control BuildModerationCenterPanel()
    {
        if (_permitModeBox.Items.Count == 0) { _permitModeBox.Items.AddRange(new object[] { "Einmalige Linknachricht", "Genau ein Link", "Zeitfenster" }); _permitModeBox.SelectedIndex = 0; }
        if (_linkFilterActionBox.Items.Count == 0) { _linkFilterActionBox.Items.AddRange(new object[] { "Nur loggen", "Nachricht lÃ¶schen", "LÃ¶schen + warnen", "LÃ¶schen + Timeout", "LÃ¶schen + Warnung + Timeout" }); _linkFilterActionBox.SelectedIndex = 2; }
        var box = new GroupBox { Text = "Moderationscenter Â· Linkfilter & Permit", AutoSize = true, Width = 980, Padding = new Padding(10), Margin = new Padding(8, 14, 8, 4), ForeColor = TextColor };
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6), Dock = DockStyle.Fill };
        flow.Controls.Add(_linkFilterEnabledCheck); flow.Controls.Add(_linkFilterWarnCheck); flow.Controls.Add(_linkFilterBareDomainsCheck); flow.Controls.Add(_linkFilterObfuscatedCheck);
        flow.Controls.Add(CreateSettingEditor("Aktion bei Link", _linkFilterActionBox)); flow.Controls.Add(CreateSettingEditor("Timeout (Sek.)", _linkFilterTimeoutControl));
        flow.Controls.Add(_permitEnabledCheck); flow.Controls.Add(_unpermitEnabledCheck); flow.Controls.Add(CreateSettingEditor("Permit-Command", _permitCommandBox));
        flow.Controls.Add(CreateSettingEditor("Unpermit-Command", _unpermitCommandBox)); flow.Controls.Add(CreateSettingEditor("Standarddauer (Sek.)", _permitDefaultSecondsControl));
        flow.Controls.Add(CreateSettingEditor("Max. Dauer (Sek.)", _permitMaxSecondsControl)); flow.Controls.Add(CreateSettingEditor("Permit-Modus", _permitModeBox));
        flow.Controls.Add(CreateSettingEditor("Domain-Whitelist", _linkWhitelistBox)); flow.Controls.Add(CreateSettingEditor("Domain-Blacklist", _linkBlacklistBox));
        flow.Controls.Add(CreateSettingEditor("User-Whitelist", _moderationUserWhitelistBox)); flow.Controls.Add(CreateSettingEditor("Warnnachricht", _linkWarningTemplateBox));
        flow.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = MutedTextColor, Margin = new Padding(8, 10, 4, 4), Text = "Erkannte MusikwÃ¼nsche Ã¼ber den konfigurierten Songrequest-Command werden vor dem allgemeinen Linkfilter geschÃ¼tzt. Normale Spotify-/YouTube-/TIDAL-Links ohne gÃ¼ltigen Request werden weiterhin geprÃ¼ft." });
        box.Controls.Add(flow); return box;
    }

    private void EnsureModerationCenterServices()
    {
        _permitService ??= new PermitService();
        _moderationHistoryService ??= new ModerationHistoryService();
        _linkModerationService ??= new LinkModerationService(_permitService, _moderationHistoryService);
    }

    private async Task ProcessModerationCenterAsync(ChatMessage message, AppConfig config, TwitchService twitch, string broadcasterId, string broadcasterLogin, string moderatorId, CancellationToken token)
    {
        EnsureModerationCenterServices();
        await _linkModerationService!.ProcessAsync(message, config, _chatModeration, twitch, broadcasterId, broadcasterLogin, moderatorId, IsRecognizedMusicRequestCommand, AppendLog, token);
    }

    private bool IsRecognizedMusicRequestCommand(ChatMessage message)
    {
        if (_activeConfig is null || !_activeConfig.MusicRequests.Enabled || !_activeConfig.MusicRequests.ChatCommandEnabled) return false;
        var parsed = ChatCommandParser.Parse(message.Text);
        if (!parsed.IsCommand || string.IsNullOrWhiteSpace(parsed.Arguments)) return false;
        var command = "!" + parsed.Command;
        var music = _activeConfig.MusicRequests;
        return command.Equals(music.ChatCommand, StringComparison.OrdinalIgnoreCase) ||
            (music.ChatCommandAliases ?? new List<string>()).Any(alias => command.Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadModerationCenterSettings(AppConfig config)
    {
        _linkFilterEnabledCheck.Checked = config.Moderation.LinkFilter.Enabled;
        _linkFilterWarnCheck.Checked = config.Moderation.LinkFilter.BotResponseEnabled;
        _linkFilterBareDomainsCheck.Checked = config.Moderation.LinkFilter.DetectBareDomains;
        _linkFilterObfuscatedCheck.Checked = config.Moderation.LinkFilter.DetectObfuscatedLinks;
        _permitEnabledCheck.Checked = config.Moderation.Permit.Enabled;
        _unpermitEnabledCheck.Checked = config.Moderation.Permit.UnpermitEnabled;
        _permitCommandBox.Text = config.Moderation.Permit.Command;
        _unpermitCommandBox.Text = config.Moderation.Permit.UnpermitCommand;
        SetNumericValue(_permitDefaultSecondsControl, config.Moderation.Permit.DefaultDurationSeconds);
        SetNumericValue(_permitMaxSecondsControl, config.Moderation.Permit.MaximumDurationSeconds);
        _permitModeBox.SelectedIndex = Math.Clamp((int)config.Moderation.Permit.Mode, 0, 2);
        _linkFilterActionBox.SelectedIndex = Math.Clamp((int)config.Moderation.LinkFilter.Action, 0, 4);
        SetNumericValue(_linkFilterTimeoutControl, config.Moderation.LinkFilter.TimeoutSeconds);
        _linkWhitelistBox.Text = string.Join(", ", config.Moderation.LinkFilter.WhitelistedDomains);
        _linkBlacklistBox.Text = string.Join(", ", config.Moderation.LinkFilter.BlacklistedDomains);
        _moderationUserWhitelistBox.Text = string.Join(", ", config.Moderation.UserWhitelist);
        _linkWarningTemplateBox.Text = config.Moderation.LinkFilter.WarningTemplate;
    }

    private void ReadModerationCenterSettings(AppConfig config)
    {
        config.Moderation.LinkFilter.Enabled = _linkFilterEnabledCheck.Checked;
        config.Moderation.LinkFilter.BotResponseEnabled = _linkFilterWarnCheck.Checked;
        config.Moderation.LinkFilter.DetectBareDomains = _linkFilterBareDomainsCheck.Checked;
        config.Moderation.LinkFilter.DetectObfuscatedLinks = _linkFilterObfuscatedCheck.Checked;
        config.Moderation.LinkFilter.Action = (LinkModerationAction)Math.Max(0, _linkFilterActionBox.SelectedIndex);
        config.Moderation.LinkFilter.TimeoutSeconds = decimal.ToInt32(_linkFilterTimeoutControl.Value);
        config.Moderation.Permit.Enabled = _permitEnabledCheck.Checked;
        config.Moderation.Permit.UnpermitEnabled = _unpermitEnabledCheck.Checked;
        config.Moderation.Permit.Command = NormalizeCommandText(_permitCommandBox.Text, "!permit");
        config.Moderation.Permit.UnpermitCommand = NormalizeCommandText(_unpermitCommandBox.Text, "!unpermit");
        config.Moderation.Permit.DefaultDurationSeconds = decimal.ToInt32(_permitDefaultSecondsControl.Value);
        config.Moderation.Permit.MaximumDurationSeconds = decimal.ToInt32(_permitMaxSecondsControl.Value);
        config.Moderation.Permit.Mode = (PermitMode)Math.Max(0, _permitModeBox.SelectedIndex);
        config.Moderation.LinkFilter.WhitelistedDomains = SplitSettingsList(_linkWhitelistBox.Text);
        config.Moderation.LinkFilter.BlacklistedDomains = SplitSettingsList(_linkBlacklistBox.Text);
        config.Moderation.UserWhitelist = SplitSettingsList(_moderationUserWhitelistBox.Text).Select(item => item.TrimStart('@')).ToList();
        config.Moderation.LinkFilter.WarningTemplate = _linkWarningTemplateBox.Text.Trim();
    }

    private static string NormalizeCommandText(string value, string fallback) { var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim(); return text.StartsWith('!') ? text : "!" + text; }
    private static List<string> SplitSettingsList(string value) => (value ?? string.Empty)
        .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
