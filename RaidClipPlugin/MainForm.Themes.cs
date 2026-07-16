namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly ComboBox _uiThemeBox = new()
    {
        Width = 210,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private void InitializeThemeEvents()
    {
        _uiThemeBox.Items.Clear();
        _uiThemeBox.Items.AddRange(new object[]
        {
            "Modern Rot", "Dark Purple", "Dark Blue", "Light Modern", "Giftgrün", "Twitch-Lila"
        });
        _uiThemeBox.SelectedIndexChanged += (_, _) =>
        {
            if (_uiThemeBox.SelectedIndex >= 0)
                ApplyUiTheme(ThemeKeyFromSelection());
        };
    }

    private string ThemeKeyFromSelection() => _uiThemeBox.SelectedIndex switch
    {
        1 => "DarkPurple",
        2 => "DarkBlue",
        3 => "LightModern",
        4 => "NeonGreen",
        5 => "TwitchPurple",
        _ => "RaidRed"
    };

    private void SelectUiTheme(string? key)
    {
        _uiThemeBox.SelectedIndex = key?.Trim().ToLowerInvariant() switch
        {
            "darkpurple" => 1,
            "darkblue" => 2,
            "lightmodern" => 3,
            "neongreen" => 4,
            "twitchpurple" => 5,
            _ => 0
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

