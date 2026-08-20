namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly FlowLayoutPanel _dashboardEventFeed = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        BackColor = Color.Transparent,
        Padding = new Padding(0, 4, 0, 0)
    };
    private static string GetServiceIcon(string service) => service switch
    {
        "OBS" => "◉",
        "Twitch" => "▣",
        "EventSub" => "✦",
        "Player" => "▶",
        "Discord" => "☁",
        "Musikdienst" => "♫",
        "Commands" => "</>",
        "Updater" => "↻",
        _ => "●"
    };

    private static Color GetServiceAccent(string service) => service switch
    {
        "OBS" => Color.FromArgb(230, 235, 242),
        "Twitch" => Color.FromArgb(166, 96, 255),
        "EventSub" => Color.FromArgb(255, 203, 72),
        "Player" => Color.FromArgb(46, 204, 113),
        "Discord" => Color.FromArgb(88, 101, 242),
        "Musikdienst" => Color.FromArgb(255, 92, 167),
        "Commands" => Color.FromArgb(0, 211, 255),
        "Updater" => Color.FromArgb(64, 156, 255),
        _ => AccentColor
    };

    private static Color CardColor => Color.FromArgb(14, 22, 31);
    private static Color CardBorderColor => Color.FromArgb(43, 55, 69);

    private Control CreateDashboardHeader(Control header, Control updatePanel)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = BackgroundColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        var greeting = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = BackgroundColor };
        greeting.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        greeting.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        greeting.Controls.Add(new Label { Text = "Guten Abend, Funjii", Dock = DockStyle.Fill, AutoSize = false, ForeColor = TextColor, Font = new Font("Segoe UI", 20F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        greeting.Controls.Add(new Label { Text = "Dein Stream-System ist bereit.", Dock = DockStyle.Fill, AutoSize = false, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 10.2F), TextAlign = ContentAlignment.TopLeft }, 0, 1);
        layout.Controls.Add(greeting, 0, 0);
        layout.Controls.Add(CreateUpdateStatusCard(), 1, 0);
        return layout;
    }

    private Control CreateUpdateStatusCard()
    {
        var card = CreateCardPanel(AccentColor, new Padding(18, 7, 18, 7));
        card.MinimumSize = new Size(0, 56);
        card.Margin = new Padding(18, 8, 18, 14);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.AutoSize = false;
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _versionLabel.Margin = Padding.Empty;
        _versionLabel.MaximumSize = Size.Empty;
        _versionLabel.Font = new Font("Segoe UI", 9.4F, FontStyle.Bold);
        _versionLabel.ForeColor = HealthyStatusColor;

        var buttonHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 2, 4)
        };

        foreach (var button in new[] { _updateButton, _changelogButton, _installUpdateButton, _skipUpdateButton })
        {
            CompactDashboardButton(button, button == _updateButton ? 184 : 126);
            button.Dock = DockStyle.None;
            button.Height = 34;
            button.MinimumSize = new Size(button.Width, 34);
            button.MaximumSize = new Size(button.Width, 34);
            button.Margin = new Padding(0, 0, 8, 0);
            buttonHost.Controls.Add(button);
        }

        layout.Controls.Add(_versionLabel, 0, 0);
        layout.Controls.Add(buttonHost, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private static void CompactDashboardButton(Button button, int width)
    {
        button.AutoSize = false;
        if (width > 0) button.Width = width;
        button.Height = 38;
        button.Padding = new Padding(8, 0, 8, 0);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.AutoEllipsis = true;
        button.MinimumSize = new Size(0, 36);
    }

    private Control CreateDashboardIndicatorGrid(params Label[] indicators)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = indicators.Length, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 4), BackColor = Color.Transparent };
        for (var index = 0; index < indicators.Length; index++)
        {
  grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / indicators.Length));
  grid.Controls.Add(CreateDashboardStatusCard(indicators[index]), index, 0);
        }
        return grid;
    }

    private Control CreateDashboardStatusCard(Label indicator)
    {
        var service = ReferenceEquals(indicator, _obsIndicator) ? "OBS" :
            ReferenceEquals(indicator, _twitchIndicator) ? "Twitch" :
            ReferenceEquals(indicator, _eventSubIndicator) ? "EventSub" :
            ReferenceEquals(indicator, _playerIndicator) ? "Player" : "Service";
        var accent = GetServiceAccent(service);
        var card = CreateCardPanel(accent, new Padding(14, 12, 14, 12));
        card.Margin = new Padding(6, 3, 6, 3);
        card.MinimumSize = new Size(142, 76);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.Controls.Add(new DashboardIconBadge { IconKind = service, AccentColor = accent, Dock = DockStyle.Fill, Margin = new Padding(2, 3, 8, 3) }, 0, 0);
        heading.Controls.Add(new Label { Text = service == "Player" ? "Lokaler Player" : service == "OBS" ? "OBS Studio" : service, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.5F), ForeColor = TextColor, BackColor = Color.Transparent, Tag = "DashboardTransparent" }, 1, 0);
        indicator.Dock = DockStyle.Fill;
        indicator.AutoSize = false;
        indicator.BorderStyle = BorderStyle.None;
        indicator.BackColor = Color.Transparent;
        indicator.Text = "●  " + (indicator.Text.Contains("Nicht", StringComparison.OrdinalIgnoreCase) ? "Nicht verbunden" : indicator.Text.Split(Environment.NewLine).LastOrDefault());
        indicator.TextAlign = ContentAlignment.MiddleLeft;
        indicator.Font = new Font("Segoe UI", 10F);
        indicator.ForeColor = indicator.Text.Contains("Nicht", StringComparison.OrdinalIgnoreCase) ? InactiveColor : HealthyStatusColor;
        indicator.Padding = new Padding(4, 0, 0, 0);
        indicator.AutoEllipsis = true;
        indicator.Margin = Padding.Empty;
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(indicator, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateDashboardActionBar(Control actions)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
        for (var i = 0; i < 4; i++) host.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        AddQuickAction(host, 0, "▣   Clip abspielen", () => _testButton.PerformClick());
        AddQuickAction(host, 1, "◯   Chat testen", () => _ = SendChatDiagnosticTestAsync());
        AddQuickAction(host, 2, "◷   Timer öffnen", () => ShowSection("timer"));
        AddQuickAction(host, 3, "♜   Minigames", () => ShowSection("minigame"));
        return CreateDashboardSection("Schnellaktionen", host);
    }

    private void AddQuickAction(TableLayoutPanel host, int row, string text, Action action)
    {
        var button = NewActionButton(text + "                                      ›");
        button.Dock = DockStyle.Fill;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Font = new Font("Segoe UI", 9.6F);
        button.Margin = new Padding(0, 4, 0, 4);
        button.BackColor = Color.FromArgb(20, 27, 36);
        button.Click += (_, _) => action();
        host.Controls.Add(button, 0, row);
    }

    private Control CreateModernDashboardLayout(Control dashboardHeader, Control dashboardActions)
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(26, 20, 26, 10), BackColor = BackgroundColor, Margin = Padding.Empty };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = BackgroundColor, Margin = new Padding(0, 0, 18, 0), Padding = Padding.Empty };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 188));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        main.Controls.Add(dashboardHeader, 0, 0);
        main.Controls.Add(CreateHeroStatusCard(), 0, 1);
        main.Controls.Add(CreateModuleGrid(), 0, 2);
        main.Controls.Add(CreateDashboardSection("Heutige Aktivität", CreateDashboardStatsGrid()), 0, 3);
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackgroundColor, Margin = Padding.Empty, Padding = Padding.Empty };
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 312));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.Controls.Add(dashboardActions, 0, 0);
        side.Controls.Add(CreateDashboardSection("Letzte Ereignisse", CreateRecentActivityList()), 0, 1);
        page.Controls.Add(main, 0, 0);
        page.Controls.Add(side, 1, 0);
        var footer = CreateDashboardFooter();
        page.Controls.Add(footer, 0, 1);
        page.SetColumnSpan(footer, 2);
        return page;
    }

    private Control CreateHeroStatusCard()
    {
        var hero = CreateCardPanel(HealthyStatusColor, new Padding(20, 16, 20, 16));
        hero.Margin = new Padding(0, 0, 0, 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        layout.Controls.Add(new DashboardIconBadge { Text = "✓", AccentColor = HealthyStatusColor, Filled = true, Dock = DockStyle.Fill, Margin = new Padding(5, 12, 7, 12), Font = new Font("Segoe UI", 25F, FontStyle.Bold) }, 0, 0);
        var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(8, 0, 0, 0), Padding = Padding.Empty };
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        text.Controls.Add(new Label { Text = "Alle Systeme bereit", Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        _overallStatusLabel.Dock = DockStyle.Fill;
        _overallStatusLabel.AutoSize = false;
        _overallStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _overallStatusLabel.Font = new Font("Segoe UI", 10.2F);
        _overallStatusLabel.ForeColor = MutedTextColor;
        _overallStatusLabel.Margin = Padding.Empty;
        _overallStatusLabel.Padding = Padding.Empty;
        _overallStatusLabel.Text = "Twitch, OBS und EventSub sind verbunden";
        text.Controls.Add(_overallStatusLabel, 0, 1);
        var meta = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 4) };
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        meta.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        meta.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var streamCheck = NewActionButton("Stream prüfen");
        streamCheck.BackColor = AccentColor;
        streamCheck.Click += (_, _) => ShowSection("stream-check");
        var startModules = NewActionButton("Module starten");
        startModules.Click += async (_, _) => await StartPluginAsync();
        var stopModules = NewActionButton("Module stoppen");
        stopModules.Click += async (_, _) => await StopPluginAsync();
        var restartModules = NewActionButton("Neu starten");
        restartModules.Click += async (_, _) => await RestartAllModulesFromDashboardAsync();
        streamCheck.Dock = DockStyle.Fill;
        startModules.Dock = DockStyle.Fill;
        stopModules.Dock = DockStyle.Fill;
        restartModules.Dock = DockStyle.Fill;
        streamCheck.Margin = new Padding(4);
        startModules.Margin = new Padding(4);
        stopModules.Margin = new Padding(4);
        restartModules.Margin = new Padding(4);
        meta.Controls.Add(streamCheck, 0, 0);
        meta.Controls.Add(startModules, 1, 0);
        meta.Controls.Add(stopModules, 0, 1);
        meta.Controls.Add(restartModules, 1, 1);
        layout.Controls.Add(text, 1, 0);
        layout.Controls.Add(meta, 2, 0);
        hero.Controls.Add(layout);
        return hero;
    }

    private async Task RestartAllModulesFromDashboardAsync()
    {
        try
        {
            AppendLog("Dashboard: Module werden neu gestartet.");
            await StopPluginAsync();
            await StartPluginAsync();
        }
        catch (Exception exception)
        {
            AppendLog("Dashboard: Neustart der Module fehlgeschlagen: " + exception.Message);
            MessageBox.Show(this, "Die Module konnten nicht neu gestartet werden: " +
                exception.Message, "Module neu starten", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void AddHeroMeta(TableLayoutPanel meta, int column, string title, string value)
    {
        meta.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, AutoSize = false, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 8.4F), TextAlign = ContentAlignment.BottomCenter, AutoEllipsis = true }, column, 0);
        meta.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, AutoSize = false, ForeColor = TextColor, Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, AutoEllipsis = true }, column, 1);
    }

    private Control CreateModuleGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 8), Margin = Padding.Empty };
        for (var i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.Controls.Add(CreateDashboardStatusCard(_twitchIndicator), 0, 0);
        grid.Controls.Add(CreateDashboardStatusCard(_obsIndicator), 1, 0);
        grid.Controls.Add(CreateDashboardStatusCard(_eventSubIndicator), 2, 0);
        grid.Controls.Add(CreateDashboardStatusCard(_playerIndicator), 3, 0);
        return grid;
    }

    private Control CreateModuleInfoCard(string title, string status, string icon, Color color)
    {
        var card = CreateCardPanel(color, new Padding(10, 6, 10, 6));
        card.Margin = new Padding(5, 4, 5, 4);
        card.MinimumSize = new Size(142, 70);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = icon, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 17F, FontStyle.Bold), ForeColor = color, BackColor = Color.Transparent, Tag = color }, 0, 0);
        var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        text.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 9.4F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true, Margin = Padding.Empty }, 0, 0);
        text.Controls.Add(new Label { Text = status, Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 8.4F, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true, Margin = Padding.Empty }, 0, 1);
        layout.Controls.Add(text, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateDashboardSection(string title, Control content)
    {
        var card = CreateCardPanel(AccentColor, new Padding(18, 14, 18, 16));
        card.Margin = new Padding(0, 0, 0, 8);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = TextColor, Font = new Font("Segoe UI", 12.2F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 0, 0);
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;
        layout.Controls.Add(content, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateDashboardSection(Control content, Padding margin)
    {
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;
        var card = CreateCardPanel(AccentColor, new Padding(12));
        card.Margin = margin;
        card.Controls.Add(content);
        return card;
    }

    private Control CreateDashboardStatsGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        for (var i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        grid.Controls.Add(CreateStatisticCard("♙", "Raids", "—", AccentColor), 0, 0);
        grid.Controls.Add(CreateStatisticCard("▣", "Clips", _historyList.Items.Count.ToString("N0"), AccentColor), 1, 0);
        grid.Controls.Add(CreateStatisticCard("◯", "Chat-Aktionen", "—", AccentColor), 2, 0);
        grid.Controls.Add(CreateStatisticCard("◇", "Events", "—", AccentColor), 3, 0);
        return grid;
    }

    private Control CreateDashboardFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackgroundColor, Padding = new Padding(4, 6, 4, 0), Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        footer.Controls.Add(new Label { Text = "●  Verbindung: Stabil     |     Aktive Module: bereit", Dock = DockStyle.Fill, ForeColor = HealthyStatusColor, Font = new Font("Segoe UI", 8.6F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        footer.Controls.Add(new Label { Text = "RaidClipPlugin " + _updateService.CurrentDisplayVersion, Dock = DockStyle.Fill, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 8.6F), TextAlign = ContentAlignment.MiddleRight }, 1, 0);
        return footer;
    }

    private Control CreateStatisticCard(string icon, string label, string value, Color color)
    {
        var panel = CreateCardPanel(color, new Padding(10, 8, 10, 8));
        panel.Margin = new Padding(5, 0, 5, 0);
        panel.MinimumSize = new Size(112, 96);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        layout.Controls.Add(new Label { Text = icon, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 20F, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.BottomCenter, AutoEllipsis = true }, 0, 0);
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.2F), ForeColor = MutedTextColor, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true }, 0, 1);
        layout.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 24F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true }, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateDashboardHealthSummary()
    {
        // Dashboard uses its own lightweight controls. The real Systemprüfung page
        // owns _moduleHealthGrid/_checkModulesButton/etc.; sharing those controls
        // would move them away from their page and leave the tab visually empty.
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var summary = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        summary.Controls.Add(new StatusDotControl { Width = 16, Height = 16, DotColor = UnknownStatusColor, Margin = new Padding(0, 10, 8, 0) });
        summary.Controls.Add(new Label { Text = "Noch nicht geprüft", Dock = DockStyle.Fill, AutoSize = false, Width = 220, Height = 32, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = UnknownStatusColor, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = Padding.Empty });
        layout.Controls.Add(summary, 0, 0);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var checkButton = NewActionButton("Jetzt prüfen");
        var repairButton = NewActionButton("Reparieren");
        checkButton.Click += async (_, _) => await CheckModulesNowAsync();
        repairButton.Click += async (_, _) => await RestartModulesNowAsync();
        CompactDashboardButton(checkButton, 0);
        CompactDashboardButton(repairButton, 0);
        checkButton.Dock = DockStyle.Fill;
        repairButton.Dock = DockStyle.Fill;
        checkButton.Margin = new Padding(0, 4, 5, 4);
        repairButton.Margin = new Padding(5, 4, 0, 4);
        buttons.Controls.Add(checkButton, 0, 0);
        buttons.Controls.Add(repairButton, 1, 0);
        layout.Controls.Add(buttons, 0, 1);

        var list = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent, Margin = new Padding(0, 8, 0, 0), Padding = Padding.Empty };
        for (var i = 0; i < 4; i++) list.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        foreach (var row in new[] { ("Twitch", "Bereit", HealthyStatusColor), ("OBS", "Bereit", HealthyStatusColor), ("Chat", "Bereit", HealthyStatusColor), ("Updater", "Aktuell", HealthyStatusColor) }) list.Controls.Add(CreateHealthRow(row.Item1, row.Item2, row.Item3));
        layout.Controls.Add(list, 0, 2);
        return layout;
    }

    private Control CreateHealthRow(string name, string state, Color color)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 1, 0, 1), Padding = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        row.Controls.Add(new Label { Text = "●", Dock = DockStyle.Fill, ForeColor = color, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11F, FontStyle.Bold) }, 0, 0);
        row.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, ForeColor = TextColor, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F), AutoEllipsis = true }, 1, 0);
        row.Controls.Add(new Label { Text = state, Dock = DockStyle.Fill, ForeColor = color, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.8F, FontStyle.Bold), AutoEllipsis = true }, 2, 0);
        return row;
    }

    private Control CreateActiveModulesGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = Color.Transparent, Padding = new Padding(2), Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var items = new[] { "Twitch Chat", "Musik System", "EventSub", "Giveaway", "Raidclip", "Commands", "Punkte", "Updater" };
        for (var i = 0; i < items.Length; i++) grid.Controls.Add(new Label { Text = "●  " + items[i], Dock = DockStyle.Fill, ForeColor = HealthyStatusColor, Font = new Font("Segoe UI", 8.2F), Padding = new Padding(2, 1, 2, 0), AutoEllipsis = true }, i % 2, i / 2);
        return grid;
    }

    private Control CreateRecentActivityList()
    {
        if (_dashboardEventFeed.Controls.Count == 0)
            AddDashboardEvent("System", "Dashboard bereit", MutedTextColor);
        return _dashboardEventFeed;
    }

    private void AppendDashboardEvent(string message)
    {
        var lower = message.ToLowerInvariant();
        var eventType = lower.Contains("follow") ? "Follow" :
            lower.Contains("abo") || lower.Contains("subscription") ? "Abo" :
            lower.Contains("cheer") || lower.Contains("bits") ? "Cheer" :
            lower.Contains("werb") ? "Werbepause" :
            lower.Contains("raid") ? "Raid" :
            lower.Contains("clip") ? "Clip" : null;
        if (eventType is null) return;
        var color = eventType == "Werbepause"
            ? Color.FromArgb(245, 176, 65)
            : eventType is "Follow" or "Abo" or "Cheer"
                ? Color.FromArgb(166, 96, 255)
                : AccentColor;
        AddDashboardEvent(eventType, message, color);
    }

    private void AddDashboardEvent(string type, string detail, Color color)
    {
        var row = new TableLayoutPanel { Width = 300, Height = 66, ColumnCount = 3, RowCount = 2, Margin = new Padding(0, 0, 0, 4), BackColor = Color.Transparent };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = "●", Dock = DockStyle.Fill, ForeColor = color, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }, 0, 0);
        row.SetRowSpan(row.Controls[^1], 2);
        row.Controls.Add(new Label { Text = type, Dock = DockStyle.Fill, ForeColor = TextColor, Font = new Font("Segoe UI", 9.4F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft }, 1, 0);
        row.Controls.Add(new Label { Text = DateTime.Now.ToString("HH:mm:ss"), Dock = DockStyle.Fill, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.BottomRight }, 2, 0);
        row.Controls.Add(new Label { Text = detail, Dock = DockStyle.Fill, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 8.2F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 1, 1);
        row.SetColumnSpan(row.Controls[^1], 2);
        _dashboardEventFeed.Controls.Add(row);
        _dashboardEventFeed.Controls.SetChildIndex(row, 0);
        while (_dashboardEventFeed.Controls.Count > 8)
            _dashboardEventFeed.Controls.RemoveAt(_dashboardEventFeed.Controls.Count - 1);
    }

    private DashboardCardPanel CreateCardPanel(Color accent, Padding padding)
    {
        return new DashboardCardPanel { Dock = DockStyle.Fill, BackColor = CardColor, AccentColor = accent, Padding = padding, Margin = Padding.Empty };
    }

    private sealed class DashboardCardPanel : Panel
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = Color.FromArgb(222, 24, 30);
        public DashboardCardPanel() => DoubleBuffered = true;
        protected override void OnPaint(PaintEventArgs e)
        {
  base.OnPaint(e);
  var bounds = ClientRectangle;
  if (bounds.Width <= 1 || bounds.Height <= 1) return;
  e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  using var path = RoundedRectangle(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 10);
  using var background = new SolidBrush(BackColor);
  e.Graphics.FillPath(background, path);
  using var border = new Pen(Color.FromArgb(42, 50, 63), 1F);
  e.Graphics.DrawPath(border, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class DashboardIconBadge : Control
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = Color.FromArgb(255, 48, 58);
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Filled { get; set; }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string IconKind { get; set; } = "";
        public DashboardIconBadge()
        {
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            ForeColor = Color.White;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var diameter = Math.Max(10, Math.Min(ClientSize.Width, ClientSize.Height) - 8);
            var circle = new Rectangle((ClientSize.Width - diameter) / 2, (ClientSize.Height - diameter) / 2, diameter, diameter);
            using var brush = new SolidBrush(Filled ? AccentColor : Color.FromArgb(72, AccentColor));
            e.Graphics.FillEllipse(brush, circle);
            if (!string.IsNullOrEmpty(IconKind))
            {
                DrawServiceLogo(e.Graphics, circle, IconKind);
                return;
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, circle, Filled ? Color.White : AccentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawServiceLogo(Graphics graphics, Rectangle bounds, string kind)
        {
            var inset = Math.Max(7, bounds.Width / 5);
            var icon = Rectangle.Inflate(bounds, -inset, -inset);
            using var pen = new Pen(Color.White, Math.Max(2F, bounds.Width / 18F))
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };
            using var white = new SolidBrush(Color.White);

            if (kind == "Player")
            {
                var points = new[]
                {
                    new Point(icon.Left + icon.Width / 4, icon.Top),
                    new Point(icon.Right, icon.Top + icon.Height / 2),
                    new Point(icon.Left + icon.Width / 4, icon.Bottom)
                };
                graphics.FillPolygon(white, points);
                return;
            }

            if (kind == "EventSub")
            {
                var center = new Point(icon.Left + icon.Width / 2, icon.Top + icon.Height / 2);
                var nodes = new[]
                {
                    new Point(center.X, icon.Top),
                    new Point(icon.Left, icon.Bottom),
                    new Point(icon.Right, icon.Bottom)
                };
                foreach (var node in nodes) graphics.DrawLine(pen, center, node);
                var radius = Math.Max(3, icon.Width / 9);
                foreach (var node in nodes.Append(center))
                    graphics.FillEllipse(white, node.X - radius, node.Y - radius, radius * 2, radius * 2);
                return;
            }

            if (kind == "OBS")
            {
                var radius = icon.Width / 3;
                graphics.DrawEllipse(pen, icon.Left + icon.Width / 2 - radius / 2, icon.Top, radius, radius);
                graphics.DrawEllipse(pen, icon.Left, icon.Bottom - radius, radius, radius);
                graphics.DrawEllipse(pen, icon.Right - radius, icon.Bottom - radius, radius, radius);
                graphics.FillEllipse(white, icon.Left + icon.Width / 2 - 3, icon.Top + icon.Height / 2 - 3, 6, 6);
                return;
            }

            // Twitch: markante Chatblase mit den zwei typischen Balken.
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddLines(new[]
            {
                new Point(icon.Left, icon.Top), new Point(icon.Right, icon.Top),
                new Point(icon.Right, icon.Bottom - icon.Height / 4),
                new Point(icon.Left + icon.Width * 2 / 3, icon.Bottom - icon.Height / 4),
                new Point(icon.Left + icon.Width / 2, icon.Bottom),
                new Point(icon.Left + icon.Width / 2, icon.Bottom - icon.Height / 4),
                new Point(icon.Left, icon.Bottom - icon.Height / 4), new Point(icon.Left, icon.Top)
            });
            graphics.DrawPath(pen, path);
            graphics.DrawLine(pen, icon.Left + icon.Width / 3, icon.Top + icon.Height / 4,
                icon.Left + icon.Width / 3, icon.Top + icon.Height * 3 / 5);
            graphics.DrawLine(pen, icon.Left + icon.Width * 2 / 3, icon.Top + icon.Height / 4,
                icon.Left + icon.Width * 2 / 3, icon.Top + icon.Height * 3 / 5);
        }
    }
}







