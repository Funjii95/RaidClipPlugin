namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly ComboBox _uiThemeBox = new()
    {
        Width = 190,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private void InitializeThemeEvents()
    {
        _uiThemeBox.Items.Clear();
        _uiThemeBox.Items.AddRange(new object[]
        {
            "Raid-Rot", "Giftgrün", "Twitch-Lila"
        });
        _uiThemeBox.SelectedIndexChanged += (_, _) =>
        {
            if (_uiThemeBox.SelectedIndex >= 0)
                ApplyUiTheme(ThemeKeyFromSelection());
        };
    }

    private string ThemeKeyFromSelection() => _uiThemeBox.SelectedIndex switch
    {
        1 => "NeonGreen",
        2 => "TwitchPurple",
        _ => "RaidRed"
    };

    private void SelectUiTheme(string? key)
    {
        _uiThemeBox.SelectedIndex = key?.Trim().ToLowerInvariant() switch
        {
            "neongreen" => 1,
            "twitchpurple" => 2,
            _ => 0
        };
    }

    private void ApplyUiTheme(string? key)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
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

            case "twitchpurple":
                BackgroundColor = Color.FromArgb(10, 8, 18);
                SidebarColor = Color.FromArgb(15, 10, 28);
                SurfaceColor = Color.FromArgb(24, 18, 40);
                InputColor = Color.FromArgb(14, 10, 24);
                BorderColor = Color.FromArgb(105, 80, 150);
                AccentColor = Color.FromArgb(145, 70, 255);
                AccentDarkColor = Color.FromArgb(67, 36, 112);
                TextColor = Color.FromArgb(246, 242, 255);
                MutedTextColor = Color.FromArgb(190, 177, 214);
                ActiveColor = Color.FromArgb(169, 112, 255);
                break;

            default:
                BackgroundColor = Color.FromArgb(8, 8, 9);
                SidebarColor = Color.FromArgb(12, 12, 13);
                SurfaceColor = Color.FromArgb(19, 19, 21);
                InputColor = Color.FromArgb(13, 13, 14);
                BorderColor = Color.FromArgb(86, 86, 90);
                AccentColor = Color.FromArgb(222, 24, 30);
                AccentDarkColor = Color.FromArgb(92, 12, 15);
                TextColor = Color.FromArgb(235, 235, 238);
                MutedTextColor = Color.FromArgb(174, 174, 180);
                ActiveColor = Color.FromArgb(239, 36, 42);
                break;
        }

        BackColor = BackgroundColor;
        ForeColor = TextColor;
        ApplyRaidClipTheme(this);
        Invalidate(true);
    }
}
