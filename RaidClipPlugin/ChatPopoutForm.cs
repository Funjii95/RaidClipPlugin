using Microsoft.Web.WebView2.WinForms;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed class ChatPopoutForm : Form
{
    private readonly string _channelName;
    private readonly LiveChatConfig _config;
    private readonly Action<Rectangle, bool> _settingsChanged;
    private readonly TwitchChatWebViewService _chatService = new();
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new()
    {
        Dock = DockStyle.Top,
        Height = 54,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 10, 0)
    };
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 500 };
    private bool _shown;
    private TwitchChatConnectionState _connectionState;
    private string? _connectionError;
    private string _extensionStatus = "7TV/BTTV: wird vorbereitet …";

    public ChatPopoutForm(string channelName, LiveChatConfig config,
        Action<Rectangle, bool> settingsChanged, Color background,
        Color surface, Color foreground, Color accent)
    {
        _channelName = channelName;
        _config = config;
        _settingsChanged = settingsChanged;
        Text = $"RaidClip Livechat – {channelName}";
        MinimumSize = new Size(360, 420);
        Size = new Size(config.PopoutWidth, config.PopoutHeight);
        TopMost = config.PopoutTopMost;
        BackColor = background;
        ForeColor = foreground;
        _statusLabel.BackColor = surface;
        _statusLabel.ForeColor = accent;
        _statusLabel.Text = "● Nicht verbunden";
        Controls.Add(_webView);
        Controls.Add(_statusLabel);

        var requested = new Rectangle(config.PopoutLeft, config.PopoutTop,
            config.PopoutWidth, config.PopoutHeight);
        if (config.PopoutLeft >= 0 && config.PopoutTop >= 0 && IsVisible(requested))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = requested;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }

        _chatService.StateChanged += UpdateStatus;
        _chatService.ExtensionsChanged += status =>
        {
            _extensionStatus = status;
            RenderStatus();
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            PersistBounds();
        };
    }

    public void SetTopMost(bool value)
    {
        TopMost = value;
        QueuePersist();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _shown = true;
        try
        {
            await _chatService.ConnectAsync(_webView, _channelName, _config,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            UpdateStatus(TwitchChatConnectionState.Error,
                "Twitch-Chat konnte nicht geladen werden: " + exception.Message);
        }
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        QueuePersist();
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        QueuePersist();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _saveTimer.Stop();
        PersistBounds();
        _chatService.Dispose();
        _webView.Dispose();
        _saveTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void QueuePersist()
    {
        if (!_shown || WindowState != FormWindowState.Normal) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void PersistBounds()
    {
        if (!_shown) return;
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height)
            _settingsChanged(bounds, TopMost);
    }

    private void UpdateStatus(TwitchChatConnectionState state, string? error)
    {
        if (IsDisposed) return;
        _connectionState = state;
        _connectionError = error;
        RenderStatus();
    }

    private void RenderStatus()
    {
        var connection = _connectionState switch
        {
            TwitchChatConnectionState.Connecting => "● Wird verbunden …",
            TwitchChatConnectionState.Connected => "● Verbunden",
            TwitchChatConnectionState.Error => "● Fehler: " + _connectionError,
            _ => "● Nicht verbunden"
        };
        _statusLabel.Text = connection + Environment.NewLine + _extensionStatus;
    }

    internal static bool IsVisible(Rectangle bounds) =>
        bounds.Width > 0 && bounds.Height > 0 &&
        Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds));
}
