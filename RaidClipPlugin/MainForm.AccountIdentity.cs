using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private readonly Label _accountNameLabel = new();
    private readonly Label _accountGreetingLabel = new();
    private string? _accountDisplayName;
    private readonly System.Windows.Forms.Timer _greetingTimer = new() { Interval = 60_000 };

    internal static string AccountGreeting(string? name, int hour)
    {
        var greeting = hour < 5 || hour >= 18 ? "Guten Abend" : hour < 11 ? "Guten Morgen" : "Guten Tag";
        return string.IsNullOrWhiteSpace(name) ? "Willkommen!" : $"{greeting}, {name}";
    }

    private void UpdateAccountIdentity(string? displayName)
    {
        _accountDisplayName = displayName;
        _accountNameLabel.Text = string.IsNullOrWhiteSpace(displayName) ? "▣  Nicht angemeldet" : "▣  " + displayName;
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
