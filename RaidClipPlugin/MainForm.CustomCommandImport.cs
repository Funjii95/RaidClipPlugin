using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly CustomCommandImportService _customCommandImport = new();
    private readonly TextBox _customImportTextBox = new()
    {
        Name = "CustomCommandPasteInput", Dock = DockStyle.Fill, Multiline = true,
        ScrollBars = ScrollBars.Both,
        PlaceholderText = "JSON, CSV oder Text einfügen – z. B. !hallo => Hallo @{user}!"
    };
    private readonly DataGridView _customImportGrid = new()
    {
        Name = "CustomCommandImportPreview", Dock = DockStyle.Fill,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = true,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        AllowUserToResizeRows = false,
        MinimumSize = new Size(320, 180),
        DefaultCellStyle = new DataGridViewCellStyle
        {
            WrapMode = DataGridViewTriState.False
        },
        RowTemplate = new DataGridViewRow { Height = 28, MinimumHeight = 28 }
    };
    private readonly Button _customImportFileButton =
        NewHeistActionButton("JSON/CSV auswählen", 180);
    private readonly Button _customImportPasteButton =
        NewHeistActionButton("Eingabe prüfen", 145);
    private readonly Button _customImportRecheckButton =
        NewHeistActionButton("Konflikte neu prüfen", 180);
    private readonly Button _customImportApplyButton =
        NewHeistActionButton("Import anwenden", 160);
    private readonly Button _customImportSafeButton =
        NewHeistActionButton("Nur konfliktfreie importieren", 235);
    private readonly TextBox _customImportDetailsBox = new()
    {
        Name = "CustomCommandImportDetails", Dock = DockStyle.Fill,
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        PlaceholderText = "Vollständige Antwort des ausgewählten Commands"
    };
    private readonly Label _customImportStatusLabel = new()
    {
        Name = "CustomCommandImportStatus", AutoSize = true,
        ForeColor = MutedTextColor,
        Text = "Noch keine Importdaten geprüft."
    };
    private List<CustomCommandImportCandidate> _customImportCandidates = new();
    private string _customImportSource = "Paste-Import";
    private bool _customImportPreviewValid;

    private Control BuildCustomCommandsPanel()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Name = "CustomCommandTabs" };
        var editor = new TabPage("Commands verwalten")
            { BackColor = SurfaceColor, ForeColor = TextColor };
        editor.Controls.Add(BuildCustomCommandsEditorPanel());
        var import = new TabPage("Custom Commands Import")
            { BackColor = SurfaceColor, ForeColor = TextColor };
        import.Controls.Add(BuildCustomCommandImportPanel());
        tabs.TabPages.Add(editor);
        tabs.TabPages.Add(import);
        return tabs;
    }

    private Control BuildCustomCommandImportPanel()
    {
        ConfigureCustomImportGrid();
        var hint = new Label
        {
            AutoSize = true, MaximumSize = new Size(1200, 0),
            ForeColor = MutedTextColor,
            Text = "Importiert StreamElements-ähnliche JSON-/CSV-Exporte oder eingefügten Text. " +
                   "Eingebaute Commands können niemals überschrieben werden. " +
                   "Bei Konflikten: überspringen, vorhandenen Custom Command überschreiben oder automatisch umbenennen."
        };
        var buttons = new FlowLayoutPanel
        {
            Name = "CustomCommandImportToolbar", Dock = DockStyle.Fill,
            AutoScroll = true, WrapContents = true, Padding = new Padding(3)
        };
        buttons.Controls.AddRange(new Control[]
        {
            _customImportFileButton, _customImportPasteButton,
            _customImportRecheckButton, _customImportApplyButton,
            _customImportSafeButton
        });
        var upper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(4)
        };
        upper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        upper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        upper.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        upper.Controls.Add(hint, 0, 0);
        upper.Controls.Add(_customImportTextBox, 0, 1);
        upper.Controls.Add(buttons, 0, 2);
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            Size = new Size(900, 600), SplitterDistance = 210, Panel1MinSize = 150, Panel2MinSize = 220
        };
        split.Panel1.Controls.Add(upper);
        var previewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            MinimumSize = new Size(320, 220)
        };
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        previewLayout.Controls.Add(_customImportGrid, 0, 0);
        previewLayout.Controls.Add(_customImportDetailsBox, 0, 1);
        split.Panel2.Controls.Add(previewLayout);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            Padding = new Padding(6)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(split, 0, 0);
        layout.Controls.Add(_customImportStatusLabel, 0, 1);
        return layout;
    }

    private void ConfigureCustomImportGrid()
    {
        if (_customImportGrid.Columns.Count > 0) return;
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Command", HeaderText = "Command", Width = 120 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Aliases", HeaderText = "Aliase", Width = 140 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Response", HeaderText = "Antwort/Text", Width = 300 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Cooldown", HeaderText = "Cooldown", Width = 75 });
        _customImportGrid.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "Enabled", HeaderText = "Aktiv", Width = 50 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Role", HeaderText = "Berechtigung", Width = 105 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Source", HeaderText = "Quelle", ReadOnly = true, Width = 110 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Status", HeaderText = "Importstatus", ReadOnly = true, Width = 90 });
        _customImportGrid.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Conflict", HeaderText = "Konfliktmeldung", ReadOnly = true, Width = 330 });
        var action = new DataGridViewComboBoxColumn
        {
            Name = "Action", HeaderText = "Aktion", Width = 135,
            FlatStyle = FlatStyle.Flat
        };
        action.Items.AddRange("Importieren", "Überspringen", "Überschreiben", "Umbenennen");
        _customImportGrid.Columns.Add(action);
        _customImportGrid.DataError += (_, _) => { };
        _customImportGrid.SelectionChanged += (_, _) =>
            UpdateCustomImportDetails();
    }

    private void InitializeCustomCommandImportEvents()
    {
        _customImportFileButton.Click += async (_, _) => await LoadCustomImportFileAsync();
        _customImportPasteButton.Click += (_, _) => AnalyzeCustomImport(
            _customImportTextBox.Text, "Paste-Import", CustomCommandImportFormat.Auto);
        _customImportRecheckButton.Click += (_, _) => RecheckCustomImport();
        _customImportApplyButton.Click += (_, _) => ApplyCustomImport(false);
        _customImportSafeButton.Click += (_, _) => ApplyCustomImport(true);
    }

    private async Task LoadCustomImportFileAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Custom Commands importieren",
            Filter = "Command-Dateien (*.json;*.csv;*.txt)|*.json;*.csv;*.txt|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var content = await File.ReadAllTextAsync(dialog.FileName);
            _customImportTextBox.Text = content;
            AnalyzeCustomImport(content, Path.GetFileName(dialog.FileName),
                Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? CustomCommandImportFormat.Json
                    : Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                        ? CustomCommandImportFormat.Csv : CustomCommandImportFormat.Auto);
        }
        catch (Exception exception)
        {
            ShowCustomImportError("Datei konnte nicht gelesen werden: " + exception.Message);
        }
    }

    private void AnalyzeCustomImport(string content, string source,
        CustomCommandImportFormat format)
    {
        try
        {
            _customImportSource = source;
            var parsed = _customCommandImport.Parse(content, source, format);
            _customImportCandidates = _customCommandImport.Analyze(parsed,
                ReadSettingsFromControls()).ToList();
            RenderCustomImportPreview();
        }
        catch (Exception exception)
        {
            ShowCustomImportError("Import konnte nicht gelesen werden: " + exception.Message);
        }
    }

    private void RecheckCustomImport()
    {
        if (_customImportCandidates.Count == 0)
        {
            AnalyzeCustomImport(_customImportTextBox.Text, _customImportSource,
                CustomCommandImportFormat.Auto);
            return;
        }
        SyncCustomImportGrid();
        _customImportCandidates = _customCommandImport.Analyze(
            _customImportCandidates, ReadSettingsFromControls()).ToList();
        RenderCustomImportPreview();
    }

    private void ApplyCustomImport(bool onlyConflictFree)
    {
        if (_customImportCandidates.Count == 0 || !_customImportPreviewValid)
        {
            ShowCustomImportError("Bitte zuerst eine gültige Vorschau erstellen.");
            return;
        }
        try
        {
            SyncCustomImportGrid();
            var config = ReadSettingsFromControls();
            var result = _customCommandImport.Apply(_customImportCandidates,
                config, onlyConflictFree);
            config.Commands.CustomCommands = result.Commands.ToList();
            LoadCustomCommandSettings(config.Commands);
            SaveSettingsFromControls();
            var summary = $"Erkannt: {result.Recognized} · Importiert: {result.Imported} · " +
                          $"Übersprungen: {result.Skipped} · Konflikte: {result.Conflicts} · " +
                          $"Ungültig: {result.Invalid}";
            _customImportStatusLabel.Text = summary;
            _customImportStatusLabel.ForeColor = result.Invalid > 0 ? ErrorColor : ActiveColor;
            AppendLog("Custom-Command-Import: " + summary);
        }
        catch (Exception exception)
        {
            ShowCustomImportError("Import fehlgeschlagen: " + exception.Message);
        }
    }

    private void SyncCustomImportGrid()
    {
        _customImportGrid.EndEdit();
        foreach (DataGridViewRow row in _customImportGrid.Rows)
        {
            if (row.Tag is not CustomCommandImportCandidate item) continue;
            item.Command.Command = CellText(row, "Command");
            item.Command.Aliases = SplitCommandValues(CellText(row, "Aliases"));
            var responseCell = row.Cells["Response"];
            var original = responseCell.Tag?.ToString() ?? item.Command.Response;
            var displayed = responseCell.Value?.ToString() ?? "";
            if (!displayed.Equals(PreviewText(original, 180),
                    StringComparison.Ordinal))
                item.Command.Response = displayed;
            item.Command.UserCooldownSeconds = ParseCooldown(row, "Cooldown");
            item.Command.Enabled = row.Cells["Enabled"].Value is true;
            item.Command.RequiredRole = ParseRoleLabel(CellText(row, "Role")).ToString();
            item.Action = ParseImportAction(CellText(row, "Action"));
        }
    }

    private void RenderCustomImportPreview()
    {
        _customImportPreviewValid = false;
        _customImportDetailsBox.Clear();
        _customImportGrid.SuspendLayout();
        try
        {
            _customImportGrid.Rows.Clear();
            foreach (var item in _customImportCandidates)
            {
                var responsePreview = PreviewText(item.Command.Response, 180);
                var conflictPreview = PreviewText(item.ConflictMessage, 220);
                var index = _customImportGrid.Rows.Add(
                    item.Command.Command,
                    string.Join(", ", item.Command.Aliases),
                    responsePreview, item.Command.UserCooldownSeconds,
                    item.Command.Enabled,
                    RoleLabel(CommandRegistry.ParseRole(item.Command.RequiredRole)),
                    PreviewText(item.Source, 80), item.ImportStatus,
                    conflictPreview, ImportActionLabel(item.Action));
                var row = _customImportGrid.Rows[index];
                row.Height = 28;
                row.MinimumHeight = 28;
                row.Tag = item;
                row.Cells["Response"].Tag = item.Command.Response;
                row.Cells["Response"].ToolTipText = item.Command.Response;
                row.Cells["Conflict"].ToolTipText = item.ConflictMessage;
                if (item.ConflictType == CustomCommandConflictType.Invalid)
                    row.DefaultCellStyle.ForeColor = ErrorColor;
                else if (item.ConflictType != CustomCommandConflictType.None)
                    row.DefaultCellStyle.ForeColor = WaitingColor;
            }
            var conflicts = _customImportCandidates.Count(item =>
                item.ConflictType is not CustomCommandConflictType.None and
                    not CustomCommandConflictType.Invalid);
            var invalid = _customImportCandidates.Count(item =>
                item.ConflictType == CustomCommandConflictType.Invalid);
            _customImportStatusLabel.Text =
                $"Erkannt: {_customImportCandidates.Count} · Konflikte: {conflicts} · Ungültig: {invalid}";
            _customImportStatusLabel.ForeColor = conflicts + invalid > 0
                ? WaitingColor : ActiveColor;
            _customImportPreviewValid = true;
            UpdateCustomImportDetails();
        }
        catch (Exception exception)
        {
            _customImportGrid.Rows.Clear();
            ShowCustomImportError("Vorschau konnte nicht angezeigt werden: " +
                exception.Message + " Der Import wurde nicht ausgeführt.");
        }
        finally
        {
            _customImportGrid.ResumeLayout();
        }
    }

    private void UpdateCustomImportDetails()
    {
        if (_customImportGrid.SelectedRows.Count != 1 ||
            _customImportGrid.SelectedRows[0].Tag is not CustomCommandImportCandidate item)
        {
            _customImportDetailsBox.Clear();
            return;
        }
        _customImportDetailsBox.Text = item.Command.Response;
    }

    public static string PreviewText(string? value, int maximumLength)
    {
        var normalized = string.Join(' ', (value ?? "")
            .Replace((char)13, ' ').Replace((char)10, ' ')
            .Replace((char)9, ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= maximumLength) return normalized;
        return normalized[..Math.Max(0, maximumLength - 1)] + "…";
    }

    private void ShowCustomImportError(string message)
    {
        _customImportStatusLabel.Text = message;
        _customImportStatusLabel.ForeColor = ErrorColor;
        AppendLog("Custom-Command-Import: " + message);
    }

    private static string ImportActionLabel(CustomCommandImportAction action) =>
        action switch
        {
            CustomCommandImportAction.Skip => "Überspringen",
            CustomCommandImportAction.Overwrite => "Überschreiben",
            CustomCommandImportAction.Rename => "Umbenennen",
            _ => "Importieren"
        };

    private static CustomCommandImportAction ParseImportAction(string? value) =>
        (value ?? "").Trim() switch
        {
            "Überschreiben" => CustomCommandImportAction.Overwrite,
            "Umbenennen" => CustomCommandImportAction.Rename,
            "Überspringen" => CustomCommandImportAction.Skip,
            _ => CustomCommandImportAction.Import
        };
}
