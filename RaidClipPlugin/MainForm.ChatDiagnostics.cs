using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private ChatConnectionDiagnostics? _lastChatConnectionStatus;
    private readonly GroupBox _chatDiagnosticsGroup = new()
    {
        Text = "Chatbot-Diagnose",
        Dock = DockStyle.Fill,
        Padding = new Padding(10)
    };

    private readonly Label _chatOAuthDiagnostic = NewDiagnosticLabel("Twitch OAuth: nicht verbunden");
    private readonly Label _chatAccountDiagnostic = NewDiagnosticLabel("Konto: –");
    private readonly Label _chatScopesDiagnostic = NewDiagnosticLabel("Chat-Scopes: –");
    private readonly Label _chatTargetDiagnostic = NewDiagnosticLabel("Zielkanal: –");
    private readonly Label _chatWebSocketDiagnostic = NewDiagnosticLabel("EventSub WebSocket: getrennt");
    private readonly Label _chatSubscriptionDiagnostic = NewDiagnosticLabel("channel.chat.message: nicht aktiv");
    private readonly Label _chatLastReceivedDiagnostic = NewDiagnosticLabel("Letzter Empfang: –");
    private readonly Label _chatLastSentDiagnostic = NewDiagnosticLabel("Letzter Versand: –");
    private readonly Label _chatLastErrorDiagnostic = NewDiagnosticLabel("Letzter Fehler: –");
    private readonly Button _restartChatButton = NewDiagnosticButton("Chatverbindung neu starten");
    private readonly Button _reauthorizeTwitchButton = NewDiagnosticButton("Twitch neu autorisieren");
    private readonly Button _testChatButton = NewDiagnosticButton("Testnachricht senden");

    private static Label NewDiagnosticLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(470, 0),
        ForeColor = MutedTextColor,
        Margin = new Padding(6, 4, 12, 4)
    };

    private static Button NewDiagnosticButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(9, 4, 9, 4),
        Margin = new Padding(5)
    };

    private void BuildChatDiagnosticsPanel()
    {
        if (_chatDiagnosticsGroup.Controls.Count > 0) return;

        var values = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };
        values.Controls.AddRange(new Control[]
        {
            _chatOAuthDiagnostic, _chatAccountDiagnostic,
            _chatScopesDiagnostic, _chatTargetDiagnostic,
            _chatWebSocketDiagnostic, _chatSubscriptionDiagnostic,
            _chatLastReceivedDiagnostic, _chatLastSentDiagnostic,
            _chatLastErrorDiagnostic
        });

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        actions.Controls.AddRange(new Control[]
        {
            _restartChatButton, _reauthorizeTwitchButton, _testChatButton
        });

        _chatDiagnosticsGroup.Controls.Add(values);
        _chatDiagnosticsGroup.Controls.Add(actions);
    }

    private void InitializeChatDiagnosticsEvents()
    {
        _restartChatButton.Click += async (_, _) =>
        {
            try
            {
                await StopPluginAsync();
                await StartPluginAsync();
            }
            catch (Exception exception)
            {
                SetChatDiagnosticError(exception.Message);
                AppendLog("Chatverbindung konnte nicht neu gestartet werden: " + exception.Message);
            }
        };

        _reauthorizeTwitchButton.Click += async (_, _) =>
        {
            try
            {
                await StopPluginAsync();
                var config = ReadSettingsFromControls();
                new AuthenticationService(config).DeleteSavedToken();
                AppendLog("Twitch-Anmeldung wurde entfernt. Neue Autorisierung wird gestartet.");
                await StartPluginAsync();
            }
            catch (Exception exception)
            {
                SetChatDiagnosticError(exception.Message);
                AppendLog("Twitch-Neuanmeldung fehlgeschlagen: " + exception.Message);
            }
        };

        _testChatButton.Click += async (_, _) => await SendChatDiagnosticTestAsync();
    }

    private async Task SendChatDiagnosticTestAsync()
    {
        if (_twitch is null || _twitchSession is null || _broadcaster is null)
        {
            SetChatDiagnosticError("Plugin und Chatbot müssen zuerst gestartet werden.");
            AppendLog("Chat-Test nicht möglich: Twitch ist nicht vollständig verbunden.");
            return;
        }

        _testChatButton.Enabled = false;
        try
        {
            await _twitch.SendChatMessageAsync(
                _broadcaster.Id,
                _twitchSession.UserId,
                "✅ Chatbot-Verbindung erfolgreich.",
                _shutdown?.Token ?? CancellationToken.None);
            var now = DateTimeOffset.Now;
            _chatLastSentDiagnostic.Text = "Letzter Versand: " + now.ToString("HH:mm:ss");
            _chatLastSentDiagnostic.ForeColor = ActiveColor;
            _chatLastErrorDiagnostic.Text = "Letzter Fehler: –";
            AppendLog("Chatbot-Testnachricht wurde erfolgreich gesendet.");
        }
        catch (Exception exception)
        {
            SetChatDiagnosticError(exception.Message);
            AppendLog("Chatbot-Testnachricht fehlgeschlagen: " + exception.Message);
        }
        finally
        {
            _testChatButton.Enabled = true;
        }
    }

    private void UpdateChatAuthenticationDiagnostics(
        TwitchSession session,
        TwitchUser broadcaster)
    {
        RunOnUiThread(() =>
        {
            var chatScopes = AuthenticationService.RequiredChatScopes;
            var missing = chatScopes.Where(scope => !session.HasScope(scope)).ToArray();
            var valid = missing.Length == 0;
            _chatOAuthDiagnostic.Text = valid
                ? "Twitch OAuth: verbunden · Token gültig"
                : "Twitch OAuth: Chatberechtigungen fehlen – neu verbinden";
            _chatOAuthDiagnostic.ForeColor = valid ? ActiveColor : ErrorColor;
            _chatAccountDiagnostic.Text = $"Konto: {session.Login} ({session.UserId})";
            _chatScopesDiagnostic.Text = valid
                ? "Chat-Scopes: user:read:chat, user:write:chat"
                : "Fehlende Scopes: " + string.Join(", ", missing);
            _chatScopesDiagnostic.ForeColor = valid ? ActiveColor : ErrorColor;
            _chatTargetDiagnostic.Text =
                $"Zielkanal: {broadcaster.DisplayName} ({broadcaster.Id})";
        });
    }

    private void UpdateChatConnectionDiagnostics(ChatConnectionDiagnostics status)
    {
        _lastChatConnectionStatus = status;
        RunOnUiThread(() =>
        {
            _chatWebSocketDiagnostic.Text = status.WebSocketConnected
                ? "EventSub WebSocket: verbunden · " + MaskDiagnosticSession(status.SessionId)
                : "EventSub WebSocket: getrennt";
            _chatWebSocketDiagnostic.ForeColor =
                status.WebSocketConnected ? ActiveColor : ErrorColor;
            _chatSubscriptionDiagnostic.Text = status.SubscriptionEnabled
                ? "channel.chat.message: enabled"
                : "channel.chat.message: nicht aktiv";
            _chatSubscriptionDiagnostic.ForeColor =
                status.SubscriptionEnabled ? ActiveColor : ErrorColor;
            if (status.LastReceivedAt is { } received)
                _chatLastReceivedDiagnostic.Text =
                    "Letzter Empfang: " + received.ToLocalTime().ToString("HH:mm:ss");
            if (!string.IsNullOrWhiteSpace(status.LastError))
                SetChatDiagnosticError(status.LastError);
        });
    }

    private void UpdateChatLastSent(DateTimeOffset sentAt)
    {
        RunOnUiThread(() =>
        {
            _chatLastSentDiagnostic.Text =
                "Letzter Versand: " + sentAt.ToLocalTime().ToString("HH:mm:ss");
            _chatLastSentDiagnostic.ForeColor = ActiveColor;
            _chatLastErrorDiagnostic.Text = "Letzter Fehler: –";
            _chatLastErrorDiagnostic.ForeColor = MutedTextColor;
        });
    }

    private void ResetChatDiagnosticConnection()
    {
        _lastChatConnectionStatus = null;
        RunOnUiThread(() =>
        {
            _chatWebSocketDiagnostic.Text = "EventSub WebSocket: getrennt";
            _chatWebSocketDiagnostic.ForeColor = InactiveColor;
            _chatSubscriptionDiagnostic.Text = "channel.chat.message: nicht aktiv";
            _chatSubscriptionDiagnostic.ForeColor = InactiveColor;
        });
    }

    private void SetChatDiagnosticError(string message)
    {
        RunOnUiThread(() =>
        {
            _chatLastErrorDiagnostic.Text = "Letzter Fehler: " + message;
            _chatLastErrorDiagnostic.ForeColor = ErrorColor;
        });
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }
            return;
        }
        action();
    }

    private static string MaskDiagnosticSession(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Session: –" :
        value.Length <= 8 ? "Session: ***" :
        "Session: " + value[..4] + "…" + value[^4..];
}
