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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));

        header.Dock = DockStyle.Fill;
        header.Margin = new Padding(0, 0, 14, 10);
        updatePanel.Dock = DockStyle.Fill;
        updatePanel.Margin = new Padding(0, 0, 0, 10);
        updatePanel.MinimumSize = new Size(360, 90);
        updatePanel.BackColor = SurfaceColor;
        updatePanel.Padding = new Padding(18, 14, 16, 12);
        if (updatePanel is ScrollableControl scrollableUpdatePanel)
        {
            scrollableUpdatePanel.AutoScroll = false;
        }
        updatePanel.Controls.SetChildIndex(_versionLabel, 0);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(CreateDashboardCard(updatePanel, new Padding(0)), 1, 0);
        return layout;
    }

    private Control CreateDashboardIndicatorGrid(params Label[] indicators)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = indicators.Length,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 10),
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
        indicator.BorderStyle = BorderStyle.None;
        indicator.BackColor = SurfaceColor;
        indicator.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        indicator.TextAlign = ContentAlignment.MiddleCenter;
        indicator.Padding = new Padding(10);
        indicator.Margin = Padding.Empty;

        var card = new Panel
        {
            Name = "SurfacePanel",
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(12),
            Margin = new Padding(5, 0, 5, 0),
            MinimumSize = new Size(160, 82)
        };
        card.Controls.Add(indicator);
        return card;
    }

    private Control CreateDashboardActionBar(Control actions)
    {
        actions.Dock = DockStyle.Fill;
        actions.Margin = Padding.Empty;
        if (actions is FlowLayoutPanel flow)
        {
            flow.AutoScroll = true;
            flow.Padding = new Padding(8, 7, 8, 7);
            flow.WrapContents = true;
        }

        return CreateDashboardCard(actions, new Padding(0, 0, 0, 10));
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
  Padding = new Padding(20, 18, 20, 18),
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
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
  Margin = new Padding(0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
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
  Margin = new Padding(0, 0, 0, 12)
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
  Text = title,
  Dock = DockStyle.Fill,
  ForeColor = TextColor,
  Font = new Font("Segoe UI", 9.4F, FontStyle.Bold),
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

        grid.Controls.Add(CreateStatisticCard("💬", "Chatnachrichten", "—"), 0, 0);
        grid.Controls.Add(CreateStatisticCard("</>", "Commands", "—"), 1, 0);
        grid.Controls.Add(CreateStatisticCard("🎬", "Raidclips", _historyList.Items.Count.ToString("N0")), 2, 0);
        grid.Controls.Add(CreateStatisticCard("👥", "Raids heute", "—"), 3, 0);
        grid.Controls.Add(CreateStatisticCard("⏱", "Letzte Aktivität", "—"), 4, 0);
        grid.Controls.Add(CreateStatisticCard("⚠", "Fehler", "Keine"), 5, 0);
        return grid;
    }

    private Control CreateStatisticCard(string icon, string label, string value)
    {
        var panel = new Panel
        {
  Name = "SurfacePanel",
  Dock = DockStyle.Fill,
  BackColor = Color.FromArgb(16, 20, 24),
  Padding = new Padding(10),
  Margin = new Padding(4)
        };
        panel.Controls.Add(new Label
        {
  Text = icon,
  AutoSize = false,
  Location = new Point(10, 10),
  Size = new Size(38, 34),
  Font = new Font("Segoe UI", 15F, FontStyle.Bold),
  ForeColor = AccentColor,
  TextAlign = ContentAlignment.MiddleCenter
        });
        panel.Controls.Add(new Label
        {
  Text = value,
  AutoSize = false,
  Location = new Point(52, 12),
  Size = new Size(100, 26),
  Font = new Font("Segoe UI", 14F, FontStyle.Bold),
  ForeColor = TextColor
        });
        panel.Controls.Add(new Label
        {
  Text = label,
  AutoSize = false,
  Location = new Point(52, 42),
  Size = new Size(130, 36),
  Font = new Font("Segoe UI", 8F),
  ForeColor = MutedTextColor
        });
        return panel;
    }

    private Control CreateActiveModulesGrid()
    {
        var grid = new TableLayoutPanel
        {
  Dock = DockStyle.Top,
  ColumnCount = 2,
  RowCount = 4,
  BackColor = SurfaceColor,
  AutoSize = true
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
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
      Height = 26,
      ForeColor = items[i].StartsWith("●") ? HealthyStatusColor : MutedTextColor,
      Font = new Font("Segoe UI", 8.8F),
      Padding = new Padding(4, 4, 4, 0)
  }, i % 2, i / 2);
        }
        return grid;
    }

    private Control CreateRecentActivityList()
    {
        var box = new FlowLayoutPanel
        {
  Dock = DockStyle.Fill,
  FlowDirection = FlowDirection.TopDown,
  WrapContents = false,
  AutoScroll = true,
  BackColor = SurfaceColor,
  Padding = new Padding(0, 2, 0, 0)
        };
        foreach (var text in new[]
        {
  "Healthcheck bereit",
  "Twitch Chat wartet auf Verbindung",
  "Raidclip-System bereit",
  "Updater aktuell"
        })
        {
  box.Controls.Add(new Label
  {
      Text = "●  " + text,
      AutoSize = false,
      Width = 320,
      Height = 28,
      ForeColor = MutedTextColor,
      Font = new Font("Segoe UI", 8.5F),
      Margin = new Padding(0, 2, 0, 2)
  });
        }
        return box;
    }

}

