using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly CommandRegistry _commandRegistry = new();
    private readonly Panel _commandsPage = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Button _commandsNavButton = CreateNavigationTile("◉  Punkte", "Commands und Berechtigungen");
    private readonly DataGridView _commandsGrid = new() { Dock = DockStyle.Fill, ReadOnly = false, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, BackgroundColor = SurfaceColor };
    private readonly TextBox _commandSearchBox = new() { Width = 240, PlaceholderText = "Command oder Beschreibung suchen" };
    private readonly ComboBox _commandModuleFilter = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _commandActiveFilter = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _commandRoleFilter = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _copyCommandButton = NewHeistActionButton("Command kopieren", 160);
    private readonly Button _copyAllCommandsButton = NewHeistActionButton("Alle Commands kopieren", 190);
    private readonly Button _exportCommandsButton = NewHeistActionButton("Command-Liste exportieren", 210);
    private bool _refreshingCommandGrid;

    private readonly CheckBox _heistEnabledCheck = NewCheck("Heist aktivieren", false);
    private readonly TextBox _heistStartCommandBox = new() { Width = 140 };
    private readonly TextBox _heistJoinCommandBox = new() { Width = 140 };
    private readonly NumericUpDown _heistMinimumControl = NewNumber(3, 3, 500);
    private readonly NumericUpDown _heistMaximumControl = NewNumber(50, 3, 500);
    private readonly NumericUpDown _heistDurationControl = NewNumber(60, 10, 300);
    private readonly NumericUpDown _heistChanceControl = NewNumber(50, 0, 100);
    private readonly NumericUpDown _heistGlobalCooldownControl = NewNumber(10, 0, 10080);
    private readonly NumericUpDown _heistUserCooldownControl = NewNumber(30, 0, 10080);
    private readonly CheckBox _heistCancelledCooldownCheck = NewCheck("Cooldown auch bei zu wenigen Teilnehmern", false);
    private readonly CheckBox _heistEveryoneCheck = NewCheck("Alle Zuschauer", true);
    private readonly CheckBox _heistFollowersCheck = NewCheck("Follower", true);
    private readonly CheckBox _heistSubscribersCheck = NewCheck("Subscriber", true);
    private readonly CheckBox _heistVipsCheck = NewCheck("VIPs", true);
    private readonly CheckBox _heistModeratorsCheck = NewCheck("Moderatoren", true);
    private readonly CheckBox _heistJoinMessagesCheck = NewCheck("Beitrittsnachrichten", true);
    private readonly CheckBox _heistCountdownMessagesCheck = NewCheck("Countdown-Nachrichten", false);
    private readonly CheckBox _heistResultMessagesCheck = NewCheck("Ergebnisnachrichten", true);
    private readonly CheckBox _heistResetJackpotCheck = NewCheck("Jackpot nach erfolgreichem Heist zurücksetzen", true);
    private readonly TextBox[] _heistMessageBoxes = Enumerable.Range(0, 9).Select(_ => new TextBox
        { Width = 760, Height = 52, Multiline = true, ScrollBars = ScrollBars.Vertical }).ToArray();
    private readonly Label _heistStateLabel = new() { Text = "● Inaktiv", AutoSize = true, ForeColor = InactiveColor,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
    private readonly Label _heistDetailsLabel = new() { Text = "Kein Heist aktiv.", AutoSize = true, MaximumSize = new Size(900, 0) };
    private readonly ListBox _heistParticipantList = new() { Width = 420, Height = 220 };
    private readonly Button _heistSaveButton = NewHeistActionButton("Einstellungen speichern", 210);
    private readonly Button _heistDefaultsButton = NewHeistActionButton("Standardwerte wiederherstellen", 240);
    private readonly Button _heistTestButton = NewHeistActionButton("Test-Heist starten", 170);
    private readonly Button _heistCancelButton = NewHeistActionButton("Laufenden Heist abbrechen", 210);
    private readonly Label _heistJackpotLabel = new() { Text = "Aktueller Jackpot: –", AutoSize = true };

    private static Button NewHeistActionButton(string text, int width)
    {
        var button = NewActionButton(text);
        button.Width = width;
        return button;
    }

    private static NumericUpDown NewNumber(long value, long minimum, long maximum) => new()
    {
        Minimum = minimum, Maximum = maximum, Value = value, Width = 120,
        ThousandsSeparator = true, Margin = new Padding(5, 2, 5, 2)
    };

    private Control BuildHeistSettingsPanel()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var general = CreateMinigameFlow();
        general.Controls.Add(_heistEnabledCheck);
        general.Controls.Add(CreateSettingEditor("Start-Command", _heistStartCommandBox));
        general.Controls.Add(CreateSettingEditor("Beitritts-Command", _heistJoinCommandBox));
        general.Controls.Add(CreateSettingEditor("Mindestteilnehmer", _heistMinimumControl));
        general.Controls.Add(CreateSettingEditor("Maximalteilnehmer", _heistMaximumControl));
        general.Controls.Add(CreateSettingEditor("Beitrittsphase (Sek.)", _heistDurationControl));
        general.Controls.Add(CreateSettingEditor("Erfolgschance (%)", _heistChanceControl));
        general.Controls.Add(CreateSettingEditor("Globaler Cooldown (Min.)", _heistGlobalCooldownControl));
        general.Controls.Add(CreateSettingEditor("Benutzer-Cooldown (Min.)", _heistUserCooldownControl));
        general.Controls.Add(_heistCancelledCooldownCheck);
        general.Controls.Add(_heistSaveButton);
        general.Controls.Add(_heistDefaultsButton);

        var permissions = CreateMinigameFlow();
        permissions.Controls.AddRange(new Control[] { _heistEveryoneCheck, _heistFollowersCheck, _heistSubscribersCheck,
            _heistVipsCheck, _heistModeratorsCheck, _heistResetJackpotCheck });
        permissions.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(900, 0),
            Text = "Bei einem erfolgreichen Heist wird der vollständige Jackpot unter allen Teilnehmern aufgeteilt." });
        permissions.Controls.Add(_heistJackpotLabel);

        var messages = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(8) };
        messages.Controls.AddRange(new Control[] { _heistJoinMessagesCheck, _heistCountdownMessagesCheck, _heistResultMessagesCheck });
        var labels = new[] { "Start", "Beitritt", "Bereits dabei", "Kein aktiver Heist", "Teilnehmerliste voll",
            "Zu wenige Teilnehmer", "Auswertung", "Erfolg", "Fehlschlag" };
        for (var i = 0; i < labels.Length; i++) messages.Controls.Add(CreateSettingEditor(labels[i], _heistMessageBoxes[i]));
        messages.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = MutedTextColor,
            Text = "Platzhalter: {user}, {seconds}, {minimum}, {maximum}, {current}, {count}, {jackpot}, {share}, {currencyName}, {startCommand}, {joinCommand}, {successChance}" });

        var live = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(12) };
        live.Controls.Add(_heistStateLabel); live.Controls.Add(_heistDetailsLabel); live.Controls.Add(_heistParticipantList);
        live.Controls.Add(_heistTestButton); live.Controls.Add(_heistCancelButton);
        AddMinigameTab(tabs, "Allgemein", general);
        AddMinigameTab(tabs, "Berechtigungen & Jackpot", permissions);
        AddMinigameTab(tabs, "Chatnachrichten", messages);
        AddMinigameTab(tabs, "Live-Status", live);
        return tabs;
    }

    private void BuildCommandsPage()
    {
        var header = new Label { Text = "Commands", Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            ForeColor = TextColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var subtitle = new Label { Text = "Chat-Befehle, Custom Commands und frei wählbare Berechtigungen",
            AutoSize = true, ForeColor = MutedTextColor };
        var filters = new FlowLayoutPanel { AutoSize = true, AutoScroll = true,
            WrapContents = true, Padding = new Padding(0, 4, 0, 4) };
        _commandActiveFilter.Items.AddRange(new object[] { "Alle", "Aktiv", "Inaktiv" }); _commandActiveFilter.SelectedIndex = 0;
        _commandRoleFilter.Items.AddRange(new object[] { "Alle Rollen", "Zuschauer", "Follower", "Subscriber", "VIP", "Moderator", "Broadcaster" }); _commandRoleFilter.SelectedIndex = 0;
        _commandModuleFilter.Items.Add("Alle Module"); _commandModuleFilter.SelectedIndex = 0;
        filters.Controls.AddRange(new Control[] { _commandSearchBox, _commandModuleFilter, _commandActiveFilter,
            _commandRoleFilter, _copyCommandButton, _copyAllCommandsButton, _exportCommandsButton });
        _commandsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Aktiv", HeaderText = "Aktiv", ReadOnly = false,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        foreach (var column in new[] { "Command", "Aliase", "Modul", "Beschreibung", "Verwendung" })
            _commandsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = column, HeaderText = column, ReadOnly = true });
        var roleColumn = new DataGridViewComboBoxColumn
        {
            Name = "Berechtigung", HeaderText = "Berechtigung", ReadOnly = false,
            Width = 130, FlatStyle = FlatStyle.Flat
        };
        roleColumn.Items.AddRange(CommandRoleLabels.Cast<object>().ToArray());
        _commandsGrid.Columns.Add(roleColumn);
        foreach (var column in new[] { "Benutzer-Cooldown", "Globaler Cooldown", "Punktekosten", "Beispiel" })
            _commandsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { Name = column, HeaderText = column, ReadOnly = true });
        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddMinigameTab(tabs, "Übersicht & Rechte", BuildCommandOverviewPanel(filters));
        AddMinigameTab(tabs, "Custom Commands", BuildCustomCommandsPanel());
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3,
            ColumnCount = 1, Padding = new Padding(20) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(tabs, 0, 2);
        _commandsPage.Controls.Add(layout);
        try
        {
            _commandRegistry.Update(new AppConfig());
            RefreshCommandGrid();
        }
        catch (Exception exception)
        {
            AppendLog("Command-Liste konnte nicht mit Standardwerten vorbereitet werden: " + exception.Message);
        }
    }

    private void InitializeHeistCommandEvents()
    {
        _commandsNavButton.Click += (_, _) => ShowSection("commands");
        _heistSaveButton.Click += (_, _) => SaveMinigameSettingsFromControls();
        _heistDefaultsButton.Click += (_, _) => LoadHeistSettings(new HeistConfig());
        _heistTestButton.Click += async (_, _) => { if (_minigame is null) AppendLog("Test-Heist benötigt eine aktive Plugin-Verbindung."); else await _minigame.RunTestHeistAsync(_shutdown?.Token ?? CancellationToken.None); };
        _heistCancelButton.Click += async (_, _) => { if (_minigame is not null) await _minigame.CancelHeistAsync(_shutdown?.Token ?? CancellationToken.None); };
        _commandSearchBox.TextChanged += (_, _) => RefreshCommandGrid();
        _commandModuleFilter.SelectedIndexChanged += (_, _) => RefreshCommandGrid();
        _commandActiveFilter.SelectedIndexChanged += (_, _) => RefreshCommandGrid();
        _commandRoleFilter.SelectedIndexChanged += (_, _) => RefreshCommandGrid();
        _copyCommandButton.Click += (_, _) => CopySelectedCommand();
        _copyAllCommandsButton.Click += (_, _) => CopyAllCommands();
        _exportCommandsButton.Click += async (_, _) => await ExportCommandsAsync();
        _commandRegistry.Changed += RefreshCommandGrid;
        InitializeCustomCommandEvents();
        _heistCancelButton.Enabled = false;
    }

    private void LoadHeistCommandsSettings(AppConfig config)
    {
        LoadHeistSettings(config.Heist);
        LoadCustomCommandSettings(config.Commands);
        _commandRegistry.Update(config);
        RefreshCommandGrid();
    }

    private void LoadHeistSettings(HeistConfig h)
    {
        _heistEnabledCheck.Checked = h.Enabled; _heistStartCommandBox.Text = h.StartCommand; _heistJoinCommandBox.Text = h.JoinCommand;
        SetNumericValue(_heistMinimumControl, h.MinimumParticipants); SetNumericValue(_heistMaximumControl, h.MaximumParticipants);
        SetNumericValue(_heistDurationControl, h.JoinDurationSeconds); SetNumericValue(_heistChanceControl, h.SuccessChancePercent);
        SetNumericValue(_heistGlobalCooldownControl, h.GlobalCooldownMinutes); SetNumericValue(_heistUserCooldownControl, h.UserCooldownMinutes);
        _heistCancelledCooldownCheck.Checked = h.ApplyGlobalCooldownOnCancelledHeist; _heistEveryoneCheck.Checked = h.AllowEveryone;
        _heistFollowersCheck.Checked = h.AllowFollowers; _heistSubscribersCheck.Checked = h.AllowSubscribers;
        _heistVipsCheck.Checked = h.AllowVips; _heistModeratorsCheck.Checked = h.AllowModerators;
        _heistJoinMessagesCheck.Checked = h.SendParticipantJoinMessages; _heistCountdownMessagesCheck.Checked = h.SendCountdownMessages;
        _heistResultMessagesCheck.Checked = h.SendResultMessages; _heistResetJackpotCheck.Checked = h.ResetJackpotAfterSuccess;
        var defaults = new HeistConfig();
        var values = new[] { HeistTextOrDefault(h.StartMessage,defaults.StartMessage),HeistTextOrDefault(h.JoinMessage,defaults.JoinMessage),HeistTextOrDefault(h.AlreadyJoinedMessage,defaults.AlreadyJoinedMessage),HeistTextOrDefault(h.NoActiveHeistMessage,defaults.NoActiveHeistMessage),HeistTextOrDefault(h.MaximumParticipantsMessage,defaults.MaximumParticipantsMessage),
            HeistTextOrDefault(h.NotEnoughParticipantsMessage,defaults.NotEnoughParticipantsMessage),HeistTextOrDefault(h.EvaluationMessage,defaults.EvaluationMessage),HeistTextOrDefault(h.SuccessMessage,defaults.SuccessMessage),HeistTextOrDefault(h.FailureMessage,defaults.FailureMessage) };
        for(var i=0;i<values.Length;i++)_heistMessageBoxes[i].Text=values[i];
    }

    private static string ReadHeistMessage(TextBox box, string fallback) =>
        string.IsNullOrWhiteSpace(box.Text) ? fallback : box.Text.Trim();


    private static string HeistTextOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();


    private void ReadHeistCommandsSettings(AppConfig config)
    {
        var h=config.Heist; h.Enabled=_heistEnabledCheck.Checked; h.StartCommand=_heistStartCommandBox.Text;
        h.JoinCommand=_heistJoinCommandBox.Text; h.MinimumParticipants=(int)_heistMinimumControl.Value;
        h.MaximumParticipants=(int)_heistMaximumControl.Value; h.JoinDurationSeconds=(int)_heistDurationControl.Value;
        h.SuccessChancePercent=(int)_heistChanceControl.Value; h.GlobalCooldownMinutes=(int)_heistGlobalCooldownControl.Value;
        h.UserCooldownMinutes=(int)_heistUserCooldownControl.Value; h.ApplyGlobalCooldownOnCancelledHeist=_heistCancelledCooldownCheck.Checked;
        h.AllowEveryone=_heistEveryoneCheck.Checked; h.AllowFollowers=_heistFollowersCheck.Checked; h.AllowSubscribers=_heistSubscribersCheck.Checked;
        h.AllowVips=_heistVipsCheck.Checked; h.AllowModerators=_heistModeratorsCheck.Checked;
        h.SendParticipantJoinMessages=_heistJoinMessagesCheck.Checked; h.SendCountdownMessages=_heistCountdownMessagesCheck.Checked;
        h.SendResultMessages=_heistResultMessagesCheck.Checked; h.ResetJackpotAfterSuccess=_heistResetJackpotCheck.Checked;
        var defaults = new HeistConfig();
        h.StartMessage=ReadHeistMessage(_heistMessageBoxes[0], defaults.StartMessage); h.JoinMessage=ReadHeistMessage(_heistMessageBoxes[1], defaults.JoinMessage); h.AlreadyJoinedMessage=ReadHeistMessage(_heistMessageBoxes[2], defaults.AlreadyJoinedMessage);
        h.NoActiveHeistMessage=ReadHeistMessage(_heistMessageBoxes[3], defaults.NoActiveHeistMessage); h.MaximumParticipantsMessage=ReadHeistMessage(_heistMessageBoxes[4], defaults.MaximumParticipantsMessage);
        h.NotEnoughParticipantsMessage=ReadHeistMessage(_heistMessageBoxes[5], defaults.NotEnoughParticipantsMessage); h.EvaluationMessage=ReadHeistMessage(_heistMessageBoxes[6], defaults.EvaluationMessage);
        h.SuccessMessage=ReadHeistMessage(_heistMessageBoxes[7], defaults.SuccessMessage); h.FailureMessage=ReadHeistMessage(_heistMessageBoxes[8], defaults.FailureMessage);
        ReadCustomCommandSettings(config.Commands);
    }

    private void OnHeistStatusChanged(HeistStatus status)
    {
        if(InvokeRequired){BeginInvoke(new Action(()=>OnHeistStatusChanged(status)));return;}
        _heistStateLabel.Text=status.State switch { HeistState.Joining=>"● Beitrittsphase",HeistState.Evaluating=>"● Auswertung",
            HeistState.Successful=>"● Erfolgreich",HeistState.Failed=>"● Fehlgeschlagen",HeistState.Cancelled=>"● Abgebrochen",_=>"● Inaktiv" };
        _heistStateLabel.ForeColor=status.State==HeistState.Successful?ActiveColor:status.State is HeistState.Failed or HeistState.Cancelled?ErrorColor:WaitingColor;
        _heistDetailsLabel.Text=$"Ersteller: {status.Creator} · Teilnehmer: {status.ParticipantCount} · Restzeit: {status.SecondsRemaining}s · Erfolgschance: {status.SuccessChancePercent}%";
        _heistJackpotLabel.Text=$"Aktueller Jackpot: {status.Jackpot:N0}"; _heistParticipantList.Items.Clear();
        _heistParticipantList.Items.AddRange(status.Participants.Cast<object>().ToArray());
        _heistCancelButton.Enabled=status.State is HeistState.Joining or HeistState.Evaluating;
    }

    private void RefreshCommandGrid()
    {
        if (IsDisposed || _refreshingCommandGrid) return;
        if (InvokeRequired) { BeginInvoke(new Action(RefreshCommandGrid)); return; }

        _refreshingCommandGrid = true;
        try
        {
            var commands = _commandRegistry.Commands;
            var modules = commands.Select(x => x.ModuleDisplayName).Distinct().OrderBy(x => x).ToArray();
            var selected = _commandModuleFilter.SelectedItem?.ToString() ?? "Alle Module";
            var desiredModules = new[] { "Alle Module" }.Concat(modules).ToArray();
            var currentModules = _commandModuleFilter.Items.Cast<object>().Select(x => x.ToString() ?? "").ToArray();
            if (!currentModules.SequenceEqual(desiredModules, StringComparer.Ordinal))
            {
                _commandModuleFilter.BeginUpdate();
                try
                {
                    _commandModuleFilter.Items.Clear();
                    _commandModuleFilter.Items.AddRange(desiredModules.Cast<object>().ToArray());
                    _commandModuleFilter.SelectedItem = _commandModuleFilter.Items.Contains(selected)
                        ? selected
                        : "Alle Module";
                }
                finally
                {
                    _commandModuleFilter.EndUpdate();
                }
            }

            var search = _commandSearchBox.Text.Trim();
            IEnumerable<ChatCommandDefinition> filtered = commands;
            if (search.Length > 0)
                filtered = filtered.Where(x => (x.CommandText + " " + x.Description + " " + x.ModuleDisplayName)
                    .Contains(search, StringComparison.OrdinalIgnoreCase));
            if (_commandModuleFilter.SelectedIndex > 0)
                filtered = filtered.Where(x => x.ModuleDisplayName == _commandModuleFilter.SelectedItem?.ToString());
            if (_commandActiveFilter.SelectedIndex == 1) filtered = filtered.Where(x => x.Enabled);
            else if (_commandActiveFilter.SelectedIndex == 2) filtered = filtered.Where(x => !x.Enabled);
            if (_commandRoleFilter.SelectedIndex > 0)
                filtered = filtered.Where(x => (int)x.RequiredRole == _commandRoleFilter.SelectedIndex - 1);

            var collisions = _commandRegistry.FindCollisions().Select(x => x.Command)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _commandsGrid.Rows.Clear();
            foreach (var command in filtered.OrderBy(x => x.ModuleDisplayName).ThenBy(x => x.CommandText))
            {
                var index = _commandsGrid.Rows.Add(command.Enabled, command.CommandText,
                    string.Join(", ", command.Aliases), command.ModuleDisplayName, command.Description, command.Usage,
                    RoleLabel(command.RequiredRole), command.UserCooldown.TotalSeconds + "s",
                    command.GlobalCooldown.TotalSeconds + "s", command.PointCost, command.Example);
                var row = _commandsGrid.Rows[index];
                row.Tag = command;
                if (!command.Enabled) row.DefaultCellStyle.ForeColor = MutedTextColor;
                if (collisions.Contains(command.CommandText))
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(80, 20, 20);
                    row.ErrorText = "Command-Kollision";
                }
            }
        }
        finally
        {
            _refreshingCommandGrid = false;
        }
    }

    private void CopySelectedCommand(){if(_commandsGrid.SelectedRows.Count==1&&_commandsGrid.SelectedRows[0].Tag is ChatCommandDefinition c)Clipboard.SetText(c.Usage);}
    private void CopyAllCommands(){var text=string.Join(Environment.NewLine,_commandRegistry.Commands.Where(x=>x.Enabled).Select(x=>x.Usage));if(text.Length>0)Clipboard.SetText(text);}
    private async Task ExportCommandsAsync(){using var dialog=new SaveFileDialog{Filter="Textdatei (*.txt)|*.txt|JSON (*.json)|*.json",FileName="raidclip-commands.txt"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;await _commandRegistry.ExportAsync(dialog.FileName,Path.GetExtension(dialog.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase),CancellationToken.None);AppendLog("Command-Liste exportiert: "+dialog.FileName);}
}




