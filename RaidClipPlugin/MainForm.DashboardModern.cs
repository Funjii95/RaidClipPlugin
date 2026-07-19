namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private static string GetServiceIcon(string service) => service switch
    {
        "OBS" => "◉",
        "Twitch" => "▣",
        "EventSub" => "✦",
        "Player" => "▶",
        _ => "●"
    };

    private static Color CardColor => Color.FromArgb(15, 20, 27);
    private static Color CardBorderColor => Color.FromArgb(42, 50, 63);

    private Control CreateDashboardHeader(Control header, Control updatePanel)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = BackgroundColor };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        header.Dock = DockStyle.Fill;
        header.Margin = new Padding(0, 0, 18, 0);
        if (updatePanel is ScrollableControl scroller) scroller.AutoScroll = false;
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(CreateUpdateStatusCard(), 1, 0);
        return layout;
    }

    private Control CreateUpdateStatusCard()
    {
        var card = CreateCardPanel(AccentColor, new Padding(18, 14, 18, 14));
        card.MinimumSize = new Size(320, 88);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.AutoSize = false;
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _versionLabel.Margin = Padding.Empty;
        _versionLabel.MaximumSize = Size.Empty;
        _versionLabel.Font = new Font("Segoe UI", 9.4F, FontStyle.Bold);
        _versionLabel.ForeColor = HealthyStatusColor;
        var buttonHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(4) };
        foreach (var button in new[] { _updateButton, _changelogButton, _installUpdateButton, _skipUpdateButton })
        {
  CompactDashboardButton(button, 0);
  button.Dock = DockStyle.Fill;
  button.Margin = Padding.Empty;
  buttonHost.Controls.Add(button);
  button.BringToFront();
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
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = indicators.Length, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };
        for (var index = 0; index < indicators.Length; index++)
        {
  grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / indicators.Length));
  grid.Controls.Add(CreateDashboardStatusCard(indicators[index]), index, 0);
        }
        return grid;
    }

    private Control CreateDashboardStatusCard(Label indicator)
    {
        var service = indicator.Text.Contains("OBS", StringComparison.OrdinalIgnoreCase) ? "OBS" : indicator.Text.Contains("Twitch", StringComparison.OrdinalIgnoreCase) ? "Twitch" : indicator.Text.Contains("EventSub", StringComparison.OrdinalIgnoreCase) ? "EventSub" : indicator.Text.Contains("Player", StringComparison.OrdinalIgnoreCase) ? "Player" : "Service";
        var card = CreateCardPanel(HealthyStatusColor, new Padding(14, 12, 14, 12));
        card.Margin = new Padding(6, 0, 6, 0);
        card.MinimumSize = new Size(150, 92);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = GetServiceIcon(service), Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 22F, FontStyle.Bold), ForeColor = service.Equals("Twitch", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(166, 96, 255) : AccentColor, BackColor = Color.Transparent }, 0, 0);
        indicator.Dock = DockStyle.Fill;
        indicator.AutoSize = false;
        indicator.BorderStyle = BorderStyle.None;
        indicator.BackColor = Color.Transparent;
        indicator.TextAlign = ContentAlignment.MiddleLeft;
        indicator.Font = new Font("Segoe UI", 10.2F, FontStyle.Bold);
        indicator.Padding = new Padding(2, 0, 0, 0);
        indicator.AutoEllipsis = true;
        indicator.Margin = Padding.Empty;
        layout.Controls.Add(indicator, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateDashboardActionBar(Control actions)
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        foreach (var percent in new[] { 15, 18, 18, 24, 13, 12 }) host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percent));
        if (actions is FlowLayoutPanel flow)
        {
  flow.AutoScroll = false;
  flow.WrapContents = false;
  var orderedControls = flow.Controls.Cast<Control>().Where(control => !ReferenceEquals(control, _overallStatusLabel)).Take(host.ColumnCount).ToArray();
  flow.Controls.Clear();
  for (var index = 0; index < orderedControls.Length; index++)
  {
      var child = orderedControls[index];
      child.Dock = DockStyle.Fill;
      child.Margin = new Padding(5, 4, 5, 4);
      if (child is Button button) { button.AutoSize = false; button.Height = 42; button.Padding = new Padding(6, 0, 6, 0); button.AutoEllipsis = true; }
      else if (child is TextBox textBox) { textBox.AutoSize = false; textBox.Height = 34; textBox.Margin = new Padding(5, 8, 5, 4); }
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
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(26, 22, 26, 22), BackColor = BackgroundColor, Margin = Padding.Empty };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = BackgroundColor, Margin = new Padding(0, 0, 18, 0), Padding = Padding.Empty };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.Controls.Add(dashboardHeader, 0, 0);
        main.Controls.Add(CreateHeroStatusCard(), 0, 1);
        main.Controls.Add(CreateDashboardSection("Module & Verbindungen", CreateModuleGrid()), 0, 2);
        main.Controls.Add(CreateDashboardSection("Heutige Statistiken", CreateDashboardStatsGrid()), 0, 3);
        main.Controls.Add(dashboardActions, 0, 4);
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackgroundColor, Margin = Padding.Empty, Padding = Padding.Empty };
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        side.Controls.Add(CreateDashboardSection("Systemprüfung", dashboardHealth), 0, 0);
        side.Controls.Add(CreateDashboardSection("Bot Log", CreateRecentActivityList()), 0, 1);
        page.Controls.Add(main, 0, 0);
        page.Controls.Add(side, 1, 0);
        return page;
    }

    private Control CreateHeroStatusCard()
    {
        var hero = CreateCardPanel(AccentColor, new Padding(26, 22, 26, 22));
        hero.Margin = new Padding(0, 0, 0, 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        layout.Controls.Add(new Label { Text = "R", Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 32F, FontStyle.Bold), ForeColor = AccentColor, BackColor = Color.Transparent }, 0, 0);
        var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = new Padding(8, 0, 0, 0), Padding = Padding.Empty };
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        text.Controls.Add(new Label { Text = "RaidClipPlugin", Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        _overallStatusLabel.Dock = DockStyle.Fill;
        _overallStatusLabel.AutoSize = false;
        _overallStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _overallStatusLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _overallStatusLabel.Margin = Padding.Empty;
        _overallStatusLabel.Padding = Padding.Empty;
        text.Controls.Add(_overallStatusLabel, 0, 1);
        text.Controls.Add(new Label { Text = "Alle Kernmodule bleiben getrennt und blockieren sich nicht gegenseitig.", Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 9.2F), ForeColor = MutedTextColor, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 0, 2);
        var meta = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        meta.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        meta.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        AddHeroMeta(meta, 0, "Kanal", "Funjii");
        AddHeroMeta(meta, 1, "Version", _updateService.CurrentDisplayVersion);
        AddHeroMeta(meta, 2, "Status", "Live");
        layout.Controls.Add(text, 1, 0);
        layout.Controls.Add(meta, 2, 0);
        hero.Controls.Add(layout);
        return hero;
    }

    private static void AddHeroMeta(TableLayoutPanel meta, int column, string title, string value)
    {
        meta.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, AutoSize = false, ForeColor = MutedTextColor, Font = new Font("Segoe UI", 8.4F), TextAlign = ContentAlignment.BottomCenter, AutoEllipsis = true }, column, 0);
        meta.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, AutoSize = false, ForeColor = TextColor, Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter, AutoEllipsis = true }, column, 1);
    }

    private Control CreateModuleGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        for (var i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.Controls.Add(CreateDashboardStatusCard(_twitchIndicator), 0, 0);
        grid.Controls.Add(CreateDashboardStatusCard(_obsIndicator), 1, 0);
        grid.Controls.Add(CreateDashboardStatusCard(_eventSubIndicator), 2, 0);
        grid.Controls.Add(CreateDashboardStatusCard(_playerIndicator), 3, 0);
        grid.Controls.Add(CreateModuleInfoCard("Discord", "Optional", "☁", WarningStatusColor), 0, 1);
        grid.Controls.Add(CreateModuleInfoCard("Musikdienst", "Optional", "♫", HealthyStatusColor), 1, 1);
        grid.Controls.Add(CreateModuleInfoCard("Commands", "Bereit", "</>", HealthyStatusColor), 2, 1);
        grid.Controls.Add(CreateModuleInfoCard("Updater", "Aktuell", "↻", HealthyStatusColor), 3, 1);
        return grid;
    }

    private Control CreateModuleInfoCard(string title, string status, string icon, Color color)
    {
        var card = CreateCardPanel(color, new Padding(14, 12, 14, 12));
        card.Margin = new Padding(6, 6, 6, 6);
        card.MinimumSize = new Size(150, 78);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = icon, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 19F, FontStyle.Bold), ForeColor = color, BackColor = Color.Transparent }, 0, 0);
        var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        text.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true }, 0, 0);
        text.Controls.Add(new Label { Text = status, Dock = DockStyle.Fill, AutoSize = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 0, 1);
        layout.Controls.Add(text, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateDashboardSection(string title, Control content)
    {
        var card = CreateCardPanel(AccentColor, new Padding(14, 10, 14, 14));
        card.Margin = new Padding(0, 0, 0, 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = TextColor, Font = new Font("Segoe UI", 9.8F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 0, 0);
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
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        for (var i = 0; i < 6; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));
        grid.Controls.Add(CreateStatisticCard("💬", "Chat", "—", AccentColor), 0, 0);
        grid.Controls.Add(CreateStatisticCard("</>", "Commands", "—", Color.FromArgb(255, 159, 67)), 1, 0);
        grid.Controls.Add(CreateStatisticCard("▣", "Raidclips", _historyList.Items.Count.ToString("N0"), AccentColor), 2, 0);
        grid.Controls.Add(CreateStatisticCard("👥", "Raids", "—", Color.FromArgb(166, 96, 255)), 3, 0);
        grid.Controls.Add(CreateStatisticCard("◴", "Punkte", "—", Color.FromArgb(245, 176, 65)), 4, 0);
        grid.Controls.Add(CreateStatisticCard("⚠", "Fehler", "Keine", UnhealthyStatusColor), 5, 0);
        return grid;
    }

    private Control CreateStatisticCard(string icon, string label, string value, Color color)
    {
        var panel = CreateCardPanel(color, new Padding(10, 8, 10, 8));
        panel.Margin = new Padding(5, 0, 5, 0);
        panel.MinimumSize = new Size(112, 82);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        layout.Controls.Add(new Label { Text = icon, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true }, 0, 0);
        layout.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = TextColor, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true }, 0, 1);
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.2F), ForeColor = MutedTextColor, TextAlign = ContentAlignment.TopCenter, AutoEllipsis = true }, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateDashboardHealthSummary()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        var summary = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        _moduleHealthOverallDot.Margin = new Padding(0, 8, 8, 0);
        _moduleHealthSummaryLabel.AutoSize = false;
        _moduleHealthSummaryLabel.Width = 220;
        _moduleHealthSummaryLabel.Height = 30;
        _moduleHealthSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _moduleHealthSummaryLabel.AutoEllipsis = true;
        _moduleHealthSummaryLabel.Margin = Padding.Empty;
        _moduleHealthProgressLabel.AutoSize = false;
        _moduleHealthProgressLabel.Width = 92;
        _moduleHealthProgressLabel.Height = 30;
        _moduleHealthProgressLabel.TextAlign = ContentAlignment.MiddleRight;
        _moduleHealthProgressLabel.AutoEllipsis = true;
        summary.Controls.Add(_moduleHealthOverallDot);
        summary.Controls.Add(_moduleHealthSummaryLabel);
        summary.Controls.Add(_moduleHealthProgressLabel);
        layout.Controls.Add(summary, 0, 0);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        CompactDashboardButton(_checkModulesButton, 0);
        CompactDashboardButton(_restartModulesButton, 0);
        _checkModulesButton.Text = "Jetzt prüfen";
        _restartModulesButton.Text = "Reparieren";
        _checkModulesButton.Dock = DockStyle.Fill;
        _restartModulesButton.Dock = DockStyle.Fill;
        _checkModulesButton.Margin = new Padding(0, 4, 5, 4);
        _restartModulesButton.Margin = new Padding(5, 4, 0, 4);
        buttons.Controls.Add(_checkModulesButton, 0, 0);
        buttons.Controls.Add(_restartModulesButton, 1, 0);
        layout.Controls.Add(buttons, 0, 1);
        var list = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Color.Transparent, Margin = new Padding(0, 8, 0, 0), Padding = Padding.Empty };
        for (var i = 0; i < 7; i++) list.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 7F));
        foreach (var row in new[] { ("Twitch API", "OK", HealthyStatusColor), ("Twitch Chat", "Bereit", HealthyStatusColor), ("EventSub", "Bereit", HealthyStatusColor), ("OBS Studio", "Bereit", HealthyStatusColor), ("Lokaler Player", "Bereit", HealthyStatusColor), ("Commands", "Bereit", HealthyStatusColor), ("Updater", "Aktuell", HealthyStatusColor) }) list.Controls.Add(CreateHealthRow(row.Item1, row.Item2, row.Item3));
        layout.Controls.Add(list, 0, 2);
        _moduleHealthLastCheckLabel.Dock = DockStyle.Fill;
        _moduleHealthLastCheckLabel.AutoSize = false;
        _moduleHealthLastCheckLabel.TextAlign = ContentAlignment.BottomRight;
        _moduleHealthLastCheckLabel.Margin = Padding.Empty;
        layout.Controls.Add(_moduleHealthLastCheckLabel, 0, 3);
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
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, BackColor = Color.Transparent, Padding = Padding.Empty, Margin = Padding.Empty };
        for (var i = 0; i < 8; i++) box.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        var rows = new[] { "● Healthcheck bereit", "● Twitch Chat wartet auf Verbindung", "● Raidclip-System bereit", "● Updater aktuell", "● Commands geladen", "● Punkte-System bereit", "● OBS wartet auf Start", "● Lokaler Player bereit" };
        foreach (var text in rows) box.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = text.Contains("wartet", StringComparison.OrdinalIgnoreCase) ? WarningStatusColor : MutedTextColor, Font = new Font("Segoe UI", 8.4F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true });
        return box;
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
  using var background = new SolidBrush(BackColor);
  e.Graphics.FillRectangle(background, bounds);
  using var border = new Pen(Color.FromArgb(42, 50, 63), 1F);
  e.Graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
  using var accent = new Pen(AccentColor, 2F);
  e.Graphics.DrawLine(accent, 12, bounds.Height - 2, bounds.Width - 12, bounds.Height - 2);
        }
    }
}
