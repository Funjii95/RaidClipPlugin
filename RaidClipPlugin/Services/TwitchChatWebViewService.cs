using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public enum TwitchChatConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public sealed class TwitchChatWebViewService : IDisposable
{
    private const string IdlePage = """
        <!doctype html><html><body style="margin:0;background:#0c0c0e;color:#aaa;font-family:Segoe UI,sans-serif;display:grid;place-items:center;height:100vh">
        <div>Chat ist nicht verbunden.</div></body></html>
        """;
    private static readonly object EnvironmentSync = new();
    private static Task<CoreWebView2Environment>? _environmentTask;
    private readonly ChatExtensionManager _extensions = new();
    private WebView2? _webView;
    private bool _disposed;

    public TwitchChatConnectionState State { get; private set; } =
        TwitchChatConnectionState.Disconnected;
    public bool IsConnected => State == TwitchChatConnectionState.Connected;
    public event Action<TwitchChatConnectionState, string?>? StateChanged;
    public event Action<string>? ExtensionsChanged;

    public static Uri CreateChatUri(string channelName)
    {
        var channel = (channelName ?? "").Trim().TrimStart('@').ToLowerInvariant();
        if (channel.Length == 0)
            throw new InvalidOperationException("Kein Twitch-Kanal konfiguriert.");
        if (channel.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            throw new InvalidOperationException("Der Twitch-Kanalname enthält ungültige Zeichen.");
        return new Uri($"https://www.twitch.tv/popout/{Uri.EscapeDataString(channel)}/chat?popout=");
    }

    public async Task ConnectAsync(WebView2 webView, string channelName,
        LiveChatConfig config, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var target = CreateChatUri(channelName);
        Attach(webView);
        SetState(TwitchChatConnectionState.Connecting, null);
        try
        {
            var environment = await GetEnvironmentAsync();
            await webView.EnsureCoreWebView2Async(environment);
            cancellationToken.ThrowIfCancellationRequested();
            var extensionStatus = await _extensions.ApplyAsync(
                webView.CoreWebView2.Profile, config, cancellationToken);
            ExtensionsChanged?.Invoke(extensionStatus);
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.Source = target;
        }
        catch (OperationCanceledException)
        {
            SetState(TwitchChatConnectionState.Disconnected, null);
            throw;
        }
        catch (Exception exception)
        {
            SetState(TwitchChatConnectionState.Error,
                "Twitch-Chat konnte nicht geladen werden: " + exception.Message);
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        if (_webView?.CoreWebView2 is not null)
            _webView.NavigateToString(IdlePage);
        SetState(TwitchChatConnectionState.Disconnected, null);
        return Task.CompletedTask;
    }

    private void Attach(WebView2 webView)
    {
        if (ReferenceEquals(_webView, webView)) return;
        if (_webView is not null)
            _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView = webView;
        _webView.NavigationCompleted += OnNavigationCompleted;
    }

    private void OnNavigationCompleted(object? sender,
        CoreWebView2NavigationCompletedEventArgs args)
    {
        if (State == TwitchChatConnectionState.Disconnected) return;
        if (args.IsSuccess)
            SetState(TwitchChatConnectionState.Connected, null);
        else
            SetState(TwitchChatConnectionState.Error,
                $"Twitch-Chat meldet den Ladefehler {args.WebErrorStatus}.");
    }

    private void SetState(TwitchChatConnectionState state, string? error)
    {
        State = state;
        StateChanged?.Invoke(state, error);
    }

    private static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        lock (EnvironmentSync)
            return _environmentTask ??= CreateEnvironmentAsync();
    }

    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin", "WebView2");
        var options = new CoreWebView2EnvironmentOptions
        {
            AreBrowserExtensionsEnabled = true
        };
        return CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_webView is not null)
            _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView = null;
        _disposed = true;
    }
}
