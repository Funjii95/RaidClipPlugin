namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private static string GetServiceIcon(string service) => service switch
    {
        "OBS" => "OBS",
        "Twitch" => "TW",
        "EventSub" => "EV",
        "Player" => "PL",
        _ => "OK"
    };

    private Control CreateDashboardHeader(Control header, Control updatePanel)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = BackgroundColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        header.Dock = DockStyle.Fill;
        header.Margin = new Padding(0, 0, 14, 0);
        if (updatePanel is ScrollableControl scroller) scroller.AutoScroll = false;

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(CreateUpdateStatusCard(), 1, 0);
        return layout;
    }

    private Control CreateUpdateStatusCard()
    {
        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(14, 10, 14, 10),
            Margin = Padding.Empty,
            MinimumSize = new Size(420, 92)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.AutoSize = false;
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _versionLabel.Margin = new Padding(0, 0, 8, 0);
        _versionLabel.MaximumSize = Size.Empty;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        CompactDashboardButton(_updateButton, 160);
        CompactDashboardButton(_changelogButton, 132);
        CompactDashboardButton(_installUpdateButton, 140);
        CompactDashboardButton(_skipUpdateButton, 104);
        _updateButton.Margin = new Padding(3, 3, 3, 3);
        _changelogButton.Margin = new Padding(3, 3, 3, 3);
        _installUpdateButton.Margin = new Padding(3, 3, 3, 3);
        _skipUpdateButton.Margin = new Padding(3, 3, 3, 3);

        buttons.Controls.Add(_updateButton);
        buttons.Controls.Add(_changelogButton);
        buttons.Controls.Add(_installUpdateButton);
        buttons.Controls.Add(_skipUpdateButton);

        layout.Controls.Add(_versionLabel, 0, 0);
        layout.Controls.Add(buttons, 1, 0);
        card.Controls.Add(layout);
        return card;
    }


    private static void CompactDashboardButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 32;
        button.Margin = new Padding(3, 0, 3, 0);
        button.Padding = new Padding(8, 0, 8, 0);
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    private Control CreateDashboardIndicatorGrid(params Label[] indicators)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = indicators.Length,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = SurfaceColor
        };

        for (var index = 0; index < indicators.Length; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / indicators.Length));
            grid.Controls.Add(CreateDashboardStatusCard(indicators[index]), index, 0);
        }

        return grid;
    }

    private Control CreateDashboardStatusCard(Label indicator)
    {
        var raw = indicator.Text.Replace("\r", " ").Replace("\n", " ");
        var icon = GetServiceIcon(raw.Contains("OBS", StringComparison.OrdinalIgnoreCase) ? "OBS" :
            raw.Contains("Twitch", StringComparison.OrdinalIgnoreCase) ? "Twitch" :
            raw.Contains("EventSub", StringComparison.OrdinalIgnoreCase) ? "EventSub" :
            raw.Contains("Player", StringComparison.OrdinalIgnoreCase) ? "Player" : string.Empty);
        var title = raw.Contains("OBS", StringComparison.OrdinalIgnoreCase) ? "OBS" :
            raw.Contains("Twitch", StringComparison.OrdinalIgnoreCase) ? "Twitch" :
            raw.Contains("EventSub", StringComparison.OrdinalIgnoreCase) ? "EventSub" :
            raw.Contains("Player", StringComparison.OrdinalIgnoreCase) ? "Player" : raw;
        var state = raw.Contains("VERBUNDEN", StringComparison.OrdinalIgnoreCase) ? "Verbunden" :
            raw.Contains("AKTIV", StringComparison.OrdinalIgnoreCase) ? "Aktiv" :
            raw.Contains("LAEUFT", StringComparison.OrdinalIgnoreCase) || raw.Contains("LÄUFT", StringComparison.OrdinalIgnoreCase) ? "Laeuft" :
            raw.Contains("BEREIT", StringComparison.OrdinalIgnoreCase) ? "Bereit" : "Bereit";

        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(16, 20, 24),
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(4, 0, 4, 0),
            MinimumSize = new Size(130, 78)
        };
        card.Controls.Add(new Label
        {
            Text = $"{icon}  {title}\n{state}",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9.8F, FontStyle.Bold),
            ForeColor = HealthyStatusColor,
            Padding = new Padding(2),
            AutoEllipsis = true
        });
        return card;
    }

    private Control CreateDashboardActionBar(Control actions)
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));

        if (actions is FlowLayoutPanel flow)
        {
            flow.AutoScroll = false;
            flow.WrapContents = false;
            var orderedControls = flow.Controls.Cast<Control>().ToArray();
            flow.Controls.Clear();
            for (var index = 0; index < Math.Min(orderedControls.Length, host.ColumnCount); index++)
            {
                var child = orderedControls[index];
                child.Dock = DockStyle.Fill;
                child.Margin = new Padding(4, 3, 4, 3);
                if (child is Button button)
                {
                    button.AutoSize = false;
                    button.Height = 36;
                    button.Padding = new Padding(4, 0, 4, 0);
                }
                else if (child is TextBox textBox)
                {
                    textBox.AutoSize = false;
                    textBox.Height = 32;
                }
                host.Controls.Add(child, index, 0);
            }
        }
        else
        {
            actions.Dock = DockStyle.Fill;
            host.Controls.Add(actions, 0, 0);
            host.SetColumnSpan(actions, host.ColumnCount);
        }

        return CreateDashboardSection("Schnellaktionen", host);
    }

    private Control CreateModernDashboardLayout(Control dashboardHeader, Control dashboardIndicators, Control dashboardActions, Control dashboardHealth)
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = BackgroundColor,
            Margin = Padding.Empty
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(dashboardHeader, 0, 0);
        page.Controls.Add(CreateDashboardSection("Module & Verbindungen", dashboardIndicators), 0, 1);
        page.Controls.Add(CreateDashboardSection("Heutige Statistiken", CreateDashboardStatsGrid()), 0, 2);
        page.Controls.Add(dashboardActions, 0, 3);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        bottom.Controls.Add(CreateDashboardSection("Aktive Module", CreateActiveModulesGrid()), 0, 0);
        bottom.Controls.Add(CreateDashboardSection("Systemstatus", dashboardHealth), 1, 0);
        bottom.Controls.Add(CreateDashboardSection("Letzte Aktivitaeten", CreateRecentActivityList()), 2, 0);
        page.Controls.Add(bottom, 0, 4);
        return page;
    }

    private Control CreateDashboardSection(string title, Control content)
    {
        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(12, 10, 12, 12),
            Margin = new Padding(0, 0, 0, 8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 0, 0);
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
        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(10),
            Margin = margin
        };
        card.Controls.Add(content);
        return card;
    }

    private Control CreateDashboardStatsGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = SurfaceColor, Padding = Padding.Empty, Margin = Padding.Empty };
        for (var i = 0; i < 6; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));
        grid.Controls.Add(CreateStatisticCard("CHAT", "Chat", "-"), 0, 0);
        grid.Controls.Add(CreateStatisticCard("CMD", "Commands", "-"), 1, 0);
        grid.Controls.Add(CreateStatisticCard("CLIP", "Raidclips", _historyList.Items.Count.ToString("N0")), 2, 0);
        grid.Controls.Add(CreateStatisticCard("RAID", "Raids", "-"), 3, 0);
        grid.Controls.Add(CreateStatisticCard("TIME", "Aktivitaet", "-"), 4, 0);
        grid.Controls.Add(CreateStatisticCard("WARN", "Fehler", "Keine"), 5, 0);
        return grid;
    }

    private Control CreateStatisticCard(string icon, string label, string value)
    {
        var panel = new TableLayoutPanel { Name = "SurfacePanel", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.FromArgb(16, 20, 24), Padding = new Padding(8), Margin = new Padding(4, 0, 4, 0), MinimumSize = new Size(110, 78) };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        panel.Controls.Add(new Label { Text = icon, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.8F, FontStyle.Bold), ForeColor = AccentColor, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true }, 0, 0);
        panel.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true }, 0, 1);
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8F), ForeColor = MutedTextColor, TextAlign = ContentAlignment.TopCenter, AutoEllipsis = true }, 0, 2);
        return panel;
    }

    private Control CreateDashboardHealthSummary()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var summary = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _moduleHealthOverallDot.Margin = new Padding(0, 6, 6, 0);
        _moduleHealthSummaryLabel.AutoSize = false;
        _moduleHealthSummaryLabel.Width = 210;
        _moduleHealthSummaryLabel.Height = 24;
        _moduleHealthSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _moduleHealthSummaryLabel.AutoEllipsis = true;
        _moduleHealthProgressLabel.AutoSize = false;
        _moduleHealthProgressLabel.Width = 90;
        _moduleHealthProgressLabel.Height = 24;
        _moduleHealthProgressLabel.TextAlign = ContentAlignment.MiddleRight;
        _moduleHealthProgressLabel.AutoEllipsis = true;
        summary.Controls.Add(_moduleHealthOverallDot);
        summary.Controls.Add(_moduleHealthSummaryLabel);
        summary.Controls.Add(_moduleHealthProgressLabel);
        layout.Controls.Add(summary, 0, 0);

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        CompactDashboardButton(_checkModulesButton, 110);
        CompactDashboardButton(_restartModulesButton, 130);
        _checkModulesButton.Dock = DockStyle.Fill;
        _restartModulesButton.Dock = DockStyle.Fill;
        _checkModulesButton.Margin = new Padding(0, 0, 5, 0);
        _restartModulesButton.Margin = new Padding(5, 0, 0, 0);
        buttons.Controls.Add(_checkModulesButton, 0, 0);
        buttons.Controls.Add(_restartModulesButton, 1, 0);
        layout.Controls.Add(buttons, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, BackColor = SurfaceColor, Margin = Padding.Empty, Padding = Padding.Empty };
        var essentials = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = MutedTextColor,
            Font = new Font("Segoe UI", 8F),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = "OBS · Chat · EventSub · Player · Commands · Punkte"
        };
        _moduleHealthLastCheckLabel.Dock = DockStyle.Bottom;
        _moduleHealthLastCheckLabel.AutoSize = false;
        _moduleHealthLastCheckLabel.Height = 18;
        _moduleHealthLastCheckLabel.TextAlign = ContentAlignment.BottomRight;
        footer.Controls.Add(essentials);
        footer.Controls.Add(_moduleHealthLastCheckLabel);
        layout.Controls.Add(footer, 0, 2);
        return layout;
    }

    private Control CreateActiveModulesGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = SurfaceColor, Padding = new Padding(2), Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var items = new[] { "Twitch Chat", "Musik System", "EventSub", "Giveaway", "Raidclip", "Commands", "Punkte", "Updater" };
        for (var i = 0; i < items.Length; i++) grid.Controls.Add(new Label { Text = items[i], Dock = DockStyle.Fill, ForeColor = HealthyStatusColor, Font = new Font("Segoe UI", 7.8F), Padding = new Padding(2, 1, 2, 0), AutoEllipsis = true }, i % 2, i / 2);
        return grid;
    }

    private Control CreateRecentActivityList()
    {
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = SurfaceColor, Padding = Padding.Empty, Margin = Padding.Empty };
        for (var i = 0; i < 4; i++) box.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var rows = new[] { "Healthcheck bereit", "Twitch Chat bereit", "Raidclip-System bereit", "Updater aktuell" };
        foreach (var text in rows) box.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 7.8F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true });
        return box;
    }
}
