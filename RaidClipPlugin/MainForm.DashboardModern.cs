namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private static string GetServiceIcon(string service) => service switch
    {
        "OBS" => "◉",
        "Twitch" => "▣",
        "EventSub" => "☆",
        "Player" => "▶",
        _ => "●"
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        header.Dock = DockStyle.Fill;
        header.Margin = new Padding(0, 0, 16, 0);
        if (updatePanel is ScrollableControl scrollableUpdatePanel)
        {
            scrollableUpdatePanel.AutoScroll = false;
        }

        var updateCard = CreateUpdateStatusCard();
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(updateCard, 1, 0);
        return layout;
    }

    private Control CreateUpdateStatusCard()
    {
        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(18, 14, 18, 14),
            Margin = Padding.Empty,
            MinimumSize = new Size(260, 82)
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.AutoSize = false;
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _versionLabel.Margin = Padding.Empty;

        var updateButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        CompactDashboardButton(_updateButton, 150);
        CompactDashboardButton(_changelogButton, 132);
        CompactDashboardButton(_installUpdateButton, 132);
        CompactDashboardButton(_skipUpdateButton, 112);
        updateButtons.Controls.Add(_updateButton);
        updateButtons.Controls.Add(_changelogButton);
        updateButtons.Controls.Add(_installUpdateButton);
        updateButtons.Controls.Add(_skipUpdateButton);

        layout.Controls.Add(_versionLabel, 0, 0);
        layout.Controls.Add(updateButtons, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private static void CompactDashboardButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 36;
        button.Margin = new Padding(4, 5, 0, 0);
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
            BackColor = BackgroundColor
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
            raw.Contains("LÄUFT", StringComparison.OrdinalIgnoreCase) || raw.Contains("LAEUFT", StringComparison.OrdinalIgnoreCase) ? "Läuft" :
            raw.Contains("BEREIT", StringComparison.OrdinalIgnoreCase) ? "Bereit" : "Bereit";

        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(6, 0, 6, 0),
            MinimumSize = new Size(150, 80)
        };

        var iconLabel = new Label
        {
            Text = icon,
            Dock = DockStyle.Left,
            Width = 42,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = AccentColor
        };
        var text = new Label
        {
            Text = $"{title}\n{state}",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.2F, FontStyle.Bold),
            ForeColor = HealthyStatusColor,
            Padding = new Padding(8, 4, 0, 0)
        };
        card.Controls.Add(text);
        card.Controls.Add(iconLabel);
        return card;
    }

    private Control CreateDashboardActionBar(Control actions)
    {
        actions.Dock = DockStyle.Fill;
        actions.Margin = Padding.Empty;
        if (actions is FlowLayoutPanel flow)
        {
            flow.AutoScroll = false;
            flow.Padding = new Padding(0);
            flow.WrapContents = true;
        }

        return CreateDashboardCard(actions, new Padding(0, 0, 0, 8));
    }

    private Control CreateDashboardSection(Control content, Padding margin)
    {
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;
        return CreateDashboardCard(content, margin);
    }

    private Control CreateDashboardCard(Control content, Padding margin)
    {
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

    private Control CreateModernDashboardLayout(
        Control dashboardHeader,
        Control dashboardIndicators,
        Control dashboardActions,
        Control dashboardHealth)
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18, 16, 18, 16),
            BackColor = BackgroundColor
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = BackgroundColor,
            Margin = new Padding(0, 0, 12, 0)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 15));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 23));

        left.Controls.Add(dashboardHeader, 0, 0);
        left.Controls.Add(CreateDashboardSection("Module & Verbindungen", dashboardIndicators), 0, 1);
        left.Controls.Add(CreateDashboardSection("Heutige Statistiken", CreateDashboardStatsGrid()), 0, 2);
        left.Controls.Add(CreateDashboardSection("Schnellaktionen", dashboardActions), 0, 3);
        left.Controls.Add(CreateDashboardSection("Aktive Module", CreateActiveModulesGrid()), 0, 4);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackgroundColor,
            Margin = Padding.Empty
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        right.Controls.Add(dashboardHealth, 0, 0);
        right.Controls.Add(CreateDashboardSection("Letzte Aktivitäten", CreateRecentActivityList()), 0, 1);

        page.Controls.Add(left, 0, 0);
        page.Controls.Add(right, 1, 0);
        return page;
    }

    private Control CreateDashboardSection(string title, Control content)
    {
        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(14, 12, 14, 14),
            Margin = new Padding(0, 0, 0, 10)
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
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;
        layout.Controls.Add(content, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateDashboardStatsGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = SurfaceColor,
            Padding = Padding.Empty
        };
        for (var i = 0; i < 6; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));

        grid.Controls.Add(CreateStatisticCard("💬", "Chat", "—"), 0, 0);
        grid.Controls.Add(CreateStatisticCard("</>", "Commands", "—"), 1, 0);
        grid.Controls.Add(CreateStatisticCard("🎬", "Raidclips", _historyList.Items.Count.ToString("N0")), 2, 0);
        grid.Controls.Add(CreateStatisticCard("👥", "Raids", "—"), 3, 0);
        grid.Controls.Add(CreateStatisticCard("⏱", "Aktivität", "—"), 4, 0);
        grid.Controls.Add(CreateStatisticCard("⚠", "Fehler", "Keine"), 5, 0);
        return grid;
    }

    private Control CreateStatisticCard(string icon, string label, string value)
    {
        var panel = new TableLayoutPanel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(16, 20, 24),
            Padding = new Padding(8),
            Margin = new Padding(4)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        panel.Controls.Add(new Label
        {
            Text = icon,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = AccentColor,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        }, 0, 1);
        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 7.8F),
            ForeColor = MutedTextColor,
            TextAlign = ContentAlignment.TopCenter,
            AutoEllipsis = true
        }, 0, 2);
        return panel;
    }

    private Control CreateActiveModulesGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = SurfaceColor,
            Padding = new Padding(4, 2, 4, 0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 4; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var items = new[]
        {
            "● Twitch Chat", "● Musik System",
            "● EventSub", "● Giveaway System",
            "● Raidclip System", "● Command System",
            "● Punkte System", "● Auto-Updater"
        };
        for (var i = 0; i < items.Length; i++)
        {
            grid.Controls.Add(new Label
            {
                Text = items[i],
                Dock = DockStyle.Fill,
                ForeColor = HealthyStatusColor,
                Font = new Font("Segoe UI", 8.4F),
                Padding = new Padding(4, 2, 4, 0),
                AutoEllipsis = true
            }, i % 2, i / 2);
        }
        return grid;
    }

    private Control CreateRecentActivityList()
    {
        var box = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = SurfaceColor,
            Padding = new Padding(0, 2, 0, 0)
        };
        for (var i = 0; i < 4; i++)
            box.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var rows = new[]
        {
            "● Healthcheck bereit",
            "● Twitch Chat wartet auf Verbindung",
            "● Raidclip-System bereit",
            "● Updater aktuell"
        };
        foreach (var text in rows)
        {
            box.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = MutedTextColor,
                Font = new Font("Segoe UI", 8.2F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            });
        }
        return box;
    }
}
