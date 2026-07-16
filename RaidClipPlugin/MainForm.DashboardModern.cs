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
}

