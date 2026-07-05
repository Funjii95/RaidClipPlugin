using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;


namespace RaidClipPlugin;


public sealed partial class MainForm
{
    private readonly CheckBox _customCommandsEnabledCheck =
        NewCheck("Custom Commands aktivieren", true);
    private readonly CheckBox _ignoreSharedChatCommandsCheck =
        NewCheck("Commands nur im aktuellen Kanal ausführen", true);
    private readonly Label _sharedChatCommandsHint = new()
    {
        AutoSize = true,
        MaximumSize = new Size(520, 0),
        ForeColor = MutedTextColor,
        Text = "Verhindert Commands aus fremden Stream-Together-/Shared-Chat-Kanälen."
    };
    private readonly DataGridView _customCommandsGrid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };
    private readonly Button _addCustomCommandButton =
        NewHeistActionButton("Command hinzufügen", 180);
    private readonly Button _removeCustomCommandButton =
        NewHeistActionButton("Auswahl entfernen", 180);
    private readonly Button _saveCustomCommandsButton =
        NewHeistActionButton("Custom Commands speichern", 220);
    private readonly Button _saveCommandRolesButton =
        NewHeistActionButton("Berechtigungen speichern", 210);
    private readonly Button _resetCommandRoleButton =
        NewHeistActionButton("Berechtigung zurücksetzen", 220);
    private readonly Dictionary<string, string> _pendingCommandRoleOverrides =
        new(StringComparer.OrdinalIgnoreCase);
    private CommandPermissionService? _commandPermissions;
    private CustomCommandService? _customCommandService;


    private Control BuildCustomCommandsEditorPanel()
    {
        ConfigureCustomCommandsGrid();
        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            ForeColor = MutedTextColor,
            Text = "Eigene Chatbefehle mit frei wählbarer Rolle. Platzhalter: " +
                   "{user}, {login}, {args}, {target}, {command}. Das !raid-Beispiel ist zunächst deaktiviert."
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
            Padding = new Padding(4)
        };
        actions.Controls.AddRange(new Control[]
        {
            _customCommandsEnabledCheck, _ignoreSharedChatCommandsCheck,
            _sharedChatCommandsHint, _addCustomCommandButton,
            _removeCustomCommandButton, _saveCustomCommandsButton
        });
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        layout.Controls.Add(_customCommandsGrid, 0, 2);
        return layout;
    }


    private void ConfigureCustomCommandsGrid()
    {
        if (_customCommandsGrid.Columns.Count > 0) return;
        _customCommandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Id", Visible = false });
        _customCommandsGrid.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "Enabled", HeaderText = "Aktiv", Width = 55 });
        _customCommandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Command", HeaderText = "Command", Width = 130 });
        _customCommandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Aliases", HeaderText = "Aliase", Width = 160 });
        _customCommandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Response", HeaderText = "Chatantwort", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        var role = new DataGridViewComboBoxColumn
        {
            Name = "Role", HeaderText = "Berechtigung", Width = 125,
            FlatStyle = FlatStyle.Flat
        };
        role.Items.AddRange(CommandRoleLabels.Cast<object>().ToArray());
        _customCommandsGrid.Columns.Add(role);
        _customCommandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "UserCooldown", HeaderText = "Nutzer-CD", Width = 90 });
        _customCommandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "GlobalCooldown", HeaderText = "Global-CD", Width = 90 });
    }


    private Control BuildCommandOverviewPanel(Control filters)
    {
        var permissions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true
        };
        permissions.Controls.AddRange(new Control[]
            { filters, _saveCommandRolesButton, _resetCommandRoleButton });
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(permissions, 0, 0);
        layout.Controls.Add(_commandsGrid, 0, 1);
        return layout;
    }


    private void InitializeCustomCommandEvents()
    {
        InitializeCustomCommandImportEvents();
        _addCustomCommandButton.Click += (_, _) => AddCustomCommandRow();
        _removeCustomCommandButton.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _customCommandsGrid.SelectedRows)
                if (!row.IsNewRow) _customCommandsGrid.Rows.Remove(row);
        };
        _saveCustomCommandsButton.Click += (_, _) => SaveSettingsFromControls();
        _saveCommandRolesButton.Click += (_, _) => SaveSettingsFromControls();
        _resetCommandRoleButton.Click += (_, _) => ResetSelectedCommandRole();
        _commandsGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_commandsGrid.IsCurrentCellDirty)
                _commandsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _commandsGrid.CellValueChanged += (_, args) =>
        {
            if (_refreshingCommandGrid || args.RowIndex < 0 ||
                _commandsGrid.Columns[args.ColumnIndex].Name != "Berechtigung") return;
            var row = _commandsGrid.Rows[args.RowIndex];
            if (row.Tag is ChatCommandDefinition definition)
                _pendingCommandRoleOverrides[definition.CommandId] =
                    ParseRoleLabel(row.Cells["Berechtigung"].Value?.ToString()).ToString();
        };
    }


    private void AddCustomCommandRow(CustomChatCommandConfig? item = null)
    {
        item ??= new CustomChatCommandConfig
        {
            Enabled = true,
            Command = "!command",
            Response = "@{user}, hier steht deine Chatantwort."
        };
        var index = _customCommandsGrid.Rows.Add(item.Id, item.Enabled,
            item.Command, string.Join(", ", item.Aliases), item.Response,
            RoleLabel(CommandRegistry.ParseRole(item.RequiredRole)),
            item.UserCooldownSeconds, item.GlobalCooldownSeconds);
        _customCommandsGrid.Rows[index].Selected = true;
    }


    private void LoadCustomCommandSettings(CommandsConfig commands)
    {
        _customCommandsEnabledCheck.Checked = commands.CustomCommandsEnabled;
        _ignoreSharedChatCommandsCheck.Checked = commands.IgnoreSharedChatOrigins;
        _customCommandsGrid.Rows.Clear();
        foreach (var item in commands.CustomCommands) AddCustomCommandRow(item);
        _pendingCommandRoleOverrides.Clear();
        foreach (var item in commands.CommandRoleOverrides)
            _pendingCommandRoleOverrides[item.Key] = item.Value;
    }


    private void ReadCustomCommandSettings(CommandsConfig commands)
    {
        commands.CustomCommandsEnabled = _customCommandsEnabledCheck.Checked;
        commands.IgnoreSharedChatOrigins = _ignoreSharedChatCommandsCheck.Checked;
        commands.CustomCommands = _customCommandsGrid.Rows.Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => new CustomChatCommandConfig
            {
                Id = CellText(row, "Id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N"),
                Enabled = row.Cells["Enabled"].Value is true,
                Command = CellText(row, "Command"),
                Aliases = SplitCommandValues(CellText(row, "Aliases")),
                Response = CellText(row, "Response"),
                RequiredRole = ParseRoleLabel(CellText(row, "Role")).ToString(),
                UserCooldownSeconds = ParseCooldown(row, "UserCooldown"),
                GlobalCooldownSeconds = ParseCooldown(row, "GlobalCooldown")
            }).ToList();
        commands.CommandRoleOverrides = new Dictionary<string, string>(
            _pendingCommandRoleOverrides, StringComparer.OrdinalIgnoreCase);
    }


    private void ResetSelectedCommandRole()
    {
        if (_commandsGrid.SelectedRows.Count != 1 ||
            _commandsGrid.SelectedRows[0].Tag is not ChatCommandDefinition definition)
            return;
        _pendingCommandRoleOverrides.Remove(definition.CommandId);
        SaveSettingsFromControls();
    }


    private static readonly string[] CommandRoleLabels =
        { "Zuschauer", "Follower", "Subscriber", "VIP", "Moderator", "Broadcaster" };


    private static string RoleLabel(CommandRole role) => role switch
    {
        CommandRole.Viewer => "Zuschauer",
        CommandRole.Vip => "VIP",
        _ => role.ToString()
    };


    private static CommandRole ParseRoleLabel(string? value) =>
        (value ?? "").Trim() switch
        {
            "Zuschauer" => CommandRole.Viewer,
            "VIP" => CommandRole.Vip,
            var text => CommandRegistry.ParseRole(text)
        };


    private static int ParseCooldown(DataGridViewRow row, string column) =>
        int.TryParse(CellText(row, column), out var value) ? value : -1;


    private static string CellText(DataGridViewRow row, string column) =>
        row.Cells[column].Value?.ToString()?.Trim() ?? "";


    private static List<string> SplitCommandValues(string text) =>
        text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim()).Where(value => value.Length > 0).ToList();


    private void StartCustomCommandServices(AppConfig config,
        TwitchService twitch, TwitchSession session, TwitchUser broadcaster,
        CancellationToken cancellationToken)
    {
        _commandPermissions = new CommandPermissionService(broadcaster.Id,
            session.UserId, config.Commands, twitch, twitch, _commandRegistry);
        _customCommandService = new CustomCommandService(broadcaster.Id,
            session.UserId, config.Commands, twitch, twitch);
        _chatModeration!.MessageAuthorizing += message =>
            _commandPermissions.AuthorizeAsync(message, cancellationToken);
        _chatModeration.MessageReceived += message =>
            _customCommandService.HandleMessageAsync(message, cancellationToken);
    }


    private void StopCustomCommandServices()
    {
        _commandPermissions = null;
        _customCommandService = null;
    }
}