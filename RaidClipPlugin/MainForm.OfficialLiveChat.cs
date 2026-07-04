using Microsoft.Web.WebView2.WinForms;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Button _liveChatNavButton = CreateNavigationTile(
        "💬  Livechat", "Twitch-Chat und Popout");
    private readonly Panel _liveChatPage = new()
    {
        Name = "LiveChatPage",
        Dock = DockStyle.Fill,
        Visible = false
    };
    private readonly WebView2 _officialChatWebView = new()
    {
        Name = "OfficialTwitchChatWebView",
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(12, 12, 14)
    };
    private readonly Button _officialChatConnectButton =
        NewHeistActionButton("Chat verbinden", 145);
    private readonly Button _officialChatDisconnectButton =
        NewHeistActionButton("Chat trennen", 130);
    private readonly Button _officialChatPopoutButton =
        NewHeistActionButton("Popout öffnen", 135);
    private readonly CheckBox _officialChatTopMostCheck =
        NewCheck("Immer im Vordergrund", false);
    private readonly Label _officialChatStatusLabel = new()
    {
        AutoSize = true,
        Text = "● Nicht verbunden",
        ForeColor = InactiveColor,
        Margin = new Padding(8, 13, 12, 4)
    };
    private readonly Label _officialChatErrorLabel = new()
    {
        AutoSize = true,
        ForeColor = ErrorColor,
        Padding = new Padding(8, 5, 8, 5),
        Visible = false
    };
    private readonly Label _officialChatPopoutStatusLabel = new()
    {
        AutoSize = true,
        Text = "Popout: geschlossen",
        ForeColor = MutedTextColor,
        Padding = new Padding(8, 5, 8, 5)
    };
    private readonly TwitchChatWebViewService _officialChatService = new();
    private readonly System.Windows.Forms.Timer _officialChatSaveTimer =
        new() { Interval = 650 };
    private ChatPopoutForm? _chatPopout;
    private Rectangle _chatPopoutBounds = new(-1, -1, 520, 760);

    private Control BuildLiveChatSection()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Name = "LiveChatViews" };
        tabs.TabPages.Add(BuildLiveChatTab());
        tabs.TabPages.Add(BuildOfficialTwitchChatTab());
        return tabs;
    }

    private TabPage BuildOfficialTwitchChatTab()
    {
        var page = new TabPage("Offizieller Twitch-Chat")
        {
            BackColor = SurfaceColor,
            ForeColor = TextColor,
            Padding = new Padding(8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(4)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var toolbar = new FlowLayoutPanel
        {
            Name = "OfficialLiveChatToolbar",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoScroll = true,
            WrapContents = true,
            Padding = new Padding(2),
            MinimumSize = new Size(0, 48)
        };
        toolbar.Controls.AddRange(new Control[]
        {
            _officialChatStatusLabel,
            _officialChatConnectButton,
            _officialChatDisconnectButton,
            _officialChatPopoutButton,
            _officialChatTopMostCheck
        });
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_officialChatErrorLabel, 0, 1);
        layout.Controls.Add(_officialChatWebView, 0, 2);
        layout.Controls.Add(_officialChatPopoutStatusLabel, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private void InitializeOfficialLiveChatEvents()
    {
        _officialChatService.StateChanged += UpdateOfficialChatStatus;
        _officialChatConnectButton.Click += async (_, _) =>
            await ConnectOfficialLiveChatAsync();
        _officialChatDisconnectButton.Click += async (_, _) =>
            await DisconnectOfficialLiveChatAsync();
        _officialChatPopoutButton.Click += (_, _) => OpenOfficialChatPopout();
        _officialChatTopMostCheck.CheckedChanged += (_, _) =>
        {
            _chatPopout?.SetTopMost(_officialChatTopMostCheck.Checked);
            ScheduleOfficialChatSettingsSave();
        };
        _officialChatSaveTimer.Tick += (_, _) =>
        {
            _officialChatSaveTimer.Stop();
            TryPersistOfficialChatSettings();
        };
    }

    private async Task ConnectOfficialLiveChatAsync()
    {
        _officialChatErrorLabel.Visible = false;
        try
        {
            await _officialChatService.ConnectAsync(_officialChatWebView,
                _twitchChannelBox.Text, CancellationToken.None);
        }
        catch (Exception exception)
        {
            ShowOfficialChatError(exception.Message);
            AppendLog("Offizieller Twitch-Chat: " + exception.Message);
        }
    }

    private async Task DisconnectOfficialLiveChatAsync()
    {
        try
        {
            await _officialChatService.DisconnectAsync();
            _officialChatErrorLabel.Visible = false;
        }
        catch (Exception exception)
        {
            ShowOfficialChatError(exception.Message);
            AppendLog("Twitch-Chat konnte nicht getrennt werden: " + exception.Message);
        }
    }

    private void OpenOfficialChatPopout()
    {
        var channel = _twitchChannelBox.Text.Trim();
        if (channel.Length == 0)
        {
            ShowOfficialChatError("Kein Twitch-Kanal konfiguriert.");
            return;
        }
        if (_chatPopout is { IsDisposed: false })
        {
            if (_chatPopout.WindowState == FormWindowState.Minimized)
                _chatPopout.WindowState = FormWindowState.Normal;
            _chatPopout.BringToFront();
            _chatPopout.Activate();
            return;
        }

        var config = ReadLiveChatConfig();
        _chatPopout = new ChatPopoutForm(channel, config,
            CaptureOfficialChatPopoutSettings, BackgroundColor, SurfaceColor,
            TextColor, AccentColor);
        _chatPopout.FormClosed += (_, _) =>
        {
            _chatPopout = null;
            _officialChatPopoutStatusLabel.Text = "Popout: geschlossen";
            _officialChatPopoutStatusLabel.ForeColor = MutedTextColor;
        };
        _chatPopout.Show(this);
        _officialChatPopoutStatusLabel.Text = "Popout: geöffnet";
        _officialChatPopoutStatusLabel.ForeColor = ActiveColor;
    }

    private void CaptureOfficialChatPopoutSettings(Rectangle bounds, bool topMost)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
                CaptureOfficialChatPopoutSettings(bounds, topMost)));
            return;
        }
        _chatPopoutBounds = bounds;
        if (_officialChatTopMostCheck.Checked != topMost)
            _officialChatTopMostCheck.Checked = topMost;
        ScheduleOfficialChatSettingsSave();
    }

    private void ScheduleOfficialChatSettingsSave()
    {
        if (!IsHandleCreated || IsDisposed) return;
        _officialChatSaveTimer.Stop();
        _officialChatSaveTimer.Start();
    }

    private void TryPersistOfficialChatSettings()
    {
        try
        {
            _configurationService.SaveGuiSettings(ReadSettingsFromControls());
        }
        catch (Exception exception)
        {
            AppendLog("Livechat-Fensterposition konnte nicht gespeichert werden: " +
                exception.Message);
        }
    }

    private void UpdateOfficialChatStatus(TwitchChatConnectionState state,
        string? error)
    {
        if (IsDisposed) return;
        _officialChatStatusLabel.Text = state switch
        {
            TwitchChatConnectionState.Connecting => "● Wird verbunden …",
            TwitchChatConnectionState.Connected => "● Verbunden",
            TwitchChatConnectionState.Error => "● Fehler",
            _ => "● Nicht verbunden"
        };
        _officialChatStatusLabel.ForeColor = state switch
        {
            TwitchChatConnectionState.Connected => ActiveColor,
            TwitchChatConnectionState.Error => ErrorColor,
            TwitchChatConnectionState.Connecting => WaitingColor,
            _ => InactiveColor
        };
        if (!string.IsNullOrWhiteSpace(error)) ShowOfficialChatError(error);
    }

    private void ShowOfficialChatError(string message)
    {
        _officialChatErrorLabel.Text = message;
        _officialChatErrorLabel.Visible = true;
    }

    private void CloseOfficialLiveChat()
    {
        _officialChatSaveTimer.Stop();
        if (_chatPopout is { IsDisposed: false }) _chatPopout.Close();
        _chatPopout = null;
        _officialChatService.Dispose();
        _officialChatWebView.Dispose();
        _officialChatSaveTimer.Dispose();
    }
}
