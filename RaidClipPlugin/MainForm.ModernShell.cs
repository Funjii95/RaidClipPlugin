using System.Runtime.InteropServices;
using System.Text.Json;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private const int SidebarExpandedWidth = 284;
    private const int SidebarCompactWidth = 72;

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
    private TableLayoutPanel? _modernSidebarShell;
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
        var bar = new Panel
        {
  Dock = DockStyle.Fill,
  BackColor = Color.FromArgb(6, 10, 14),
  Padding = new Padding(16, 0, 6, 0)
        };
        bar.MouseDown += DragModernWindow;

        var layout = new TableLayoutPanel
        {
  Dock = DockStyle.Fill,
  ColumnCount = 2,
  RowCount = 1,
  BackColor = bar.BackColor,
  Margin = Padding.Empty,
  Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.MouseDown += DragModernWindow;

        var titleFlow = new FlowLayoutPanel
        {
  Dock = DockStyle.Fill,
  FlowDirection = FlowDirection.LeftToRight,
  WrapContents = false,
  BackColor = bar.BackColor,
  Padding = new Padding(0, 8, 0, 0),
  Margin = Padding.Empty
        };
        titleFlow.MouseDown += DragModernWindow;

        var logo = new Label
        {
  Text = "R",
  AutoSize = false,
  Width = 30,
  Height = 28,
  Font = new Font("Segoe UI", 18F, FontStyle.Bold),
  ForeColor = AccentColor,
  TextAlign = ContentAlignment.MiddleCenter,
  Margin = new Padding(0, 0, 8, 0)
        };
        logo.MouseDown += DragModernWindow;

        var title = new Label
        {
  Text = "RaidClipPlugin",
  AutoSize = true,
  Font = new Font("Segoe UI", 11F, FontStyle.Bold),
  ForeColor = TextColor,
  Margin = new Padding(0, 4, 8, 0)
        };
        title.MouseDown += DragModernWindow;

        var version = new Label
        {
  Text = _updateService.CurrentDisplayVersion,
  AutoSize = true,
  Font = new Font("Segoe UI", 9F),
  ForeColor = MutedTextColor,
  Margin = new Padding(0, 6, 0, 0)
        };
        version.MouseDown += DragModernWindow;

        titleFlow.Controls.Add(logo);
        titleFlow.Controls.Add(title);
        titleFlow.Controls.Add(version);

        var right = new FlowLayoutPanel
        {
  AutoSize = true,
  FlowDirection = FlowDirection.LeftToRight,
  WrapContents = false,
  BackColor = bar.BackColor,
  Margin = Padding.Empty,
  Padding = Padding.Empty
        };
        right.Controls.Add(CreateTopbarChip("▣  Funjii", AccentColor));
        right.Controls.Add(CreateTopbarChip("●  Aktiv", HealthyStatusColor));
        right.Controls.Add(CreateTopbarChip("🔔", AccentColor));
        right.Controls.Add(CreateWindowButton("–", () => WindowState = FormWindowState.Minimized));
        right.Controls.Add(CreateWindowButton("□", ToggleModernMaximize));
        right.Controls.Add(CreateWindowButton("×", Close));

        layout.Controls.Add(titleFlow, 0, 0);
        layout.Controls.Add(right, 1, 0);
        bar.Controls.Add(layout);
        return bar;
    }

    private Label CreateTopbarChip(string text, Color iconColor)
    {
        return new Label
        {
  Text = text,
  AutoSize = false,
  Height = 34,
  Width = text.Length <= 3 ? 52 : 92,
  TextAlign = ContentAlignment.MiddleCenter,
  Font = new Font("Segoe UI", 9F, FontStyle.Bold),
  ForeColor = text.Contains("●") ? HealthyStatusColor : TextColor,
  BackColor = SurfaceColor,
  Margin = new Padding(4, 4, 4, 0),
  Padding = new Padding(6, 0, 6, 0)
        };
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
        _modernSidebarShell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = SidebarColor, Padding = Padding.Empty, Margin = Padding.Empty };
        _modernSidebarShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _modernSidebarShell.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        _modernSidebarShell.Controls.Add(navigation, 0, 0);
        _modernSidebarShell.Controls.Add(CreateSidebarStatusCard(), 0, 1);
        return _modernSidebarShell;
    }

    private static Label CreateSidebarDivider(string text)
    {
        return new Label
        {
  Text = text.ToUpperInvariant(),
  AutoSize = false,
  Width = 236,
  Height = 22,
  ForeColor = MutedTextColor,
  BackColor = SidebarColor,
  Font = new Font("Segoe UI", 7.8F, FontStyle.Bold),
  TextAlign = ContentAlignment.BottomLeft,
  Padding = new Padding(18, 0, 0, 4),
  Margin = new Padding(6, 6, 6, 0)
        };
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
        var compact = ClientSize.Width < 1220 || ClientSize.Height < 720;
        if (_modernCompactSidebar == compact) return;
        _modernCompactSidebar = compact;
        _modernRootLayout.ColumnStyles[0].Width = compact ? SidebarCompactWidth : SidebarExpandedWidth;
        if (_modernSidebarNavigation is null) return;
        if (_modernSidebarShell?.RowStyles.Count > 1)
        {
            _modernSidebarShell.RowStyles[1].Height = compact ? 0 : 88;
        }

        foreach (Control control in _modernSidebarNavigation.Controls)
        {
            if (control is PictureBox picture) { picture.Visible = !compact && ClientSize.Height >= 700; continue; }
            if (control is Button button && button.Tag is Tuple<string, string> meta)
            {
                button.Width = compact ? 42 : 240;
                button.Height = compact ? 34 : 50;
                button.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                button.Padding = compact ? Padding.Empty : new Padding(14, 5, 12, 5);
                button.Text = compact ? meta.Item1.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? meta.Item1 : $"{meta.Item1}{Environment.NewLine}{meta.Item2}";
                _moduleHealthToolTip.SetToolTip(button, meta.Item1.Trim());
            }
        }
    }


    private void FitModernWindowToWorkingArea()
    {
        try
        {
            var area = Screen.FromControl(this).WorkingArea;
            var targetWidth = Math.Min(Size.Width, area.Width - 32);
            var targetHeight = Math.Min(Size.Height, area.Height - 32);
            targetWidth = Math.Max(960, targetWidth);
            targetHeight = Math.Max(620, targetHeight);

            MinimumSize = new Size(
                Math.Min(MinimumSize.Width, targetWidth),
                Math.Min(MinimumSize.Height, targetHeight));

            Size = new Size(targetWidth, targetHeight);
            if (WindowState == FormWindowState.Normal)
            {
                Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
                Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
            }

            if (area.Width < 1200 || area.Height < 720)
            {
                WindowState = FormWindowState.Maximized;
            }
        }
        catch
        {
            // Best-effort only: layout still uses the minimum size above.
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

