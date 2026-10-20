using System.Diagnostics;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed class TwitchSignInForm : Form
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(560, 0), Text = "Twitch-Anmeldung wird vorbereitet …" };
    private readonly TextBox _code = new() { Name = "TwitchDeviceCode", ReadOnly = true, Width = 260, Font = new Font("Segoe UI", 20F, FontStyle.Bold) };
    private readonly LinkLabel _link = new() { AutoSize = true, Text = "Browser öffnen", Visible = false };
    private readonly Button _close = new() { AutoSize = true, Text = "Abbrechen", DialogResult = DialogResult.Cancel };
    private readonly Func<IProgress<TwitchAuthorizationRequest>, CancellationToken, Task<TwitchSession>> _signIn;
    private string? _verificationUrl;
    public TwitchSession? Session { get; private set; }

    public TwitchSignInForm(Func<IProgress<TwitchAuthorizationRequest>, CancellationToken, Task<TwitchSession>> signIn)
    {
        _signIn = signIn;
        Text = "Bei Twitch anmelden";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(24) };
        panel.Controls.AddRange([_status, _code, _link, _close]);
        Controls.Add(panel);
        CancelButton = _close;
        _code.Visible = false;
        _link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(_verificationUrl!) { UseShellExecute = true }); }
            catch { _status.Text = "Browser konnte nicht geöffnet werden. Öffne die angezeigte Adresse manuell und gib den Code ein."; }
        };
        Shown += async (_, _) => await SignInAsync();
        FormClosing += (_, _) => _cancel.Cancel();
    }

    private async Task SignInAsync()
    {
        try
        {
            var progress = new Progress<TwitchAuthorizationRequest>(request =>
            {
                if (IsDisposed || _cancel.IsCancellationRequested) return;
                _verificationUrl = request.VerificationUri;
                _link.Text = request.VerificationUri;
                _link.Visible = true;
                _code.Text = request.UserCode;
                _code.Visible = true;
                _status.Text = "Melde dich im Browser mit deinem eigenen Twitch-Konto an und bestätige diesen Code. " +
                    "Falls sich kein Browser öffnet, klicke auf die Adresse. Dieses Fenster wartet auf deine Freigabe.";
            });
            var session = await _signIn(progress, _cancel.Token);
            if (IsDisposed || _cancel.IsCancellationRequested) return;
            Session = session;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (IsDisposed || _cancel.IsCancellationRequested) return;
            _code.Visible = false;
            _link.Visible = false;
            _status.Text = "Twitch-Anmeldung fehlgeschlagen: " + error.Message +
                "\n\nPrüfe Client ID und Secret unter Einstellungen → Wartung. Es wurden keine Spielstände oder Cloud-Zuordnungen geändert.";
            _close.Text = "Schließen";
        }
    }
}
