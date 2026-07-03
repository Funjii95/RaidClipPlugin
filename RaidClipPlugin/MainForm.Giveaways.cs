using System.Text;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Button _giveawayNavButton = CreateNavigationTile(
        "🎁  Giveaways", "Verlosungen und Gewinner");
    private readonly Panel _giveawayPage = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _giveawayStatusLabel = new()
    {
        Text = "● Giveaway: Deaktiviert", AutoSize = true,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = InactiveColor, Padding = new Padding(4)
    };
    private readonly Label _giveawayRuntimeLabel = new()
    {
        Text = "Kein Giveaway gestartet", AutoSize = true,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold), Margin = new Padding(8)
    };
    private readonly CheckBox _giveawayEnabledCheck = NewCheck("Giveaway-Modul aktivieren", false);
    private readonly TextBox _giveawayTitleBox = new() { Text = "Community Giveaway", Width = 260 };
    private readonly TextBox _giveawayDescriptionBox = new() { Width = 360, Multiline = true, Height = 54 };
    private readonly TextBox _giveawayPrizeBox = new() { Text = "Überraschung", Width = 260 };
    private readonly TextBox _giveawayCommandBox = new() { Text = "!giveaway", Width = 150 };
    private readonly TextBox _giveawayAliasesBox = new() { Text = "!join", Width = 220 };
    private readonly NumericUpDown _giveawayDuration = CreateIntegerControl(10, 1, 10080);
    private readonly NumericUpDown _giveawayMaxWinners = CreateIntegerControl(1, 1, 100);
    private readonly CheckBox _giveawayPreventDuplicates = NewCheck("Doppelte Teilnahme verhindern", true);
    private readonly CheckBox _giveawayPreventReentry = NewCheck("Erneute Teilnahme verhindern", true);
    private readonly CheckBox _giveawayAutoDraw = NewCheck("Nach Ablauf automatisch auslosen", true);
    private readonly CheckBox _giveawayLiveOnly = NewCheck("Nur erlauben, wenn der Stream live ist", true);
    private readonly CheckBox _giveawayAnnounceWinners = NewCheck("Gewinner im Chat ankündigen", true);
    private readonly CheckBox _giveawayAnnounceCount = NewCheck("Teilnehmerzahl regelmäßig ankündigen", false);
    private readonly NumericUpDown _giveawayCountInterval = CreateIntegerControl(5, 1, 1440);
    private readonly CheckBox _giveawayShowList = NewCheck("Teilnehmerliste anzeigen", true);
    private readonly CheckBox _giveawayAutoClose = NewCheck("Nach Auslosung automatisch schließen", true);

    private readonly CheckBox _giveawayEveryone = NewCheck("Alle Zuschauer", true);
    private readonly CheckBox _giveawayFollowers = NewCheck("Follower", true);
    private readonly CheckBox _giveawaySubscribers = NewCheck("Abonnenten", true);
    private readonly CheckBox _giveawayVips = NewCheck("VIPs", true);
    private readonly CheckBox _giveawayModerators = NewCheck("Moderatoren", true);
    private readonly CheckBox _giveawayBroadcaster = NewCheck("Broadcaster", false);
    private readonly NumericUpDown _giveawayFollowMinutes = CreateIntegerControl(0, 0, 10000000);
    private readonly NumericUpDown _giveawayMinimumPoints = CreateIntegerControl(0, 0, 1000000000);
    private readonly NumericUpDown _giveawayEntryCost = CreateIntegerControl(0, 0, 1000000000);
    private readonly CheckBox _giveawayDeductPoints = NewCheck("Punkte bei Teilnahme abziehen", true);
    private readonly CheckBox _giveawayRefund = NewCheck("Bei Abbruch zurückerstatten", true);
    private readonly TextBox _giveawayAllowlistBox = new() { Width = 320, Multiline = true, Height = 55 };
    private readonly TextBox _giveawayBlocklistBox = new() { Width = 320, Multiline = true, Height = 55 };
    private readonly CheckBox _giveawayExcludeBots = NewCheck("Bots ausschließen", true);
    private readonly CheckBox _giveawayExcludeBroadcaster = NewCheck("Broadcaster bei Auslosung ausschließen", true);

    private readonly CheckBox _giveawayPreviousWinners = NewCheck("Frühere Gewinner erneut zulassen", false);
    private readonly CheckBox _giveawaySubWeight = NewCheck("Abonnenten: doppelte Chance", false);
    private readonly CheckBox _giveawayVipWeight = NewCheck("VIPs: erhöhte Chance", false);
    private readonly NumericUpDown _giveawayVipMultiplier = CreateIntegerControl(2, 1, 100);
    private readonly CheckBox _giveawayExtraTickets = NewCheck("Zusatzlose über Punkte erlauben", false);
    private readonly NumericUpDown _giveawayTicketCost = CreateIntegerControl(100, 0, 1000000000);
    private readonly NumericUpDown _giveawayMaxTickets = CreateIntegerControl(5, 0, 100);
    private readonly CheckBox _giveawayModCommands = NewCheck("Moderator-Commands aktivieren", true);
    private readonly TextBox _giveawayAdminCommandsBox = new()
    {
        Width = 520, Multiline = true, Height = 130,
        Text = "!giveaway start\r\n!giveaway stop\r\n!giveaway pause\r\n!giveaway resume\r\n!giveaway draw\r\n!giveaway reroll\r\n!giveaway status"
    };
    private readonly TextBox _giveawayMessagesBox = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9F),
        Text = "Started|1|🎉 Giveaway gestartet! Gewinn: {prize} – Teilnahme mit {command}\r\n" +
               "Joined|1|@{username}, du nimmst am Giveaway teil!\r\n" +
               "Duplicate|1|@{username}, du bist bereits im Giveaway eingetragen.\r\n" +
               "InsufficientPoints|1|@{username}, du benötigst mindestens {requiredPoints} Punkte.\r\n" +
               "Excluded|1|@{username}, du darfst an diesem Giveaway nicht teilnehmen.\r\n" +
               "Ended|1|Das Giveaway ist beendet. Insgesamt haben {participantCount} Zuschauer teilgenommen.\r\n" +
               "Winner|1|🎉 Gewinner des Giveaways ist @{winner}! Herzlichen Glückwunsch zu {prize}!\r\n" +
               "Winners|1|🎉 Die Gewinner sind: {winners}\r\n" +
               "Status|1|Giveaway {title}: {participantCount} Teilnehmer, noch {remainingTime}."
    };

    private readonly Button _giveawaySaveButton = NewActionButton("Giveaway speichern");
    private readonly Button _giveawayStartButton = NewActionButton("Starten");
    private readonly Button _giveawayPauseButton = NewActionButton("Pausieren");
    private readonly Button _giveawayResumeButton = NewActionButton("Fortsetzen");
    private readonly Button _giveawayDrawButton = NewActionButton("Auslosen");
    private readonly Button _giveawayAdditionalButton = NewActionButton("Weiteren Gewinner ziehen");
    private readonly Button _giveawayRerollButton = NewActionButton("Neu auslosen");
    private readonly Button _giveawayEndButton = NewActionButton("Beenden");
    private readonly Button _giveawayCancelButton = NewActionButton("Abbrechen");
    private readonly Button _giveawayResetButton = NewActionButton("Teilnehmer zurücksetzen");
    private readonly Button _giveawayCopyButton = NewActionButton("Gewinner kopieren");
    private readonly Button _giveawayExportButton = NewActionButton("CSV exportieren");
    private readonly Button _giveawayRefreshButton = NewActionButton("Aktualisieren");
    private readonly TextBox _giveawayManualUserBox = new() { Width = 180, PlaceholderText = "Twitch-Nutzer" };
    private readonly Button _giveawayAddUserButton = NewActionButton("Hinzufügen");
    private readonly Button _giveawayRemoveUserButton = NewActionButton("Entfernen");
    private readonly TextBox _giveawaySearchBox = new() { Width = 180, PlaceholderText = "Teilnehmer suchen" };
    private readonly DataGridView _giveawayGrid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoGenerateColumns = false
    };

    private GiveawayService? _giveawayService;
    private Task? _giveawayTask;
    private GiveawayRuntimeState _giveawayState = new();

    private void InitializeGiveawayEvents()
    {
        _giveawayNavButton.Click += (_, _) => ShowSection("giveaways");
        _giveawaySaveButton.Click += (_, _) => SaveGiveawaySettings();
        _giveawayStartButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.StartAsync(CurrentToken()));
        _giveawayPauseButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.PauseAsync(CurrentToken()));
        _giveawayResumeButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.ResumeAsync(CurrentToken()));
        _giveawayDrawButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.DrawConfiguredAsync(CurrentToken()));
        _giveawayAdditionalButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.DrawAdditionalAsync(CurrentToken()));
        _giveawayRerollButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.RerollAsync(CurrentToken()));
        _giveawayEndButton.Click += async (_, _) => await RunGiveawayActionAsync(s => s.EndAsync(CurrentToken()));
        _giveawayCancelButton.Click += async (_, _) =>
        {
            if (MessageBox.Show("Giveaway abbrechen und ggf. Punkte erstatten?", "Giveaway abbrechen",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                await RunGiveawayActionAsync(s => s.CancelAsync(CurrentToken()));
        };
        _giveawayResetButton.Click += async (_, _) =>
        {
            if (MessageBox.Show("Alle Teilnehmer dieses Giveaways entfernen?", "Teilnehmer zurücksetzen",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                await RunGiveawayActionAsync(s => s.ResetParticipantsAsync(CurrentToken()));
        };
        _giveawayCopyButton.Click += (_, _) =>
        {
            var names = string.Join(", ", _giveawayState.Winners.Select(x => x.DisplayName));
            if (names.Length > 0) Clipboard.SetText(names);
        };
        _giveawayRefreshButton.Click += async (_, _) => await RefreshGiveawayAsync();
        _giveawaySearchBox.TextChanged += (_, _) => RefreshGiveawayGrid();
        _giveawayAddUserButton.Click += async (_, _) => await AddGiveawayUserAsync();
        _giveawayRemoveUserButton.Click += async (_, _) => await RemoveGiveawayUserAsync();
        _giveawayExportButton.Click += (_, _) => ExportGiveawayCsv();
    }

    private CancellationToken CurrentToken() => _shutdown?.Token ?? CancellationToken.None;

    private void BuildGiveawayPage()
    {
        var title = new Label { Text = "Giveaways", AutoSize = true,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold), ForeColor = Color.White };
        var subtitle = new Label { Text = "Verlosungen sicher verwalten, auslosen und wiederherstellen",
            AutoSize = true, ForeColor = MutedTextColor };
        var headerText = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        headerText.Controls.Add(title); headerText.Controls.Add(subtitle);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(headerText, 0, 0); header.Controls.Add(_giveawayStatusLabel, 1, 0);

        var general = GiveawayFlow();
        foreach (var control in new Control[] {
            _giveawayEnabledCheck, Editor("Titel", _giveawayTitleBox), Editor("Beschreibung", _giveawayDescriptionBox),
            Editor("Gewinn", _giveawayPrizeBox), Editor("Teilnahme-Command", _giveawayCommandBox),
            Editor("Aliase (Komma)", _giveawayAliasesBox), Editor("Dauer (Min.)", _giveawayDuration),
            Editor("Max. Gewinner", _giveawayMaxWinners), _giveawayPreventDuplicates, _giveawayPreventReentry,
            _giveawayAutoDraw, _giveawayLiveOnly, _giveawayAnnounceWinners, _giveawayAnnounceCount,
            Editor("Ankündigungsintervall", _giveawayCountInterval), _giveawayShowList, _giveawayAutoClose })
            general.Controls.Add(control);

        var eligibility = GiveawayFlow();
        foreach (var control in new Control[] {
            _giveawayEveryone, _giveawayFollowers, _giveawaySubscribers, _giveawayVips,
            _giveawayModerators, _giveawayBroadcaster, Editor("Min. Followdauer (Min.)", _giveawayFollowMinutes),
            Editor("Mindestpunkte", _giveawayMinimumPoints), Editor("Teilnahmekosten", _giveawayEntryCost),
            _giveawayDeductPoints, _giveawayRefund, Editor("Whitelist", _giveawayAllowlistBox),
            Editor("Blacklist", _giveawayBlocklistBox), _giveawayExcludeBots, _giveawayExcludeBroadcaster }) eligibility.Controls.Add(control);

        var chance = GiveawayFlow();
        foreach (var control in new Control[] { _giveawayPreviousWinners, _giveawaySubWeight,
            _giveawayVipWeight, Editor("VIP-Multiplikator", _giveawayVipMultiplier), _giveawayExtraTickets,
            Editor("Punkte je Zusatzlos", _giveawayTicketCost), Editor("Max. Zusatzlose", _giveawayMaxTickets),
            _giveawayModCommands, Editor("Admin-Commands (7 Zeilen)", _giveawayAdminCommandsBox) })
            chance.Controls.Add(control);

        var messages = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        messages.Controls.Add(_giveawayMessagesBox);
        var messageHint = new Label { Dock = DockStyle.Top, Height = 42, ForeColor = MutedTextColor,
            Text = "Format: Name|1 oder 0|Chattext. Platzhalter: {username}, {title}, {prize}, {command}, {participantCount}, {winner}, {winners}, {remainingTime}, {requiredPoints}" };
        messages.Controls.Add(messageHint);

        var settingsTabs = new TabControl { Dock = DockStyle.Fill };
        AddMinigameTab(settingsTabs, "Allgemein", general);
        AddMinigameTab(settingsTabs, "Teilnahme", eligibility);
        AddMinigameTab(settingsTabs, "Gewinnchancen & Admin", chance);
        AddMinigameTab(settingsTabs, "Chattexte", messages);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true };
        foreach (var control in new Control[] { _giveawayStartButton, _giveawayPauseButton, _giveawayResumeButton,
            _giveawayDrawButton, _giveawayAdditionalButton, _giveawayRerollButton, _giveawayEndButton,
            _giveawayCancelButton, _giveawayResetButton, _giveawayCopyButton, _giveawaySaveButton,
            _giveawayRuntimeLabel }) actions.Controls.Add(control);

        _giveawayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name="User", HeaderText="Nutzer", Width=180 });
        _giveawayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name="Role", HeaderText="Rolle", Width=110 });
        _giveawayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name="Joined", HeaderText="Teilgenommen", Width=145 });
        _giveawayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name="Points", HeaderText="Punkte", Width=80 });
        _giveawayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name="Tickets", HeaderText="Lose", Width=70 });
        _giveawayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name="Status", HeaderText="Status", AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill });
        var participantTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        foreach (var control in new Control[] { _giveawayManualUserBox, _giveawayAddUserButton,
            _giveawayRemoveUserButton, _giveawaySearchBox, _giveawayRefreshButton, _giveawayExportButton })
            participantTools.Controls.Add(control);
        var participantPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        participantPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        participantPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        participantPanel.Controls.Add(participantTools, 0, 0); participantPanel.Controls.Add(_giveawayGrid, 0, 1);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(20) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(settingsTabs, 0, 1);
        layout.Controls.Add(actions, 0, 2); layout.Controls.Add(participantPanel, 0, 3);
        _giveawayPage.Controls.Add(layout);
    }

    private static FlowLayoutPanel GiveawayFlow() => new()
    { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(8) };
    private static Control Editor(string label, Control control) => CreateSettingEditor(label, control);

    private void LoadGiveawaySettings(GiveawayConfig g)
    {
        _giveawayEnabledCheck.Checked=g.Enabled; _giveawayTitleBox.Text=g.Title;
        _giveawayDescriptionBox.Text=g.Description; _giveawayPrizeBox.Text=g.Prize;
        _giveawayCommandBox.Text=g.Command; _giveawayAliasesBox.Text=string.Join(", ",g.Aliases);
        SetNumericValue(_giveawayDuration,g.DurationMinutes); SetNumericValue(_giveawayMaxWinners,g.MaximumWinners);
        _giveawayPreventDuplicates.Checked=g.PreventDuplicateEntries; _giveawayPreventReentry.Checked=g.PreventReentryAfterLeaving;
        _giveawayAutoDraw.Checked=g.AutoDrawWhenExpired; _giveawayLiveOnly.Checked=g.LiveOnly;
        _giveawayAnnounceWinners.Checked=g.AnnounceWinners; _giveawayAnnounceCount.Checked=g.AnnounceParticipantCount;
        SetNumericValue(_giveawayCountInterval,g.ParticipantCountIntervalMinutes); _giveawayShowList.Checked=g.ShowParticipantList;
        _giveawayAutoClose.Checked=g.AutoCloseAfterDraw; _giveawayEveryone.Checked=g.AllowedRoles.Everyone;
        _giveawayFollowers.Checked=g.AllowedRoles.Followers; _giveawaySubscribers.Checked=g.AllowedRoles.Subscribers;
        _giveawayVips.Checked=g.AllowedRoles.Vips; _giveawayModerators.Checked=g.AllowedRoles.Moderators;
        _giveawayBroadcaster.Checked=g.AllowedRoles.Broadcaster; SetNumericValue(_giveawayFollowMinutes,g.MinimumFollowMinutes);
        SetNumericValue(_giveawayMinimumPoints,g.MinimumPoints); SetNumericValue(_giveawayEntryCost,g.EntryCost);
        _giveawayDeductPoints.Checked=g.DeductPointsAtJoin; _giveawayRefund.Checked=g.RefundPointsOnCancel;
        _giveawayAllowlistBox.Text=string.Join(", ",g.AllowedUsers); _giveawayBlocklistBox.Text=string.Join(", ",g.BlockedUsers);
        _giveawayExcludeBots.Checked=g.ExcludeBots; _giveawayExcludeBroadcaster.Checked=g.ExcludeBroadcasterFromDraw;
        _giveawayPreviousWinners.Checked=g.AllowPreviousWinners;
        _giveawaySubWeight.Checked=g.SubscriberDoubleChance; _giveawayVipWeight.Checked=g.VipIncreasedChance;
        SetNumericValue(_giveawayVipMultiplier,g.VipTicketMultiplier); _giveawayExtraTickets.Checked=g.ExtraTicketsEnabled;
        SetNumericValue(_giveawayTicketCost,g.ExtraTicketCost); SetNumericValue(_giveawayMaxTickets,g.MaximumExtraTickets);
        _giveawayModCommands.Checked=g.ModeratorCommands.Enabled;
        _giveawayAdminCommandsBox.Lines=new[]{g.ModeratorCommands.Start,g.ModeratorCommands.Stop,g.ModeratorCommands.Pause,
            g.ModeratorCommands.Resume,g.ModeratorCommands.Draw,g.ModeratorCommands.Reroll,g.ModeratorCommands.Status};
        _giveawayMessagesBox.Lines=GiveawayMessageLines(g.ChatMessages);
    }

    private GiveawayConfig ReadGiveawaySettings()
    {
        var g=new GiveawayConfig { Enabled=_giveawayEnabledCheck.Checked, Title=_giveawayTitleBox.Text.Trim(),
            Description=_giveawayDescriptionBox.Text.Trim(), Prize=_giveawayPrizeBox.Text.Trim(),
            Command=_giveawayCommandBox.Text.Trim(), Aliases=ParseNames(_giveawayAliasesBox.Text,false),
            DurationMinutes=(int)_giveawayDuration.Value, MaximumWinners=(int)_giveawayMaxWinners.Value,
            PreventDuplicateEntries=_giveawayPreventDuplicates.Checked, PreventReentryAfterLeaving=_giveawayPreventReentry.Checked,
            AutoDrawWhenExpired=_giveawayAutoDraw.Checked, LiveOnly=_giveawayLiveOnly.Checked,
            AnnounceWinners=_giveawayAnnounceWinners.Checked, AnnounceParticipantCount=_giveawayAnnounceCount.Checked,
            ParticipantCountIntervalMinutes=(int)_giveawayCountInterval.Value, ShowParticipantList=_giveawayShowList.Checked,
            AutoCloseAfterDraw=_giveawayAutoClose.Checked, MinimumFollowMinutes=(int)_giveawayFollowMinutes.Value,
            MinimumPoints=(int)_giveawayMinimumPoints.Value, EntryCost=(int)_giveawayEntryCost.Value,
            DeductPointsAtJoin=_giveawayDeductPoints.Checked, RefundPointsOnCancel=_giveawayRefund.Checked,
            AllowedUsers=ParseNames(_giveawayAllowlistBox.Text,true), BlockedUsers=ParseNames(_giveawayBlocklistBox.Text,true),
            ExcludeBots=_giveawayExcludeBots.Checked, ExcludeBroadcasterFromDraw=_giveawayExcludeBroadcaster.Checked,
            AllowPreviousWinners=_giveawayPreviousWinners.Checked,
            SubscriberDoubleChance=_giveawaySubWeight.Checked, VipIncreasedChance=_giveawayVipWeight.Checked,
            VipTicketMultiplier=(int)_giveawayVipMultiplier.Value, ExtraTicketsEnabled=_giveawayExtraTickets.Checked,
            ExtraTicketCost=(int)_giveawayTicketCost.Value, MaximumExtraTickets=(int)_giveawayMaxTickets.Value };
        g.AllowedRoles=new GiveawayAllowedRoles { Everyone=_giveawayEveryone.Checked, Followers=_giveawayFollowers.Checked,
            Subscribers=_giveawaySubscribers.Checked, Vips=_giveawayVips.Checked, Moderators=_giveawayModerators.Checked,
            Broadcaster=_giveawayBroadcaster.Checked };
        var cmd=_giveawayAdminCommandsBox.Lines.Select(x=>x.Trim()).Where(x=>x.Length>0).ToArray();
        if(cmd.Length!=7) throw new InvalidOperationException("Bitte genau sieben Giveaway-Admin-Commands eintragen.");
        g.ModeratorCommands=new GiveawayModeratorCommands { Enabled=_giveawayModCommands.Checked, Start=cmd[0],Stop=cmd[1],
            Pause=cmd[2],Resume=cmd[3],Draw=cmd[4],Reroll=cmd[5],Status=cmd[6] };
        g.ChatMessages=ReadGiveawayMessages(); return g;
    }

    private static List<string> ParseNames(string text,bool names) => text.Split(new[]{',',';','\r','\n'},
        StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
        .Select(x=>names?x.TrimStart('@').ToLowerInvariant():x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void SaveGiveawaySettings()
    {
        try { var config=ReadSettingsFromControls(); config.Giveaways=ReadGiveawaySettings();
            ConfigurationService.ValidateGiveawaySettings(config.Giveaways);
            _configurationService.SaveGuiSettings(config); ApplyRuntimeSettings(config);
            AppendLog("Giveaway-Einstellungen wurden gespeichert.");
            SetGiveawayStatus(config.Giveaways.Enabled?"Gespeichert":"Deaktiviert", config.Giveaways.Enabled?ActiveColor:InactiveColor); }
        catch(Exception ex) { AppendLog("Giveaway-Einstellungen konnten nicht gespeichert werden: "+ex.Message);
            SetGiveawayStatus("Einstellungen ungültig",ErrorColor); }
    }

    private async Task StartGiveawayModuleAsync(AppConfig config,TwitchSession session,TwitchService twitch,
        TwitchUser broadcaster,CancellationToken token)
    {
        if(!config.Giveaways.Enabled){SetGiveawayStatus("Deaktiviert",InactiveColor);return;}
        _giveawayService=new GiveawayService(broadcaster.Id,session.UserId,config.Giveaways,config.Minigame,
            twitch,twitch,_viewerPoints,new GiveawayStore());
        _giveawayService.StateChanged += state => UpdateGiveawayState(state);
        await _giveawayService.InitializeAsync(token);
        _giveawayTask=_giveawayService.RunAsync(token); ObserveGiveawayTask(_giveawayTask);
        SetGiveawayStatus("Aktiv",ActiveColor);
    }

    private async void ObserveGiveawayTask(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) when (_shutdown?.IsCancellationRequested == true) { }
        catch (Exception exception)
        {
            AppendLog("Giveaway-Modul wurde beendet: " + exception.Message);
            SetGiveawayStatus("Fehler", ErrorColor);
        }
    }

    private void StopGiveawayModule(){_giveawayService?.Dispose();_giveawayService=null;_giveawayTask=null;
        SetGiveawayStatus("Deaktiviert",InactiveColor);}

    private async Task RunGiveawayActionAsync(Func<GiveawayService,Task<GiveawayActionResult>> action)
    { if(_giveawayService is null){AppendLog("Giveaway-Modul ist nicht gestartet.");return;}
      try{var result=await action(_giveawayService);if(!result.Success)AppendLog("Giveaway: "+result.Error);else await RefreshGiveawayAsync();}
      catch(Exception ex){AppendLog("Giveaway-Aktion fehlgeschlagen: "+ex.Message);} }

    private async Task RefreshGiveawayAsync(){if(_giveawayService is null)return;
        UpdateGiveawayState(await _giveawayService.GetStateAsync(CurrentToken()));}
    private void UpdateGiveawayState(GiveawayRuntimeState state){if(InvokeRequired){BeginInvoke(new Action(()=>UpdateGiveawayState(state)));return;}
        _giveawayState=state; _giveawayRuntimeLabel.Text=$"{state.Status} · {state.Participants.Count} Teilnehmer · {state.Winners.Count} Gewinner · {GiveawayService.RemainingTime(state)}";
        RefreshGiveawayGrid();}
    private void RefreshGiveawayGrid(){if(InvokeRequired){BeginInvoke(new Action(RefreshGiveawayGrid));return;}
        var q=_giveawaySearchBox.Text.Trim();_giveawayGrid.Rows.Clear();
        if(!_giveawayShowList.Checked)return;foreach(var p in _giveawayState.Participants.Where(p=>q.Length==0||p.DisplayName.Contains(q,StringComparison.OrdinalIgnoreCase)||p.UserLogin.Contains(q,StringComparison.OrdinalIgnoreCase))){
            var i=_giveawayGrid.Rows.Add(p.DisplayName,p.Role,p.JoinedAtUtc.ToLocalTime().ToString("dd.MM. HH:mm"),p.PointsUsed,1+p.ExtraTickets,p.IsValid?"Gültig":p.InvalidReason);
            _giveawayGrid.Rows[i].Tag=p;}}
    private async Task AddGiveawayUserAsync(){if(_giveawayService is null)return;var login=_giveawayManualUserBox.Text.Trim().TrimStart('@');
        if(login.Length==0)return;await RunGiveawayActionAsync(s=>s.AddParticipantManuallyAsync(login,CurrentToken()));_giveawayManualUserBox.Clear();}
    private async Task RemoveGiveawayUserAsync(){if(_giveawayService is null||_giveawayGrid.CurrentRow?.Tag is not GiveawayParticipant p)return;
        await RunGiveawayActionAsync(s=>s.RemoveParticipantAsync(p.UserId,CurrentToken()));}
    private void ExportGiveawayCsv(){using var dialog=new SaveFileDialog{Filter="CSV-Datei|*.csv",FileName=$"giveaway-{DateTime.Now:yyyyMMdd-HHmm}.csv"};if(dialog.ShowDialog()!=DialogResult.OK)return;
        var b=new StringBuilder("UserId;Login;Anzeigename;Rolle;Teilnahme;Punkte;Lose;Status\r\n");foreach(var p in _giveawayState.Participants)
            b.AppendLine(string.Join(';',p.UserId,p.UserLogin,p.DisplayName,p.Role,p.JoinedAtUtc.ToString("O"),p.PointsUsed,1+p.ExtraTickets,p.IsValid?"Gültig":p.InvalidReason).Replace("\r","").Replace("\n"," "));
        File.WriteAllText(dialog.FileName,b.ToString(),Encoding.UTF8);AppendLog("Giveaway-Teilnehmer wurden als CSV exportiert.");}
    private void SetGiveawayStatus(string text,Color color){if(InvokeRequired){BeginInvoke(new Action(()=>SetGiveawayStatus(text,color)));return;}
        _giveawayStatusLabel.Text="● Giveaway: "+text;_giveawayStatusLabel.ForeColor=color;}

    private static string[] GiveawayMessageLines(GiveawayChatMessages m)=>new[]{Line("Started",m.Started),Line("Joined",m.Joined),Line("Duplicate",m.Duplicate),
        Line("InsufficientPoints",m.InsufficientPoints),Line("Excluded",m.Excluded),Line("Ended",m.Ended),Line("Winner",m.Winner),Line("Winners",m.Winners),
        Line("Status",m.Status),Line("Paused",m.Paused),Line("Resumed",m.Resumed),Line("Cancelled",m.Cancelled),Line("Offline",m.Offline),Line("NotActive",m.NotActive)};
    private static string Line(string name,GiveawayChatMessage m)=>$"{name}|{(m.Enabled?1:0)}|{m.Text}";
    private GiveawayChatMessages ReadGiveawayMessages(){var map=_giveawayMessagesBox.Lines.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Split('|',3))
        .Where(x=>x.Length==3).ToDictionary(x=>x[0].Trim(),x=>new GiveawayChatMessage(x[1].Trim()!="0",x[2].Trim()),StringComparer.OrdinalIgnoreCase);
        GiveawayChatMessage M(string k,GiveawayChatMessage d)=>map.TryGetValue(k,out var x)?x:d;var d=new GiveawayChatMessages();return new GiveawayChatMessages{
            Started=M("Started",d.Started),Joined=M("Joined",d.Joined),Duplicate=M("Duplicate",d.Duplicate),InsufficientPoints=M("InsufficientPoints",d.InsufficientPoints),
            Excluded=M("Excluded",d.Excluded),Ended=M("Ended",d.Ended),Winner=M("Winner",d.Winner),Winners=M("Winners",d.Winners),Status=M("Status",d.Status),
            Paused=M("Paused",d.Paused),Resumed=M("Resumed",d.Resumed),Cancelled=M("Cancelled",d.Cancelled),Offline=M("Offline",d.Offline),NotActive=M("NotActive",d.NotActive)};}
}
