using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Label _accountNameLabel = new();
    private readonly Label _accountGreetingLabel = new();
    private string? _accountDisplayName;
    private bool _accountLoginBusy;
    private readonly Button _connectTwitchAccountButton = NewActionButton("Bei Twitch anmelden");
    private readonly System.Windows.Forms.Timer _greetingTimer = new() { Interval = 60_000 };

    internal static string AccountGreeting(string? name, int hour)
    {
        var greeting = hour < 5 || hour >= 18 ? "Guten Abend" : hour < 11 ? "Guten Morgen" : "Guten Tag";
        return string.IsNullOrWhiteSpace(name) ? "Willkommen!" : $"{greeting}, {name}";
    }

    private async Task ConnectTwitchAccountAsync()
    {
        if (_accountLoginBusy || _maintenanceBusy || _connectionTestBusy || _dataAdministrationBusy || _updateBusy ||
            _shutdown is not null || _stoppingTasks.Any(task => !task.IsCompleted))
        {
            MessageBox.Show(this, "Plugin zuerst vollständig stoppen und laufende Vorgänge beenden.", "Twitch-Anmeldung");
            return;
        }
        _accountLoginBusy = true;
        _connectTwitchAccountButton.Enabled = false;
        _startButton.Enabled = false;
        _testConnectionsButton.Enabled = false;
        try
        {
            UpdateAccountIdentity(null);
            SetServiceStatus(_twitchIndicator, "Twitch", "Anmeldung …", WaitingColor);
            // Load credentials only. Do not require OBS connectivity, a configured channel,
            // saved game settings, or a matching cloud channel just to authenticate an account.
            var config = _configurationService.LoadForEditing();
            using var dialog = new TwitchSignInForm((progress, token) =>
                new AuthenticationService(config).GetSessionAsync(token, progress));
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Session is not { } session)
            {
                SetServiceStatus(_twitchIndicator, "Twitch", "Nicht angemeldet", InactiveColor);
                return;
            }
            var twitch = new TwitchService(config.Twitch.ClientId, session.AccessToken);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ShowAuthenticatedAccountAsync(twitch, session, timeout.Token);
            if (string.IsNullOrWhiteSpace(_twitchChannelBox.Text)) _twitchChannelBox.Text = session.Login;
            SetServiceStatus(_twitchIndicator, "Twitch", "Angemeldet", ActiveColor);
            AppendLog($"Twitch-Konto {session.Login} angemeldet. Plugin-Dienste können separat gestartet werden.");
        }
        catch (Exception error)
        {
            UpdateAccountIdentity(null);
            SetServiceStatus(_twitchIndicator, "Twitch", "Anmeldung fehlgeschlagen", ErrorColor);
            MessageBox.Show(this, error.Message, "Twitch-Anmeldung fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _accountLoginBusy = false;
            _connectTwitchAccountButton.Enabled = true;
            _startButton.Enabled = !_administrationTestOnly;
            _testConnectionsButton.Enabled = true;
        }
    }

    private void UpdateAccountIdentity(string? displayName)
    {
        _accountDisplayName = displayName;
        _accountNameLabel.Text = string.IsNullOrWhiteSpace(displayName) ? "▣  Twitch anmelden" : "▣  " + displayName;
        _accountGreetingLabel.Text = AccountGreeting(displayName, DateTime.Now.Hour);
    }

    private async Task ShowAuthenticatedAccountAsync(TwitchService twitch, TwitchSession session, CancellationToken token)
    {
        // Token owner, not configured broadcaster (which may be a different account).
        UpdateAccountIdentity(session.Login);
        try
        {
            var owner = await twitch.GetUserAsync("", token);
            if (owner?.Id == session.UserId && !string.IsNullOrWhiteSpace(owner.DisplayName))
                UpdateAccountIdentity(owner.DisplayName);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Validated login remains a truthful fallback when Helix is unavailable. */ }
    }
}
