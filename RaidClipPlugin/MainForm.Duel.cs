using RaidClipPlugin.Config;
using RaidClipPlugin.Services;


namespace RaidClipPlugin;


public sealed partial class MainForm
{
    private readonly CheckBox _duelEnabledCheck = NewCheck("Duel aktivieren", false);
    private readonly TextBox _duelCommandBox = new() { Width = 140 };
    private readonly TextBox _duelAcceptCommandBox = new() { Width = 140 };
    private readonly TextBox _duelDenyCommandBox = new() { Width = 140 };
    private readonly NumericUpDown _duelMinimumBetControl = NewNumber(10, 1, 9_000_000_000);
    private readonly NumericUpDown _duelMaximumBetControl = NewNumber(10000, 1, 9_000_000_000);
    private readonly NumericUpDown _duelTimeoutControl = NewNumber(60, 10, 300);
    private readonly NumericUpDown _duelUserCooldownControl = NewNumber(30, 0, 86400);
    private readonly NumericUpDown _duelGlobalCooldownControl = NewNumber(3, 0, 3600);
    private readonly CheckBox _duelAllInCheck = NewCheck("All-In erlauben", true);
    private readonly CheckBox _duelEveryoneCheck = NewCheck("Alle Zuschauer", true);
    private readonly CheckBox _duelFollowersCheck = NewCheck("Follower", true);
    private readonly CheckBox _duelSubscribersCheck = NewCheck("Subscriber", true);
    private readonly CheckBox _duelVipsCheck = NewCheck("VIPs", true);
    private readonly CheckBox _duelModeratorsCheck = NewCheck("Moderatoren", true);
    private readonly CheckBox _duelFairModeCheck = NewCheck("Fairer Modus (50 / 50)", true);
    private readonly NumericUpDown _duelChallengerChanceControl = NewNumber(50, 1, 99);
    private readonly CheckBox _duelRequestMessagesCheck = NewCheck("Anfrage-Nachrichten", true);
    private readonly CheckBox _duelResultMessagesCheck = NewCheck("Ergebnis-Nachrichten", true);
    private readonly CheckBox _duelDenyMessagesCheck = NewCheck("Ablehnungs-Nachrichten", true);
    private readonly CheckBox _duelTimeoutMessagesCheck = NewCheck("Timeout-Nachrichten", true);
    private readonly CheckBox _duelLoserTimeoutCheck = NewCheck(
        "Verlierer automatisch timeouten", false);
    private readonly NumericUpDown _duelLoserTimeoutSecondsControl =
        NewNumber(60, 1, 1_209_600);
    private readonly TextBox _duelLoserTimeoutReasonBox = new() { Width = 360 };
    private readonly TextBox[] _duelMessageBoxes = Enumerable.Range(0, 12).Select(_ => new TextBox
        { Width = 760, Height = 52, Multiline = true, ScrollBars = ScrollBars.Vertical }).ToArray();
    private readonly Label _duelStateLabel = new() { Text = "● Inaktiv", AutoSize = true,
        ForeColor = InactiveColor, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
    private readonly Label _duelOpenLabel = new() { Text = "Offene Anfragen: 0", AutoSize = true };
    private readonly Label _duelDetailsLabel = new() { Text = "Kein Duel aktiv.", AutoSize = true, MaximumSize = new Size(900, 0) };
    private readonly Button _duelSaveButton = NewHeistActionButton("Einstellungen speichern", 210);
    private readonly Button _duelDefaultsButton = NewHeistActionButton("Standardwerte wiederherstellen", 240);
    private readonly Button _duelCancelButton = NewHeistActionButton("Offene Duels abbrechen", 200);
    private readonly Button _duelTestButton = NewHeistActionButton("Test-Duel ausführen", 180);


    private Control BuildDuelSettingsPanel()
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(new Label { Text = "Zuschauer treten um Punkte gegeneinander an",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = MutedTextColor,
            Padding = new Padding(10, 0, 0, 0) }, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill };


        var general = CreateMinigameFlow();
        general.Controls.AddRange(new Control[] { _duelEnabledCheck,
            CreateSettingEditor("Duel-Command", _duelCommandBox),
            CreateSettingEditor("Accept-Command", _duelAcceptCommandBox),
            CreateSettingEditor("Deny-Command", _duelDenyCommandBox),
            CreateSettingEditor("Mindesteinsatz", _duelMinimumBetControl),
            CreateSettingEditor("Maximaleinsatz", _duelMaximumBetControl),
            CreateSettingEditor("Anfrage-Timeout (Sek.)", _duelTimeoutControl),
            _duelAllInCheck,
            CreateSettingEditor("Benutzer-Cooldown (Sek.)", _duelUserCooldownControl),
            CreateSettingEditor("Globaler Cooldown (Sek.)", _duelGlobalCooldownControl),
            _duelSaveButton, _duelDefaultsButton });


        var rules = CreateMinigameFlow();
        rules.Controls.AddRange(new Control[] { _duelEveryoneCheck, _duelFollowersCheck,
            _duelSubscribersCheck, _duelVipsCheck, _duelModeratorsCheck, _duelFairModeCheck,
            CreateSettingEditor("Gewinnchance Herausforderer (%)", _duelChallengerChanceControl),
            _duelLoserTimeoutCheck,
            CreateSettingEditor("Timeout für Verlierer (Sek.)", _duelLoserTimeoutSecondsControl),
            CreateSettingEditor("Timeout-Grund", _duelLoserTimeoutReasonBox) });
        rules.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = MutedTextColor,
            Text = "Im fairen Modus haben beide Teilnehmer eine Chance von 50 Prozent." });


        var messages = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8) };
        messages.Controls.AddRange(new Control[] { _duelRequestMessagesCheck, _duelResultMessagesCheck,
            _duelDenyMessagesCheck, _duelTimeoutMessagesCheck });
        var labels = new[] { "Anfrage", "Angenommen", "Gewinner", "Abgelehnt", "Timeout",
            "Zu wenig Punkte (Herausforderer)", "Zu wenig Punkte (Ziel)", "Selbstduell",
            "Keine offene Anfrage", "Falscher Benutzer", "Bereits offene Anfrage", "Ungültiger Einsatz" };
        for (var i = 0; i < labels.Length; i++) messages.Controls.Add(CreateSettingEditor(labels[i], _duelMessageBoxes[i]));
        messages.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = MutedTextColor,
            Text = "Platzhalter: {user}, {challenger}, {target}, {winner}, {loser}, {amount}, {pot}, {currencyName}, {seconds}, {duelCommand}, {acceptCommand}, {denyCommand}, {winChance}" });


        var live = CreateMinigameFlow();
        live.Controls.AddRange(new Control[] { _duelStateLabel, _duelOpenLabel, _duelDetailsLabel,
            _duelCancelButton, _duelTestButton });
        AddMinigameTab(tabs, "Allgemein & Cooldowns", general);
        AddMinigameTab(tabs, "Berechtigungen & Gewinnchance", rules);
        AddMinigameTab(tabs, "Chatnachrichten", messages);
        AddMinigameTab(tabs, "Live-Status", live);
        outer.Controls.Add(tabs, 0, 1);
        return outer;
    }


    private void InitializeDuelEvents()
    {
        _duelSaveButton.Click += (_, _) => SaveDuelSettingsFromControls();
        _duelDefaultsButton.Click += (_, _) => LoadDuelSettings(new DuelConfig());
        _duelCancelButton.Click += async (_, _) =>
        {
            if (_minigame is null) AppendLog("Duel-Abbruch benötigt eine aktive Plugin-Verbindung.");
            else await _minigame.CancelDuelsAsync(_shutdown?.Token ?? CancellationToken.None);
        };
        _duelTestButton.Click += async (_, _) =>
        {
            if (_minigame is null) AppendLog("Test-Duel benötigt eine aktive Plugin-Verbindung.");
            else await _minigame.RunTestDuelAsync(_shutdown?.Token ?? CancellationToken.None);
        };
        _duelFairModeCheck.CheckedChanged += (_, _) =>
            _duelChallengerChanceControl.Enabled = !_duelFairModeCheck.Checked;
        _duelLoserTimeoutCheck.CheckedChanged += (_, _) =>
        {
            _duelLoserTimeoutSecondsControl.Enabled = _duelLoserTimeoutCheck.Checked;
            _duelLoserTimeoutReasonBox.Enabled = _duelLoserTimeoutCheck.Checked;
        };
    }


    private static string UseDuelText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }


    private void LoadDuelSettings(DuelConfig duel)
    {
        var defaults = new DuelConfig();
        _duelEnabledCheck.Checked = duel.Enabled;
        _duelCommandBox.Text = UseDuelText(duel.DuelCommand, defaults.DuelCommand);
        _duelAcceptCommandBox.Text = UseDuelText(duel.AcceptCommand, defaults.AcceptCommand);
        _duelDenyCommandBox.Text = UseDuelText(duel.DenyCommand, defaults.DenyCommand);
        SetNumericValue(_duelMinimumBetControl, duel.MinimumBet);
        SetNumericValue(_duelMaximumBetControl, duel.MaximumBet);
        SetNumericValue(_duelTimeoutControl, duel.RequestTimeoutSeconds);
        SetNumericValue(_duelUserCooldownControl, duel.UserCooldownSeconds);
        SetNumericValue(_duelGlobalCooldownControl, duel.GlobalCooldownSeconds);
        _duelAllInCheck.Checked = duel.AllowAllIn;
        _duelEveryoneCheck.Checked = duel.AllowEveryone;
        _duelFollowersCheck.Checked = duel.AllowFollowers;
        _duelSubscribersCheck.Checked = duel.AllowSubscribers;
        _duelVipsCheck.Checked = duel.AllowVips;
        _duelModeratorsCheck.Checked = duel.AllowModerators;
        _duelFairModeCheck.Checked = duel.FairMode;
        SetNumericValue(_duelChallengerChanceControl, duel.ChallengerWinChancePercent);
        _duelChallengerChanceControl.Enabled = !duel.FairMode;
        _duelRequestMessagesCheck.Checked = duel.SendRequestMessage;
        _duelResultMessagesCheck.Checked = duel.SendResultMessage;
        _duelDenyMessagesCheck.Checked = duel.SendDenyMessage;
        _duelTimeoutMessagesCheck.Checked = duel.SendTimeoutMessage;
        _duelLoserTimeoutCheck.Checked = duel.TimeoutLoserEnabled;
        SetNumericValue(_duelLoserTimeoutSecondsControl, duel.LoserTimeoutSeconds);
        _duelLoserTimeoutReasonBox.Text = UseDuelText(duel.LoserTimeoutReason, defaults.LoserTimeoutReason);
        _duelLoserTimeoutSecondsControl.Enabled = duel.TimeoutLoserEnabled;
        _duelLoserTimeoutReasonBox.Enabled = duel.TimeoutLoserEnabled;
        var values = new[]
        {
            UseDuelText(duel.DuelRequestMessage, defaults.DuelRequestMessage),
            UseDuelText(duel.DuelAcceptedMessage, defaults.DuelAcceptedMessage),
            UseDuelText(duel.DuelWinMessage, defaults.DuelWinMessage),
            UseDuelText(duel.DuelDeniedMessage, defaults.DuelDeniedMessage),
            UseDuelText(duel.DuelTimeoutMessage, defaults.DuelTimeoutMessage),
            UseDuelText(duel.NotEnoughPointsChallengerMessage, defaults.NotEnoughPointsChallengerMessage),
            UseDuelText(duel.NotEnoughPointsTargetMessage, defaults.NotEnoughPointsTargetMessage),
            UseDuelText(duel.SelfDuelMessage, defaults.SelfDuelMessage),
            UseDuelText(duel.NoPendingDuelMessage, defaults.NoPendingDuelMessage),
            UseDuelText(duel.WrongTargetMessage, defaults.WrongTargetMessage),
            UseDuelText(duel.AlreadyPendingDuelMessage, defaults.AlreadyPendingDuelMessage),
            UseDuelText(duel.InvalidBetMessage, defaults.InvalidBetMessage)
        };
        for (var i = 0; i < values.Length; i++) _duelMessageBoxes[i].Text = values[i];
    }


    private void ReadDuelSettings(AppConfig config)
    {
        var defaults = new DuelConfig();
        var duel = config.Duel;
        duel.Enabled = _duelEnabledCheck.Checked;
        duel.DuelCommand = UseDuelText(_duelCommandBox.Text, defaults.DuelCommand);
        duel.AcceptCommand = UseDuelText(_duelAcceptCommandBox.Text, defaults.AcceptCommand);
        duel.DenyCommand = UseDuelText(_duelDenyCommandBox.Text, defaults.DenyCommand);
        duel.MinimumBet = decimal.ToInt64(_duelMinimumBetControl.Value);
        duel.MaximumBet = decimal.ToInt64(_duelMaximumBetControl.Value);
        duel.RequestTimeoutSeconds = (int)_duelTimeoutControl.Value;
        duel.UserCooldownSeconds = (int)_duelUserCooldownControl.Value;
        duel.GlobalCooldownSeconds = (int)_duelGlobalCooldownControl.Value;
        duel.AllowAllIn = _duelAllInCheck.Checked;
        duel.AllowEveryone = _duelEveryoneCheck.Checked;
        duel.AllowFollowers = _duelFollowersCheck.Checked;
        duel.AllowSubscribers = _duelSubscribersCheck.Checked;
        duel.AllowVips = _duelVipsCheck.Checked;
        duel.AllowModerators = _duelModeratorsCheck.Checked;
        duel.FairMode = _duelFairModeCheck.Checked;
        duel.ChallengerWinChancePercent = (int)_duelChallengerChanceControl.Value;
        duel.SendRequestMessage = _duelRequestMessagesCheck.Checked;
        duel.SendResultMessage = _duelResultMessagesCheck.Checked;
        duel.SendDenyMessage = _duelDenyMessagesCheck.Checked;
        duel.SendTimeoutMessage = _duelTimeoutMessagesCheck.Checked;
        duel.TimeoutLoserEnabled = _duelLoserTimeoutCheck.Checked;
        duel.LoserTimeoutSeconds = (int)_duelLoserTimeoutSecondsControl.Value;
        duel.LoserTimeoutReason = UseDuelText(_duelLoserTimeoutReasonBox.Text, defaults.LoserTimeoutReason);
        duel.DuelRequestMessage = UseDuelText(_duelMessageBoxes[0].Text, defaults.DuelRequestMessage);
        duel.DuelAcceptedMessage = UseDuelText(_duelMessageBoxes[1].Text, defaults.DuelAcceptedMessage);
        duel.DuelWinMessage = UseDuelText(_duelMessageBoxes[2].Text, defaults.DuelWinMessage);
        duel.DuelDeniedMessage = UseDuelText(_duelMessageBoxes[3].Text, defaults.DuelDeniedMessage);
        duel.DuelTimeoutMessage = UseDuelText(_duelMessageBoxes[4].Text, defaults.DuelTimeoutMessage);
        duel.NotEnoughPointsChallengerMessage = UseDuelText(_duelMessageBoxes[5].Text, defaults.NotEnoughPointsChallengerMessage);
        duel.NotEnoughPointsTargetMessage = UseDuelText(_duelMessageBoxes[6].Text, defaults.NotEnoughPointsTargetMessage);
        duel.SelfDuelMessage = UseDuelText(_duelMessageBoxes[7].Text, defaults.SelfDuelMessage);
        duel.NoPendingDuelMessage = UseDuelText(_duelMessageBoxes[8].Text, defaults.NoPendingDuelMessage);
        duel.WrongTargetMessage = UseDuelText(_duelMessageBoxes[9].Text, defaults.WrongTargetMessage);
        duel.AlreadyPendingDuelMessage = UseDuelText(_duelMessageBoxes[10].Text, defaults.AlreadyPendingDuelMessage);
        duel.InvalidBetMessage = UseDuelText(_duelMessageBoxes[11].Text, defaults.InvalidBetMessage);

        SyncMinigameCommandOverrides(config);
        AppendSaveDebug(
            "Duel-Werte aus GUI gelesen: Aktiv=" + duel.Enabled +
            ", Command=" + duel.DuelCommand +
            ", Accept=" + duel.AcceptCommand +
            ", Deny=" + duel.DenyCommand +
            ", Min=" + duel.MinimumBet +
            ", Max=" + duel.MaximumBet +
            ", CommandDuel=" + config.Commands.IsCommandEnabled("duel.challenge"));
    }



    private static void VerifyDuelSettingsPersisted(DuelConfig expected, DuelConfig actual)
    {
        if (expected.Enabled != actual.Enabled ||
            !string.Equals(expected.DuelCommand, actual.DuelCommand, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(expected.AcceptCommand, actual.AcceptCommand, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(expected.DenyCommand, actual.DenyCommand, StringComparison.OrdinalIgnoreCase) ||
            expected.MinimumBet != actual.MinimumBet ||
            expected.MaximumBet != actual.MaximumBet ||
            expected.RequestTimeoutSeconds != actual.RequestTimeoutSeconds ||
            expected.UserCooldownSeconds != actual.UserCooldownSeconds ||
            expected.GlobalCooldownSeconds != actual.GlobalCooldownSeconds ||
            expected.AllowAllIn != actual.AllowAllIn ||
            expected.AllowEveryone != actual.AllowEveryone ||
            expected.AllowFollowers != actual.AllowFollowers ||
            expected.AllowSubscribers != actual.AllowSubscribers ||
            expected.AllowVips != actual.AllowVips ||
            expected.AllowModerators != actual.AllowModerators ||
            expected.FairMode != actual.FairMode ||
            expected.ChallengerWinChancePercent != actual.ChallengerWinChancePercent ||
            expected.SendRequestMessage != actual.SendRequestMessage ||
            expected.SendResultMessage != actual.SendResultMessage ||
            expected.SendDenyMessage != actual.SendDenyMessage ||
            expected.SendTimeoutMessage != actual.SendTimeoutMessage ||
            expected.TimeoutLoserEnabled != actual.TimeoutLoserEnabled ||
            expected.LoserTimeoutSeconds != actual.LoserTimeoutSeconds ||
            !string.Equals(expected.LoserTimeoutReason, actual.LoserTimeoutReason, StringComparison.Ordinal) ||
            !string.Equals(expected.DuelRequestMessage, actual.DuelRequestMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.DuelAcceptedMessage, actual.DuelAcceptedMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.DuelWinMessage, actual.DuelWinMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.DuelDeniedMessage, actual.DuelDeniedMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.DuelTimeoutMessage, actual.DuelTimeoutMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.NotEnoughPointsChallengerMessage, actual.NotEnoughPointsChallengerMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.NotEnoughPointsTargetMessage, actual.NotEnoughPointsTargetMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.SelfDuelMessage, actual.SelfDuelMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.NoPendingDuelMessage, actual.NoPendingDuelMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.WrongTargetMessage, actual.WrongTargetMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.AlreadyPendingDuelMessage, actual.AlreadyPendingDuelMessage, StringComparison.Ordinal) ||
            !string.Equals(expected.InvalidBetMessage, actual.InvalidBetMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Duel-Einstellungen wurden geschrieben, aber beim Nachladen nicht bestätigt.");
        }
    }

    private void SaveDuelSettingsFromControls()
    {
        AppendSaveDebug("Duel-Speichern geklickt.");
        if (_settingsSaveBusy)
        {
            AppendLog("Speichern läuft bereits. Bitte kurz warten …");
            return;
        }

        _settingsSaveBusy = true;
        SetSettingsControlsEnabled(false);

        try
        {
            AppendSaveDebug("Duel-Speichern startet.");
            var config = _configurationService.LoadForEditing();
            ReadDuelSettings(config);
            _configurationService.SaveGuiSettings(config);

            var verified = _configurationService.LoadForEditing();
            VerifyDuelSettingsPersisted(config.Duel, verified.Duel);
            AppendSaveDebug(
                "Duel-Nachladeprüfung erfolgreich: Aktiv=" + verified.Duel.Enabled +
                ", Command=" + verified.Duel.DuelCommand +
                ", Accept=" + verified.Duel.AcceptCommand +
                ", Deny=" + verified.Duel.DenyCommand +
                ", WinTextLen=" + (verified.Duel.DuelWinMessage?.Length ?? 0));

            ApplyRuntimeSettings(verified);
            SetMinigameStatus("Duel-Einstellungen gespeichert", ActiveColor);
            SetOverallStatus("Einstellungen gespeichert", ActiveColor);
            _commandRegistry.Update(verified);
            RefreshCommandGrid();
        }
        catch (Exception exception)
        {
            AppendSaveDebug("Duel-Einstellungen konnten nicht gespeichert werden: " + exception.Message);
            SetOverallStatus("Einstellungsfehler", ErrorColor);
            SetMinigameStatus("Duel-Fehler: " + exception.Message, ErrorColor);
            ShowSection("minigame");
        }
        finally
        {
            SetSettingsControlsEnabled(true);
            _settingsSaveBusy = false;
        }
    }

    private void OnDuelStatusChanged(DuelStatus status)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnDuelStatusChanged(status))); return; }
        _duelStateLabel.Text = status.TestMode ? "● Testmodus" : status.State switch
        {
            DuelState.Waiting => "● Wartet", DuelState.Accepted => "● Angenommen",
            DuelState.Denied => "● Abgelehnt", DuelState.Expired => "● Abgelaufen",
            DuelState.Paid => "● Ausgezahlt", DuelState.Cancelled => "● Abgebrochen", _ => "● Inaktiv"
        };
        _duelStateLabel.ForeColor = status.State == DuelState.Paid ? ActiveColor :
            status.State is DuelState.Denied or DuelState.Expired or DuelState.Cancelled ? ErrorColor : WaitingColor;
        _duelOpenLabel.Text = $"Offene Anfragen: {status.OpenRequests}";
        _duelDetailsLabel.Text = string.IsNullOrWhiteSpace(status.Challenger)
            ? "Kein Duel aktiv."
            : $"Herausforderer: {status.Challenger} · Ziel: {status.Target} · Einsatz: {status.Stake:N0} · Restzeit: {status.SecondsRemaining}s · Status: {status.State}";
        _duelCancelButton.Enabled = status.OpenRequests > 0;
    }
}
