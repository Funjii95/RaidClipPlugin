using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly ComboBox _uiThemeBox = new()
    {
        Width = 210,
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly NumericUpDown _accentRed = CreateRgbControl(255);
    private readonly NumericUpDown _accentGreen = CreateRgbControl(48);
    private readonly NumericUpDown _accentBlue = CreateRgbControl(58);
    private readonly TextBox _accentHex = new() { Width = 92, Text = "#FF303A", MaxLength = 7 };
    private readonly Panel _accentPreview = new() { Width = 42, Height = 32, BackColor = Color.FromArgb(255, 48, 58) };
    private bool _updatingAccentControls;

    private static NumericUpDown CreateRgbControl(int value) => new()
    {
        Minimum = 0,
        Maximum = 255,
        Value = value,
        Width = 68
    };

    private void InitializeThemeEvents()
    {
        _uiThemeBox.Items.Clear();
        _uiThemeBox.Items.AddRange(new object[]
        {
            "Dark Purple", "Dark Blue", "Light Modern", "Modern Rot", "Giftgrün", "Twitch-Lila"
        });
        _uiThemeBox.SelectedIndexChanged += (_, _) =>
        {
            if (_uiThemeBox.SelectedIndex >= 0)
            {
                ApplyUiTheme(ThemeKeyFromSelection());
                SetAccentControls(AccentColor, apply: false);
            }
        };
        _accentRed.ValueChanged += (_, _) => ApplyAccentFromRgb();
        _accentGreen.ValueChanged += (_, _) => ApplyAccentFromRgb();
        _accentBlue.ValueChanged += (_, _) => ApplyAccentFromRgb();
        _accentHex.Leave += (_, _) => ApplyAccentFromHex(showError: true);
        _accentHex.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode != Keys.Enter) return;
            ApplyAccentFromHex(showError: true);
            eventArgs.SuppressKeyPress = true;
        };
    }

    private Control BuildAccentColorEditor()
    {
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        flow.Controls.Add(new Label { Text = "R", AutoSize = true, Margin = new Padding(0, 9, 3, 0) });
        flow.Controls.Add(_accentRed);
        flow.Controls.Add(new Label { Text = "G", AutoSize = true, Margin = new Padding(8, 9, 3, 0) });
        flow.Controls.Add(_accentGreen);
        flow.Controls.Add(new Label { Text = "B", AutoSize = true, Margin = new Padding(8, 9, 3, 0) });
        flow.Controls.Add(_accentBlue);
        flow.Controls.Add(_accentHex);
        flow.Controls.Add(_accentPreview);
        var choose = NewActionButton("Farbe wählen");
        choose.Click += (_, _) => ChooseAccentColor();
        var reset = NewActionButton("Standard");
        reset.Click += (_, _) => SetAccentControls(Color.FromArgb(255, 48, 58), apply: true);
        flow.Controls.Add(choose);
        flow.Controls.Add(reset);
        return flow;
    }

    private void ChooseAccentColor()
    {
        using var dialog = new ColorDialog
        {
            Color = Color.FromArgb((int)_accentRed.Value, (int)_accentGreen.Value, (int)_accentBlue.Value),
            FullOpen = true,
            AnyColor = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            SetAccentControls(dialog.Color, apply: true);
    }

    private void ApplyAccentFromRgb()
    {
        if (_updatingAccentControls) return;
        SetAccentControls(Color.FromArgb((int)_accentRed.Value,
            (int)_accentGreen.Value, (int)_accentBlue.Value), apply: true);
    }

    private void ApplyAccentFromHex(bool showError)
    {
        var normalized = ConfigurationService.NormalizeAccentColor(_accentHex.Text);
        if (string.IsNullOrEmpty(normalized))
        {
            if (showError)
                MessageBox.Show(this, "Bitte eine Farbe im Format #RRGGBB eingeben.",
                    "Ungültige Akzentfarbe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _accentHex.Text = ColorToHex(AccentColor);
            return;
        }
        SetAccentControls(ColorTranslator.FromHtml(normalized), apply: true);
    }

    private void SetAccentControls(Color color, bool apply)
    {
        _updatingAccentControls = true;
        _accentRed.Value = color.R;
        _accentGreen.Value = color.G;
        _accentBlue.Value = color.B;
        _accentHex.Text = ColorToHex(color);
        _accentPreview.BackColor = color;
        _updatingAccentControls = false;
        if (apply) ApplyCustomAccent(color);
    }

    private void ApplySavedAccent(string? hex)
    {
        var normalized = ConfigurationService.NormalizeAccentColor(hex);
        SetAccentControls(string.IsNullOrEmpty(normalized)
            ? AccentColor
            : ColorTranslator.FromHtml(normalized), apply: !string.IsNullOrEmpty(normalized));
    }

    private void ApplyCustomAccent(Color color)
    {
        var previous = AccentColor;
        AccentColor = color;
        AccentDarkColor = Color.FromArgb(
            Math.Max(18, color.R * 44 / 100),
            Math.Max(12, color.G * 44 / 100),
            Math.Max(14, color.B * 44 / 100));
        BorderColor = Color.FromArgb(
            Math.Max(35, color.R * 45 / 100),
            Math.Max(35, color.G * 45 / 100),
            Math.Max(35, color.B * 45 / 100));
        UpdateDashboardAccent(this, previous, color);
        ApplyRaidClipTheme(this);
        Invalidate(true);
    }

    private static void UpdateDashboardAccent(Control root, Color previous, Color color)
    {
        foreach (Control control in root.Controls)
        {
            if (control is DashboardCardPanel card && card.AccentColor == previous)
                card.AccentColor = color;
            UpdateDashboardAccent(control, previous, color);
        }
    }

    private static string ColorToHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private string SelectedAccentHex() => ColorToHex(Color.FromArgb(
        (int)_accentRed.Value, (int)_accentGreen.Value, (int)_accentBlue.Value));

    private string ThemeKeyFromSelection() => _uiThemeBox.SelectedIndex switch
    {
        0 => "DarkPurple",
        1 => "DarkBlue",
        2 => "LightModern",
        3 => "RaidRed",
        4 => "NeonGreen",
        5 => "TwitchPurple",
        _ => "RaidRed"
    };

    private void SelectUiTheme(string? key)
    {
        _uiThemeBox.SelectedIndex = key?.Trim().ToLowerInvariant() switch
        {
            "darkpurple" => 0,
            "darkblue" => 1,
            "lightmodern" => 2,
            "raidred" => 3,
            "neongreen" => 4,
            "twitchpurple" => 5,
            _ => 3
        };
    }

    private void ApplyUiTheme(string? key)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
            case "darkpurple":
            case "twitchpurple":
                BackgroundColor = Color.FromArgb(9, 7, 15);
                SidebarColor = Color.FromArgb(15, 10, 27);
                SurfaceColor = Color.FromArgb(22, 18, 34);
                InputColor = Color.FromArgb(13, 10, 22);
                BorderColor = Color.FromArgb(62, 45, 93);
                AccentColor = Color.FromArgb(170, 92, 255);
                AccentDarkColor = Color.FromArgb(101, 47, 173);
                TextColor = Color.FromArgb(246, 242, 255);
                MutedTextColor = Color.FromArgb(190, 178, 214);
                ActiveColor = Color.FromArgb(91, 232, 112);
                break;

            case "darkblue":
                BackgroundColor = Color.FromArgb(3, 13, 28);
                SidebarColor = Color.FromArgb(5, 20, 42);
                SurfaceColor = Color.FromArgb(8, 28, 56);
                InputColor = Color.FromArgb(4, 17, 36);
                BorderColor = Color.FromArgb(31, 86, 150);
                AccentColor = Color.FromArgb(26, 128, 255);
                AccentDarkColor = Color.FromArgb(5, 78, 165);
                TextColor = Color.FromArgb(235, 247, 255);
                MutedTextColor = Color.FromArgb(165, 196, 226);
                ActiveColor = Color.FromArgb(91, 232, 112);
                break;

            case "lightmodern":
                BackgroundColor = Color.FromArgb(246, 248, 250);
                SidebarColor = Color.FromArgb(255, 255, 255);
                SurfaceColor = Color.FromArgb(255, 255, 255);
                InputColor = Color.FromArgb(250, 252, 253);
                BorderColor = Color.FromArgb(220, 225, 230);
                AccentColor = Color.FromArgb(55, 184, 91);
                AccentDarkColor = Color.FromArgb(213, 242, 219);
                TextColor = Color.FromArgb(28, 32, 38);
                MutedTextColor = Color.FromArgb(93, 104, 116);
                ActiveColor = Color.FromArgb(25, 150, 60);
                break;

            case "neongreen":
                BackgroundColor = Color.FromArgb(4, 12, 7);
                SidebarColor = Color.FromArgb(5, 18, 9);
                SurfaceColor = Color.FromArgb(10, 27, 15);
                InputColor = Color.FromArgb(4, 15, 8);
                BorderColor = Color.FromArgb(52, 128, 63);
                AccentColor = Color.FromArgb(57, 255, 20);
                AccentDarkColor = Color.FromArgb(20, 88, 18);
                TextColor = Color.FromArgb(238, 255, 239);
                MutedTextColor = Color.FromArgb(166, 205, 172);
                ActiveColor = Color.FromArgb(84, 255, 62);
                break;

            case "raidred":
            default:
                BackgroundColor = Color.FromArgb(8, 8, 10);
                SidebarColor = Color.FromArgb(13, 13, 16);
                SurfaceColor = Color.FromArgb(22, 22, 26);
                InputColor = Color.FromArgb(13, 13, 16);
                BorderColor = Color.FromArgb(83, 39, 43);
                AccentColor = Color.FromArgb(255, 48, 58);
                AccentDarkColor = Color.FromArgb(112, 17, 24);
                TextColor = Color.FromArgb(245, 245, 248);
                MutedTextColor = Color.FromArgb(180, 180, 188);
                ActiveColor = Color.FromArgb(91, 232, 112);
                break;
        }

        BackColor = BackgroundColor;
        ForeColor = TextColor;
        ApplyRaidClipTheme(this);
        Invalidate(true);
    }
}

