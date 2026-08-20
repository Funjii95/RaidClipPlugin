using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using System.Security.Cryptography;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly CheckBox _eventTriggersEnabled = NewCheck("Event-Trigger aktivieren", false);
    private readonly CheckBox _followAlertEnabled = NewCheck("Follower", false);
    private readonly CheckBox _tipAlertEnabled = NewCheck("Tips", false);
    private readonly CheckBox _subscriptionAlertEnabled = NewCheck("Abonnements", false);
    private readonly CheckBox _cheerAlertEnabled = NewCheck("Cheers / Bits", false);
    private readonly CheckBox _adAlertEnabled = NewCheck("Werbepausen", false);
    private readonly TextBox _followAlertMessage = AlertText("Danke für den Follow, @{user}!");
    private readonly TextBox _tipAlertMessage = AlertText("Vielen Dank an {user} für {amount} {currency}!");
    private readonly TextBox _subscriptionAlertMessage = AlertText("Danke für dein Abo, @{user}!");
    private readonly TextBox _cheerAlertMessage = AlertText("Danke @{user} für {amount} Bits!");
    private readonly TextBox _adAlertMessage = AlertText("Werbepause für {duration} Sekunden – gleich geht es weiter!");
    private readonly NumericUpDown _tipMinimum = AlertAmount();
    private readonly NumericUpDown _cheerMinimum = AlertAmount();
    private readonly ComboBox _followSound = AlertSound("Hinweis");
    private readonly ComboBox _tipSound = AlertSound("Erfolg");
    private readonly ComboBox _subscriptionSound = AlertSound("Erfolg");
    private readonly ComboBox _cheerSound = AlertSound("Glocke");
    private readonly ComboBox _adSound = AlertSound("Achtung");
    private readonly CheckBox _streamElementsEnabled = NewCheck("StreamElements aktiv", false);
    private readonly TextBox _streamElementsChannel = new() { Width = 520 };
    private readonly TextBox _streamElementsToken = SecretText();
    private readonly ComboBox _streamElementsTokenType = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _streamlabsEnabled = NewCheck("Streamlabs aktiv", false);
    private readonly TextBox _streamlabsToken = SecretText();
    private readonly CheckBox _koFiEnabled = NewCheck("Ko-fi aktiv", false);
    private readonly TextBox _koFiToken = SecretText();
    private readonly CheckBox _tipeeeEnabled = NewCheck("TipeeeStream aktiv", false);
    private readonly TextBox _tipeeeToken = SecretText();
    private readonly Label _tipProviderStatus = new() { AutoSize = true, ForeColor = MutedTextColor };
    private TipEventService? _tipEventService;
    private Task? _tipEventTask;

    private static TextBox AlertText(string text) => new()
    {
        Width = 720,
        Height = 48,
        Multiline = true,
        Text = text
    };

    private static TextBox SecretText() => new()
    {
        Width = 520,
        UseSystemPasswordChar = true
    };

    private static NumericUpDown AlertAmount() => new()
    {
        Minimum = 0,
        Maximum = 100_000_000,
        DecimalPlaces = 2,
        Width = 150
    };

    private static ComboBox AlertSound(string selected)
    {
        var box = new ComboBox
        {
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        box.Items.AddRange(new object[]
        {
            "Kein Sound", "Hinweis", "Erfolg", "Glocke", "Achtung", "Frage"
        });
        box.SelectedItem = selected;
        return box;
    }

    private Control BuildEventTriggerSettingsPanel()
    {
        _streamElementsTokenType.Items.AddRange(new object[] { "jwt", "apikey", "oauth2" });
        if (_streamElementsTokenType.SelectedIndex < 0) _streamElementsTokenType.SelectedIndex = 0;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var alerts = new TabPage("Antworten") { BackColor = BackgroundColor };
        var alertFlow = CreateMinigameFlow();
        alertFlow.Controls.Add(_eventTriggersEnabled);
        AddAlertEditor(alertFlow, _followAlertEnabled, "Antwort bei Follow", _followAlertMessage, null, _followSound);
        AddAlertEditor(alertFlow, _tipAlertEnabled, "Antwort bei Tip", _tipAlertMessage, _tipMinimum, _tipSound);
        AddAlertEditor(alertFlow, _subscriptionAlertEnabled, "Antwort bei Abo / Geschenkabo", _subscriptionAlertMessage, null, _subscriptionSound);
        AddAlertEditor(alertFlow, _cheerAlertEnabled, "Antwort bei Cheers / Bits", _cheerAlertMessage, _cheerMinimum, _cheerSound);
        AddAlertEditor(alertFlow, _adAlertEnabled, "Antwort bei Werbepause", _adAlertMessage, null, _adSound);
        alertFlow.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(930, 0),
            ForeColor = MutedTextColor,
            Text = "Platzhalter: {user}, {amount}, {currency}, {message}, {months}, " +
                   "{quantity}, {gift}, {provider}, {duration}, {type}."
        });
        var save = NewHeistActionButton("Event-Trigger speichern", 210);
        save.Click += (_, _) => SaveSettingsFromControls();
        alertFlow.Controls.Add(save);
        alerts.Controls.Add(alertFlow);

        var providers = new TabPage("Tip-Anbieter") { BackColor = BackgroundColor };
        var providerFlow = CreateMinigameFlow();
        providerFlow.Controls.Add(_streamElementsEnabled);
        providerFlow.Controls.Add(CreateSettingEditor("StreamElements Channel-ID", _streamElementsChannel));
        providerFlow.Controls.Add(CreateSettingEditor("StreamElements JWT / Token", _streamElementsToken));
        providerFlow.Controls.Add(CreateSettingEditor("StreamElements Token-Typ", _streamElementsTokenType));
        providerFlow.Controls.Add(_streamlabsEnabled);
        providerFlow.Controls.Add(CreateSettingEditor("Streamlabs OAuth Access Token", _streamlabsToken));
        providerFlow.Controls.Add(_koFiEnabled);
        providerFlow.Controls.Add(CreateSettingEditor("Ko-fi Relay-Prüftoken", _koFiToken));
        providerFlow.Controls.Add(_tipeeeEnabled);
        providerFlow.Controls.Add(CreateSettingEditor("TipeeeStream Relay-Prüftoken", _tipeeeToken));
        providerFlow.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(930, 0),
            ForeColor = MutedTextColor,
            Text = "Lokale Webhook-Ziele: http://127.0.0.1:17892/kofi und /tipeeestream. " +
                   "Für externe Aufrufe wird ein HTTPS-Relay/Tunnel benötigt; dieser muss den " +
                   "Prüftoken als Header X-RaidClip-Token weiterreichen."
        });
        providerFlow.Controls.Add(_tipProviderStatus);
        providers.Controls.Add(providerFlow);
        tabs.TabPages.Add(alerts);
        tabs.TabPages.Add(providers);
        return tabs;
    }

    private void AddAlertEditor(
        FlowLayoutPanel flow,
        CheckBox enabled,
        string label,
        TextBox message,
        NumericUpDown? minimum,
        ComboBox sound)
    {
        flow.Controls.Add(enabled);
        flow.Controls.Add(CreateSettingEditor(label, message));
        if (minimum is not null)
            flow.Controls.Add(CreateSettingEditor("Mindestbetrag / Mindest-Bits", minimum));
        var soundRow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        soundRow.Controls.Add(sound);
        var preview = NewHeistActionButton("Anhören", 110);
        preview.Click += (_, _) => PlayEventSound(sound.Text);
        soundRow.Controls.Add(preview);
        var custom = NewHeistActionButton("Eigene WAV…", 130);
        custom.Click += (_, _) => ChooseCustomEventSound(sound);
        soundRow.Controls.Add(custom);
        flow.Controls.Add(CreateSettingEditor("Lokaler Sound für den Streamer", soundRow));
    }

    private void ChooseCustomEventSound(ComboBox sound)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Eigenen Event-Sound auswählen",
            Filter = "Wave-Audiodatei (*.wav)|*.wav",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RaidClipPlugin", "EventSounds");
            Directory.CreateDirectory(directory);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dialog.FileName)))[..12];
            var safeName = string.Concat(Path.GetFileNameWithoutExtension(dialog.FileName)
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            var destination = Path.Combine(directory, $"{safeName}-{hash}.wav");
            if (!File.Exists(destination)) File.Copy(dialog.FileName, destination);
            var value = "Datei: " + destination;
            if (!sound.Items.Contains(value)) sound.Items.Add(value);
            sound.SelectedItem = value;
            PlayEventSound(value);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "Der Sound konnte nicht übernommen werden: " + exception.Message,
                "Event-Sound", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadEventTriggerSettings(EventTriggerConfig config)
    {
        _eventTriggersEnabled.Checked = config.Enabled;
        LoadRule(config.Follow, _followAlertEnabled, _followAlertMessage, _followSound);
        LoadRule(config.Tip, _tipAlertEnabled, _tipAlertMessage, _tipSound, _tipMinimum);
        LoadRule(config.Subscription, _subscriptionAlertEnabled, _subscriptionAlertMessage, _subscriptionSound);
        LoadRule(config.Cheer, _cheerAlertEnabled, _cheerAlertMessage, _cheerSound, _cheerMinimum);
        LoadRule(config.AdBreak, _adAlertEnabled, _adAlertMessage, _adSound);
        var providers = config.TipProviders;
        _streamElementsEnabled.Checked = providers.StreamElements.Enabled;
        _streamElementsChannel.Text = providers.StreamElements.ChannelId;
        _streamElementsToken.Text = providers.StreamElements.Token;
        _streamElementsTokenType.SelectedItem = providers.StreamElements.TokenType;
        if (_streamElementsTokenType.SelectedIndex < 0) _streamElementsTokenType.SelectedIndex = 0;
        _streamlabsEnabled.Checked = providers.Streamlabs.Enabled;
        _streamlabsToken.Text = providers.Streamlabs.AccessToken;
        _koFiEnabled.Checked = providers.KoFi.Enabled;
        _koFiToken.Text = providers.KoFi.VerificationToken;
        _tipeeeEnabled.Checked = providers.TipeeeStream.Enabled;
        _tipeeeToken.Text = providers.TipeeeStream.VerificationToken;
    }

    private static void LoadRule(ChatAlertRuleConfig rule, CheckBox enabled,
        TextBox message, ComboBox sound, NumericUpDown? minimum = null)
    {
        enabled.Checked = rule.Enabled;
        message.Text = rule.Message;
        if (!string.IsNullOrWhiteSpace(rule.Sound) &&
            rule.Sound.StartsWith("Datei: ", StringComparison.OrdinalIgnoreCase) &&
            !sound.Items.Contains(rule.Sound))
            sound.Items.Add(rule.Sound);
        sound.SelectedItem = rule.Sound;
        if (sound.SelectedIndex < 0) sound.SelectedIndex = 0;
        if (minimum is not null)
            minimum.Value = Math.Clamp(rule.MinimumAmount, minimum.Minimum, minimum.Maximum);
    }

    private void ReadEventTriggerSettings(AppConfig config)
    {
        var triggers = config.EventTriggers;
        triggers.Enabled = _eventTriggersEnabled.Checked;
        ReadRule(triggers.Follow, _followAlertEnabled, _followAlertMessage, _followSound);
        ReadRule(triggers.Tip, _tipAlertEnabled, _tipAlertMessage, _tipSound, _tipMinimum);
        ReadRule(triggers.Subscription, _subscriptionAlertEnabled, _subscriptionAlertMessage, _subscriptionSound);
        ReadRule(triggers.Cheer, _cheerAlertEnabled, _cheerAlertMessage, _cheerSound, _cheerMinimum);
        ReadRule(triggers.AdBreak, _adAlertEnabled, _adAlertMessage, _adSound);
        triggers.TipProviders.StreamElements.Enabled = _streamElementsEnabled.Checked;
        triggers.TipProviders.StreamElements.ChannelId = _streamElementsChannel.Text.Trim();
        triggers.TipProviders.StreamElements.Token = _streamElementsToken.Text.Trim();
        triggers.TipProviders.StreamElements.TokenType = _streamElementsTokenType.Text;
        triggers.TipProviders.Streamlabs.Enabled = _streamlabsEnabled.Checked;
        triggers.TipProviders.Streamlabs.AccessToken = _streamlabsToken.Text.Trim();
        triggers.TipProviders.KoFi.Enabled = _koFiEnabled.Checked;
        triggers.TipProviders.KoFi.VerificationToken = _koFiToken.Text.Trim();
        triggers.TipProviders.TipeeeStream.Enabled = _tipeeeEnabled.Checked;
        triggers.TipProviders.TipeeeStream.VerificationToken = _tipeeeToken.Text.Trim();
    }

    private static void ReadRule(ChatAlertRuleConfig rule, CheckBox enabled,
        TextBox message, ComboBox sound, NumericUpDown? minimum = null)
    {
        rule.Enabled = enabled.Checked;
        rule.Message = message.Text.Trim();
        rule.MinimumAmount = minimum?.Value ?? 0;
        rule.Sound = sound.Text;
    }

    private void StartTipEvents(AppConfig config, TwitchService twitch,
        string broadcasterId, string senderId, CancellationToken cancellationToken)
    {
        if (!config.EventTriggers.Enabled || !config.EventTriggers.Tip.Enabled) return;
        _tipEventService = new TipEventService(config.EventTriggers.TipProviders);
        _tipEventService.StatusChanged += SetTipProviderStatus;
        _tipEventService.TipReceived += alert => HandleChatAlertAsync(
            alert, config, twitch, broadcasterId, senderId, cancellationToken);
        _tipEventTask = _tipEventService.RunAsync(cancellationToken);
    }

    private void SetTipProviderStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetTipProviderStatus(status)));
            return;
        }
        _tipProviderStatus.Text = status;
    }

    private async Task HandleChatAlertAsync(ChatAlertEvent alert, AppConfig config,
        TwitchService twitch, string broadcasterId, string senderId,
        CancellationToken cancellationToken)
    {
        if (!config.EventTriggers.Enabled) return;
        var rule = alert.Kind switch
        {
            ChatAlertKind.Follow => config.EventTriggers.Follow,
            ChatAlertKind.Tip => config.EventTriggers.Tip,
            ChatAlertKind.Subscription => config.EventTriggers.Subscription,
            ChatAlertKind.Cheer => config.EventTriggers.Cheer,
            ChatAlertKind.AdBreak => config.EventTriggers.AdBreak,
            _ => null
        };
        if (rule is null || !rule.Enabled || alert.Amount < rule.MinimumAmount) return;
        var text = FormatChatAlert(rule.Message, alert);
        if (string.IsNullOrWhiteSpace(text)) return;
        PlayEventSound(rule.Sound);
        try
        {
            await twitch.SendChatMessageAsync(broadcasterId, senderId, text, cancellationToken);
            AppendLog($"Event-Trigger {alert.Kind} ({alert.Provider}): {text}");
        }
        catch (Exception exception)
        {
            AppendLog($"Event-Trigger {alert.Kind} fehlgeschlagen: {exception.Message}");
        }
    }

    public static string FormatChatAlert(string template, ChatAlertEvent alert) =>
        (template ?? "")
            .Replace("{user}", alert.UserName, StringComparison.OrdinalIgnoreCase)
            .Replace("{amount}", alert.Amount.ToString("0.##",
                System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{currency}", alert.Currency, StringComparison.OrdinalIgnoreCase)
            .Replace("{message}", alert.Message, StringComparison.OrdinalIgnoreCase)
            .Replace("{months}", alert.Months.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{quantity}", alert.Quantity.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{gift}", alert.IsGift ? "ja" : "nein", StringComparison.OrdinalIgnoreCase)
            .Replace("{provider}", alert.Provider, StringComparison.OrdinalIgnoreCase)
            .Replace("{duration}", alert.DurationSeconds.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{type}", alert.IsAutomatic ? "automatisch" : "manuell", StringComparison.OrdinalIgnoreCase);

    public static void PlayEventSound(string? sound)
    {
        if (sound?.StartsWith("Datei: ", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = sound[7..].Trim();
            if (File.Exists(path))
            {
                try { new System.Media.SoundPlayer(path).Play(); }
                catch { System.Media.SystemSounds.Hand.Play(); }
            }
            return;
        }
        switch (sound)
        {
            case "Hinweis": System.Media.SystemSounds.Asterisk.Play(); break;
            case "Erfolg": System.Media.SystemSounds.Exclamation.Play(); break;
            case "Glocke": System.Media.SystemSounds.Beep.Play(); break;
            case "Achtung": System.Media.SystemSounds.Hand.Play(); break;
            case "Frage": System.Media.SystemSounds.Question.Play(); break;
        }
    }
}
