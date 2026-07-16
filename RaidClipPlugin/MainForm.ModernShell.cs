using System.Runtime.InteropServices;
using System.Text.Json;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private const int SidebarExpandedWidth = 308;
    private const int SidebarCompactWidth = 82;

    private readonly Label _modernSidebarBotStateLabel = new()
    {
        Text = "INAKTIV",
        AutoSize = true,
        Font = new Font("Segoe UI", 12F, FontStyle.Bold),
        ForeColor = InactiveColor,
        Margin = new Padding(0, 4, 0, 0)
    };

    private readonly Label _modernSidebarTwitchLabel = new()
    {
        Text = "Twitch: Getrennt",
        AutoSize = true,
        ForeColor = MutedTextColor,
        Font = new Font("Segoe UI", 8.4F),
        Margin = new Padding(0, 6, 0, 0)
    };

    private readonly StatusDotControl _modernSidebarDot = new()
    {
        Width = 12,
        Height = 12,
        DotColor = InactiveColor,
        Margin = new Padding(8, 8, 0, 0)
    };

    private FlowLayoutPanel? _modernSidebarNavigation;
    private TableLayoutPanel? _modernRootLayout;
    private bool _modernCompactSidebar;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private static string WindowStatePath
    {
        get
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaidClipPlugin");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "window-state.json");
        }
    }

    private Panel CreateModernTitleBar()
    {
        var bar = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor, Padding = new Padding(12, 0, 6, 0) };
        bar.MouseDown += DragModernWindow;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackgroundColor, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.MouseDown += DragModernWindow;

        var titleFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = BackgroundColor, Padding = new Padding(0, 7, 0, 0), Margin = Padding.Empty };
        titleFlow.MouseDown += DragModernWindow;
        var icon = new PictureBox { Image = Icon?.ToBitmap(), Width = 22, Height = 22, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 2, 8, 0), BackColor = BackgroundColor };
        icon.MouseDown += DragModernWindow;
        var title = new Label { Text = "RaidClip Plugin", AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = TextColor, Margin = new Padding(0, 4, 0, 0) };
        title.MouseDown += DragModernWindow;
        titleFlow.Controls.Add(icon);
        titleFlow.Controls.Add(title);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = BackgroundColor, Margin = Padding.Empty, Padding = Padding.Empty };
        buttons.Controls.Add(CreateWindowButton("–", () => WindowState = FormWindowState.Minimized));
        buttons.Controls.Add(CreateWindowButton("□", ToggleModernMaximize));
        buttons.Controls.Add(CreateWindowButton("×", Close));

        layout.Controls.Add(titleFlow, 0, 0);
        layout.Controls.Add(buttons, 1, 0);
        bar.Controls.Add(layout);
        return bar;
    }

    private Button CreateWindowButton(string text, Action action)
    {
        var button = new Button { Text = text, Width = 42, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = BackgroundColor, ForeColor = TextColor, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = Padding.Empty, TabStop = true, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentDarkColor;
        button.FlatAppearance.MouseDownBackColor = AccentColor;
        button.Click += (_, _) => action();
        return button;
    }

    private void DragModernWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        _ = SendMessage(Handle, 0xA1, 0x2, 0);
    }

    private void ToggleModernMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }

    private Control CreateSidebarShell(FlowLayoutPanel navigation)
    {
        _modernSidebarNavigation = navigation;
        navigation.BackColor = SidebarColor;
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = SidebarColor, Padding = Padding.Empty, Margin = Padding.Empty };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        shell.Controls.Add(navigation, 0, 0);
        shell.Controls.Add(CreateSidebarStatusCard(), 0, 1);
        return shell;
    }

    private Control CreateSidebarStatusCard()
    {
        var card = new Panel { Name = "SurfacePanel", Dock = DockStyle.Fill, BackColor = SurfaceColor, Padding = new Padding(12), Margin = new Padding(12, 8, 12, 14) };
        var title = new Label { Text = "Bot Status", AutoSize = true, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(12, 10) };
        var stateRow = new FlowLayoutPanel { Location = new Point(12, 30), Size = new Size(200, 28), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = SurfaceColor };
        stateRow.Controls.Add(_modernSidebarBotStateLabel);
        stateRow.Controls.Add(_modernSidebarDot);
        _modernSidebarTwitchLabel.Location = new Point(12, 66);
        card.Controls.Add(title);
        card.Controls.Add(stateRow);
        card.Controls.Add(_modernSidebarTwitchLabel);
        return card;
    }

    private void UpdateModernServiceStatus(string service, string state, Color color)
    {
        if (service.Equals("Twitch", StringComparison.OrdinalIgnoreCase))
            _modernSidebarTwitchLabel.Text = "Twitch: " + state;
    }

    private void UpdateModernBotStatus(string text, Color color)
    {
        var active = text.Contains("Aktiv", StringComparison.OrdinalIgnoreCase);
        _modernSidebarBotStateLabel.Text = active ? "AKTIV" : "INAKTIV";
        _modernSidebarBotStateLabel.ForeColor = color;
        _modernSidebarDot.DotColor = color;
    }

    private void ApplyResponsiveSidebar()
    {
        _modernRootLayout ??= Controls.Find("ModernRootLayout", true).OfType<TableLayoutPanel>().FirstOrDefault();
        if (_modernRootLayout is null || _modernRootLayout.ColumnStyles.Count == 0) return;
        var compact = ClientSize.Width < 1240;
        if (_modernCompactSidebar == compact) return;
        _modernCompactSidebar = compact;
        _modernRootLayout.ColumnStyles[0].Width = compact ? SidebarCompactWidth : SidebarExpandedWidth;
        if (_modernSidebarNavigation is null) return;
        foreach (Control control in _modernSidebarNavigation.Controls)
        {
            if (control is PictureBox picture) { picture.Visible = !compact; continue; }
            if (control is Button button && button.Tag is Tuple<string, string> meta)
            {
                button.Width = compact ? 48 : 266;
                button.Height = compact ? 48 : 64;
                button.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                button.Padding = compact ? Padding.Empty : new Padding(14, 8, 12, 8);
                button.Text = compact ? meta.Item1.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? meta.Item1 : $"{meta.Item1}{Environment.NewLine}{meta.Item2}";
                _moduleHealthToolTip.SetToolTip(button, meta.Item1.Trim());
            }
        }
    }

    private void RestoreModernWindowPlacement()
    {
        try
        {
            if (!File.Exists(WindowStatePath)) return;
            var state = JsonSerializer.Deserialize<ModernWindowState>(File.ReadAllText(WindowStatePath));
            if (state is null || state.Width < 900 || state.Height < 650) return;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(Math.Max(state.Width, MinimumSize.Width), Math.Max(state.Height, MinimumSize.Height));
            Location = new Point(state.Left, state.Top);
        }
        catch { StartPosition = FormStartPosition.CenterScreen; }
    }

    private void SaveModernWindowPlacement()
    {
        try
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var state = new ModernWindowState(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            File.WriteAllText(WindowStatePath, JsonSerializer.Serialize(state));
        }
        catch { }
    }

    private sealed record ModernWindowState(int Left, int Top, int Width, int Height);
}

