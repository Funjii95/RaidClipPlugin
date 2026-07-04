using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class GiveawayService : IDisposable
{
    private static readonly Regex WhiteSpace = new("\\s+", RegexOptions.Compiled);
    private readonly string _broadcasterId;
    private readonly string _chatSenderId;
    private readonly IGiveawayTwitchClient _twitch;
    private readonly IClipChatClient _chat;
    private readonly ViewerPointStore _points;
    private readonly GiveawayStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GiveawayConfig _config;
    private MinigameConfig _minigame;
    private GiveawayRuntimeState _state = new();
    private bool _initialized;
    private bool _disposed;

    public event Action<GiveawayRuntimeState>? StateChanged;

    public GiveawayService(
        string broadcasterId,
        string chatSenderId,
        GiveawayConfig config,
        MinigameConfig minigame,
        IGiveawayTwitchClient twitch,
        IClipChatClient chat,
        ViewerPointStore points,
        GiveawayStore store)
    {
        _broadcasterId = broadcasterId;
        _chatSenderId = chatSenderId;
        _config = config;
        _minigame = minigame;
        _twitch = twitch;
        _chat = chat;
        _points = points;
        _store = store;
    }

    public void UpdateConfig(GiveawayConfig config, MinigameConfig minigame)
    {
        _config = config;
        _minigame = minigame;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            _state = await _store.LoadAsync(cancellationToken);
            _initialized = true;
            if (_state.Status == GiveawayStatus.Active && IsExpired(_state))
            {
                if (_config.AutoDrawWhenExpired)
                    await DrawLockedAsync(_config.MaximumWinners, true,
                        cancellationToken);
                else
                    await EndLockedAsync(cancellationToken);
            }
            NotifyStateChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_state.Status != GiveawayStatus.Active) continue;
                if (IsExpired(_state))
                {
                    if (_config.AutoDrawWhenExpired)
                        await DrawLockedAsync(_config.MaximumWinners, true,
                            cancellationToken);
                    else
                        await EndLockedAsync(cancellationToken);
                    continue;
                }

                if (_config.AnnounceParticipantCount &&
                    ShouldAnnounceParticipantCount())
                {
                    _state.LastParticipantAnnouncementUtc = DateTimeOffset.UtcNow;
                    await SaveLockedAsync(cancellationToken);
                    await SendTemplateAsync(_config.ChatMessages.Status,
                        null, Array.Empty<GiveawayWinner>(), cancellationToken);
                }
                NotifyStateChanged();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Giveaway-Zeitsteuerung fehlgeschlagen: " +
                                  SafeError(exception));
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public async Task<bool> ProcessMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(message.Text))
            return false;

        if (_config.ModeratorCommands.Enabled &&
            CommandPermissionService.Resolve(message,
                message.IsBroadcaster || message.IsModerator) &&
            await TryProcessModeratorCommandAsync(message, cancellationToken))
            return true;

        if (!TryParseJoinCommand(
                message.Text, _config, out var requestedExtraTickets))
            return false;

        await JoinAsync(message, requestedExtraTickets, cancellationToken);
        return true;
    }

    public async Task<GiveawayActionResult> StartAsync(
        CancellationToken cancellationToken)
    {
        if (_config.LiveOnly)
        {
            try
            {
                var stream = await _twitch.GetLiveStreamAsync(
                    _broadcasterId, cancellationToken);
                if (stream is null || !stream.IsLive)
                    return new(false, "Der Twitch-Kanal ist offline.");
            }
            catch (Exception exception)
            {
                return new(false, "Livestatus konnte nicht geprüft werden: " +
                    SafeError(exception));
            }
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (_state.IsRunning)
                return new(false, "Es läuft bereits ein Giveaway.");
            var now = DateTimeOffset.UtcNow;
            _state = new GiveawayRuntimeState
            {
                Id = Guid.NewGuid().ToString("N"),
                Status = GiveawayStatus.Active,
                Title = _config.Title,
                Description = _config.Description,
                Prize = _config.Prize,
                Command = _config.Command,
                StartedAtUtc = now,
                EndsAtUtc = now.AddMinutes(_config.DurationMinutes),
                LastParticipantAnnouncementUtc = now
            };
            await SaveLockedAsync(cancellationToken);
            Console.WriteLine(
                $"Giveaway gestartet: {_state.Title}, Preis: {_state.Prize}.");
            await SendTemplateAsync(_config.ChatMessages.Started,
                null, Array.Empty<GiveawayWinner>(), cancellationToken);
            NotifyStateChanged();
            return new(true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GiveawayActionResult> PauseAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (_state.Status != GiveawayStatus.Active)
                return new(false, "Kein aktives Giveaway vorhanden.");
            _state.PausedRemainingSeconds = Math.Max(0,
                (int)Math.Ceiling((_state.EndsAtUtc.GetValueOrDefault() -
                                  DateTimeOffset.UtcNow).TotalSeconds));
            _state.Status = GiveawayStatus.Paused;
            _state.EndsAtUtc = null;
            await SaveLockedAsync(cancellationToken);
            await SendTemplateAsync(_config.ChatMessages.Paused,
                null, Array.Empty<GiveawayWinner>(), cancellationToken);
            Console.WriteLine("Giveaway pausiert.");
            NotifyStateChanged();
            return new(true);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> ResumeAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (_state.Status != GiveawayStatus.Paused)
                return new(false, "Das Giveaway ist nicht pausiert.");
            _state.Status = GiveawayStatus.Active;
            _state.EndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(
                Math.Max(1, _state.PausedRemainingSeconds));
            _state.PausedRemainingSeconds = 0;
            await SaveLockedAsync(cancellationToken);
            await SendTemplateAsync(_config.ChatMessages.Resumed,
                null, Array.Empty<GiveawayWinner>(), cancellationToken);
            Console.WriteLine("Giveaway fortgesetzt.");
            NotifyStateChanged();
            return new(true);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> EndAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (!_state.IsRunning)
                return new(false, "Kein Giveaway aktiv.");
            return await EndLockedAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> CancelAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (!_state.IsRunning)
                return new(false, "Kein Giveaway aktiv.");
            if (_config.RefundPointsOnCancel)
                await RefundAllLockedAsync(cancellationToken);
            _state.Status = GiveawayStatus.Cancelled;
            _state.EndsAtUtc = null;
            await SaveLockedAsync(cancellationToken);
            await SendTemplateAsync(_config.ChatMessages.Cancelled,
                null, Array.Empty<GiveawayWinner>(), cancellationToken);
            Console.WriteLine("Giveaway abgebrochen.");
            NotifyStateChanged();
            return new(true);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> ResetParticipantsAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (_config.RefundPointsOnCancel)
                await RefundAllLockedAsync(cancellationToken);
            _state.Participants.Clear();
            _state.Winners.Clear();
            await SaveLockedAsync(cancellationToken);
            Console.WriteLine("Giveaway-Teilnehmer wurden zurückgesetzt.");
            NotifyStateChanged();
            return new(true);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> DrawAsync(
        int count,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return await DrawLockedAsync(count, false, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public Task<GiveawayActionResult> DrawConfiguredAsync(
        CancellationToken cancellationToken) =>
        DrawAsync(_config.MaximumWinners, cancellationToken);

    public Task<GiveawayActionResult> DrawAdditionalAsync(
        CancellationToken cancellationToken) => DrawAsync(1, cancellationToken);

    public async Task<GiveawayActionResult> RerollAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (_state.Winners.Count == 0)
                return new(false, "Es gibt noch keinen Gewinner für eine Neuauslosung.");
            var replaced = _state.Winners[^1];
            _state.Winners.RemoveAt(_state.Winners.Count - 1);
            var participant = _state.Participants.FirstOrDefault(item =>
                item.UserId.Equals(replaced.UserId, StringComparison.Ordinal));
            if (participant is not null) participant.IsValid = false;
            Console.WriteLine("Giveaway-Neuauslosung statt " + replaced.UserLogin + ".");
            var result = await DrawLockedAsync(1, false, cancellationToken);
            if (participant is not null) participant.IsValid = true;
            if (!result.Success) _state.Winners.Add(replaced);
            await SaveLockedAsync(cancellationToken);
            NotifyStateChanged();
            return result;
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> JoinAsync(
        ChatMessage message,
        int requestedExtraTickets,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (_state.Status != GiveawayStatus.Active)
            {
                await SendTemplateAsync(_config.ChatMessages.NotActive,
                    message, Array.Empty<GiveawayWinner>(), cancellationToken);
                return new(false, "Kein aktives Giveaway.");
            }

            if (_config.LiveOnly)
            {
                var stream = await _twitch.GetLiveStreamAsync(
                    _broadcasterId, cancellationToken);
                if (stream is null || !stream.IsLive)
                {
                    await SendTemplateAsync(_config.ChatMessages.Offline,
                        message, Array.Empty<GiveawayWinner>(), cancellationToken);
                    return Reject(message, "stream-offline");
                }
            }

            var userKey = UserKey(message);
            var existing = _state.Participants.FirstOrDefault(item =>
                item.UserId.Equals(userKey, StringComparison.Ordinal));
            if (existing is { IsValid: true } && _config.PreventDuplicateEntries)
            {
                await SendTemplateAsync(_config.ChatMessages.Duplicate,
                    message, Array.Empty<GiveawayWinner>(), cancellationToken);
                return Reject(message, "duplicate");
            }
            if (existing is { IsValid: false } &&
                _config.PreventReentryAfterLeaving)
            {
                await SendTemplateAsync(_config.ChatMessages.Excluded,
                    message, Array.Empty<GiveawayWinner>(), cancellationToken);
                return Reject(message, "reentry-blocked");
            }

            var eligibility = await CheckEligibilityLockedAsync(
                message, cancellationToken);
            if (!eligibility.Allowed)
            {
                await SendTemplateAsync(_config.ChatMessages.Excluded,
                    message, Array.Empty<GiveawayWinner>(), cancellationToken);
                return Reject(message, eligibility.Reason);
            }

            var extraTickets = _config.ExtraTicketsEnabled
                ? Math.Clamp(requestedExtraTickets, 0, _config.MaximumExtraTickets)
                : 0;
            var totalCost = Math.Max(0, _config.EntryCost) +
                            extraTickets * Math.Max(0, _config.ExtraTicketCost);
            var currentPoints = await _points.GetPointsAsync(
                message.UserId, cancellationToken);
            var requiredPoints = Math.Max(_config.MinimumPoints,
                _config.DeductPointsAtJoin
                    ? checked(_minigame.MinimumPoints + totalCost) : 0);
            if (currentPoints < requiredPoints)
            {
                await SendTemplateAsync(_config.ChatMessages.InsufficientPoints,
                    message, Array.Empty<GiveawayWinner>(), cancellationToken,
                    requiredPoints);
                return Reject(message, "insufficient-points");
            }

            var pointsUsed = 0;
            if (_config.DeductPointsAtJoin && totalCost > 0)
            {
                var spend = await _points.ApplyGambleAsync(
                    message.UserId, message.UserName, totalCost, 0,
                    _minigame.MinimumPoints, cancellationToken);
                if (!spend.Success)
                {
                    await SendTemplateAsync(_config.ChatMessages.InsufficientPoints,
                        message, Array.Empty<GiveawayWinner>(), cancellationToken,
                        requiredPoints);
                    return Reject(message, "point-deduction-failed");
                }
                pointsUsed = totalCost;
                Console.WriteLine(
                    $"Giveaway-Punktabzug: {message.UserLogin}, {pointsUsed} Punkte.");
            }

            if (existing is not null) _state.Participants.Remove(existing);
            var participant = new GiveawayParticipant
            {
                UserId = userKey,
                UserLogin = message.UserLogin,
                DisplayName = message.UserName,
                JoinedAtUtc = DateTimeOffset.UtcNow,
                Role = RoleName(message, eligibility.FollowedAtUtc is not null),
                IsSubscriber = message.IsSubscriber,
                IsVip = message.IsVip,
                IsBroadcaster = message.IsBroadcaster,
                PointsUsed = pointsUsed,
                ExtraTickets = extraTickets,
                IsValid = true
            };
            _state.Participants.Add(participant);
            await SaveLockedAsync(cancellationToken);
            Console.WriteLine(
                $"Giveaway-Teilnahme: {participant.UserLogin}, Punkte: {participant.PointsUsed}.");
            await SendTemplateAsync(_config.ChatMessages.Joined,
                message, Array.Empty<GiveawayWinner>(), cancellationToken);
            NotifyStateChanged();
            return new(true);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Giveaway-Teilnahme fehlgeschlagen: " +
                              SafeError(exception));
            return new(false, SafeError(exception));
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> AddParticipantManuallyAsync(
        string login,
        CancellationToken cancellationToken)
    {
        var user = await _twitch.GetUserAsync(
            NormalizeUser(login), cancellationToken);
        if (user is null) return new(false, "Twitch-Nutzer wurde nicht gefunden.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            if (!_state.IsRunning) return new(false, "Kein aktives Giveaway.");
            if (_state.Participants.Any(item =>
                    item.UserId.Equals(user.Id, StringComparison.Ordinal) && item.IsValid))
                return new(false, "Nutzer ist bereits eingetragen.");
            _state.Participants.RemoveAll(item => item.UserId == user.Id);
            _state.Participants.Add(new GiveawayParticipant
            {
                UserId = user.Id,
                UserLogin = user.Login,
                DisplayName = user.DisplayName,
                JoinedAtUtc = DateTimeOffset.UtcNow,
                Role = "Manuell",
                IsValid = true
            });
            await SaveLockedAsync(cancellationToken);
            Console.WriteLine("Giveaway-Teilnehmer manuell hinzugefügt: " + user.Login);
            NotifyStateChanged();
            return new(true);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayActionResult> RemoveParticipantAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            var participant = _state.Participants.FirstOrDefault(item =>
                item.UserId.Equals(userId, StringComparison.Ordinal));
            if (participant is null) return new(false, "Teilnehmer wurde nicht gefunden.");
            participant.IsValid = false;
            participant.InvalidReason = "Manuell entfernt";
            await SaveLockedAsync(cancellationToken);
            Console.WriteLine(
                "Giveaway-Teilnehmer manuell entfernt: " + participant.UserLogin);
            NotifyStateChanged();
            return new(true);
        }
        finally { _gate.Release(); }
    }

    public async Task<GiveawayRuntimeState> GetStateAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return CloneState(_state);
        }
        finally { _gate.Release(); }
    }

    private async Task<GiveawayActionResult> DrawLockedAsync(
        int requestedCount,
        bool automatic,
        CancellationToken cancellationToken)
    {
        if (_state.Status is GiveawayStatus.NotStarted or GiveawayStatus.Cancelled)
            return new(false, "Kein Giveaway vorhanden.");

        var previousWinnerIds = _state.Winners
            .Select(item => item.UserId).ToHashSet(StringComparer.Ordinal);
        var candidates = _state.Participants
            .Where(item => item.IsValid &&
                           (!_config.ExcludeBroadcasterFromDraw ||
                            !item.IsBroadcaster) &&
                           !IsBot(item.UserLogin) &&
                           (_config.AllowPreviousWinners ||
                            !previousWinnerIds.Contains(item.UserId)))
            .ToList();
        if (candidates.Count == 0)
            return new(false, "Keine gültigen Teilnehmer vorhanden.");

        var count = Math.Min(Math.Max(1, requestedCount), candidates.Count);
        var selected = new List<GiveawayWinner>();
        for (var index = 0; index < count; index++)
        {
            var participant = PickSecureWinner(candidates);
            candidates.RemoveAll(item => item.UserId == participant.UserId);
            var winner = new GiveawayWinner
            {
                UserId = participant.UserId,
                UserLogin = participant.UserLogin,
                DisplayName = participant.DisplayName,
                DrawnAtUtc = DateTimeOffset.UtcNow,
                DrawNumber = _state.Winners.Count + 1
            };
            _state.Winners.Add(winner);
            selected.Add(winner);
            Console.WriteLine(
                $"Giveaway-Gewinner #{winner.DrawNumber}: {winner.UserLogin}." +
                (automatic ? " Automatisch gezogen." : ""));
        }

        if (_config.AnnounceWinners)
        {
            var message = selected.Count == 1
                ? _config.ChatMessages.Winner
                : _config.ChatMessages.Winners;
            await SendTemplateAsync(message, null, selected, cancellationToken);
        }
        if (_config.AutoCloseAfterDraw)
        {
            _state.Status = GiveawayStatus.Ended;
            _state.EndsAtUtc = null;
        }
        await SaveLockedAsync(cancellationToken);
        NotifyStateChanged();
        return new(true, "", selected);
    }

    private GiveawayParticipant PickSecureWinner(
        IReadOnlyList<GiveawayParticipant> candidates)
    {
        var weighted = candidates.Select(item => new
        {
            Participant = item,
            Weight = CalculateTicketWeight(item)
        }).ToArray();
        var total = weighted.Sum(item => item.Weight);
        var roll = RandomNumberGenerator.GetInt32(total);
        foreach (var item in weighted)
        {
            if (roll < item.Weight) return item.Participant;
            roll -= item.Weight;
        }
        return weighted[^1].Participant;
    }

    public static int CalculateTicketWeight(GiveawayParticipant participant) =>
        checked(1 + Math.Max(0, participant.ExtraTickets));

    private async Task<GiveawayEligibilityResult> CheckEligibilityLockedAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        var login = NormalizeUser(message.UserLogin);
        if (_config.BlockedUsers.Any(item => NormalizeUser(item) == login) ||
            IsBot(login))
            return new(false, "blacklist");
        if (_config.AllowedUsers.Any(item => NormalizeUser(item) == login))
            return new(true, "whitelist");

        DateTimeOffset? followedAt = null;
        if (_config.AllowedRoles.Followers || _config.MinimumFollowMinutes > 0)
            followedAt = await _twitch.GetFollowedAtAsync(
                _broadcasterId, message.UserId, cancellationToken);

        var roles = _config.AllowedRoles;
        var allowed = CommandPermissionService.Resolve(message,
                      roles.Everyone ||
                      roles.Broadcaster && message.IsBroadcaster ||
                      roles.Moderators && message.IsModerator ||
                      roles.Vips && message.IsVip ||
                      roles.Subscribers && message.IsSubscriber ||
                      roles.Followers && followedAt is not null);
        if (!allowed) return new(false, "role");

        if (_config.MinimumFollowMinutes > 0 && !message.IsBroadcaster)
        {
            if (followedAt is null) return new(false, "not-following");
            if (DateTimeOffset.UtcNow - followedAt.Value <
                TimeSpan.FromMinutes(_config.MinimumFollowMinutes))
                return new(false, "follow-duration");
        }
        return new(true, "", followedAt);
    }

    private async Task<bool> TryProcessModeratorCommandAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        var text = NormalizeCommandText(message.Text);
        var commands = _config.ModeratorCommands;
        if (text == NormalizeCommandText(commands.Start))
            await StartAsync(cancellationToken);
        else if (text == NormalizeCommandText(commands.Stop))
            await EndAsync(cancellationToken);
        else if (text == NormalizeCommandText(commands.Pause))
            await PauseAsync(cancellationToken);
        else if (text == NormalizeCommandText(commands.Resume))
            await ResumeAsync(cancellationToken);
        else if (text == NormalizeCommandText(commands.Draw))
            await DrawConfiguredAsync(cancellationToken);
        else if (text == NormalizeCommandText(commands.Reroll))
            await RerollAsync(cancellationToken);
        else if (text == NormalizeCommandText(commands.Status))
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await EnsureInitializedLockedAsync(cancellationToken);
                await SendTemplateAsync(_config.ChatMessages.Status,
                    message, Array.Empty<GiveawayWinner>(), cancellationToken);
            }
            finally { _gate.Release(); }
        }
        else return false;
        return true;
    }

    public static bool TryParseJoinCommand(
        string text,
        GiveawayConfig config,
        out int extraTickets)
    {
        extraTickets = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = WhiteSpace.Replace(text.Trim(), " ").Split(' ');
        var commands = new[] { config.Command }
            .Concat(config.Aliases ?? new List<string>());
        if (!commands.Any(command => parts[0].Equals(
                command, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (parts.Length == 1) return true;
        return parts.Length == 2 && int.TryParse(parts[1], out extraTickets) &&
               extraTickets >= 0;
    }

    private async Task<GiveawayActionResult> EndLockedAsync(
        CancellationToken cancellationToken)
    {
        _state.Status = GiveawayStatus.Ended;
        _state.EndsAtUtc = null;
        await SaveLockedAsync(cancellationToken);
        await SendTemplateAsync(_config.ChatMessages.Ended,
            null, Array.Empty<GiveawayWinner>(), cancellationToken);
        Console.WriteLine("Giveaway beendet.");
        NotifyStateChanged();
        return new(true);
    }

    private async Task RefundAllLockedAsync(CancellationToken cancellationToken)
    {
        foreach (var participant in _state.Participants.Where(item => item.PointsUsed > 0))
        {
            await _points.AddPointsAsync(
                participant.UserId, participant.DisplayName,
                participant.PointsUsed, _minigame.MinimumPoints,
                cancellationToken,
                _minigame.MaximumAccountEnabled
                    ? _minigame.MaximumAccountPoints : int.MaxValue);
            Console.WriteLine(
                $"Giveaway-Rückerstattung: {participant.UserLogin}, {participant.PointsUsed} Punkte.");
            participant.PointsUsed = 0;
        }
    }

    private async Task SendTemplateAsync(
        GiveawayChatMessage configured,
        ChatMessage? user,
        IReadOnlyList<GiveawayWinner> winners,
        CancellationToken cancellationToken,
        int? requiredPoints = null)
    {
        if (!configured.Enabled || string.IsNullOrWhiteSpace(configured.Text)) return;
        var winner = winners.FirstOrDefault()?.DisplayName ??
                     _state.Winners.LastOrDefault()?.DisplayName ?? "";
        var winnerNames = string.Join(", ", winners.Select(item => "@" + item.DisplayName));
        var remaining = RemainingTime(_state);
        var text = configured.Text
            .Replace("{username}", user?.UserName ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{winner}", winner, StringComparison.OrdinalIgnoreCase)
            .Replace("{winners}", winnerNames, StringComparison.OrdinalIgnoreCase)
            .Replace("{prize}", _state.Prize, StringComparison.OrdinalIgnoreCase)
            .Replace("{title}", _state.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{command}", _state.Command, StringComparison.OrdinalIgnoreCase)
            .Replace("{participantCount}",
                _state.Participants.Count(item => item.IsValid).ToString(),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{remainingTime}", remaining, StringComparison.OrdinalIgnoreCase)
            .Replace("{requiredPoints}",
                (requiredPoints ?? _config.MinimumPoints).ToString(),
                StringComparison.OrdinalIgnoreCase)
            .Replace("{entryCost}", _config.EntryCost.ToString(),
                StringComparison.OrdinalIgnoreCase);
        try
        {
            await _chat.SendChatMessageAsync(
                _broadcasterId, _chatSenderId, text, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Giveaway-Chatnachricht fehlgeschlagen: " +
                              SafeError(exception));
        }
    }

    private GiveawayActionResult Reject(ChatMessage message, string reason)
    {
        Console.WriteLine(
            $"Giveaway-Teilnahme abgelehnt: {message.UserLogin}, Grund: {reason}.");
        return new(false, reason);
    }

    private bool IsBot(string login)
    {
        if (!_config.ExcludeBots) return false;
        var normalized = NormalizeUser(login);
        return _minigame.PointsBlacklist.Any(item =>
                   NormalizeUser(item) == normalized) ||
               normalized is "nightbot" or "streamelements" or "streamlabs" or "moobot";
    }

    private static string RoleName(ChatMessage message, bool isFollower) =>
        message.IsBroadcaster ? "Broadcaster" :
        message.IsModerator ? "Moderator" :
        message.IsVip ? "VIP" :
        message.IsSubscriber ? "Subscriber" :
        isFollower ? "Follower" : "Zuschauer";

    private bool ShouldAnnounceParticipantCount()
    {
        var last = _state.LastParticipantAnnouncementUtc ??
                   _state.StartedAtUtc ?? DateTimeOffset.UtcNow;
        return DateTimeOffset.UtcNow - last >= TimeSpan.FromMinutes(
            Math.Max(1, _config.ParticipantCountIntervalMinutes));
    }

    private async Task EnsureInitializedLockedAsync(
        CancellationToken cancellationToken)
    {
        if (_initialized) return;
        _state = await _store.LoadAsync(cancellationToken);
        _initialized = true;
    }

    private async Task SaveLockedAsync(CancellationToken cancellationToken) =>
        await _store.SaveAsync(_state, cancellationToken);

    private void NotifyStateChanged() =>
        StateChanged?.Invoke(CloneState(_state));

    public static string RemainingTime(GiveawayRuntimeState state)
    {
        if (state.Status == GiveawayStatus.Paused)
            return TimeSpan.FromSeconds(Math.Max(0, state.PausedRemainingSeconds))
                .ToString(@"hh\:mm\:ss");
        if (state.EndsAtUtc is null) return "–";
        var remaining = state.EndsAtUtc.Value - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        return remaining.ToString(@"hh\:mm\:ss");
    }

    private static bool IsExpired(GiveawayRuntimeState state) =>
        state.EndsAtUtc is not null && state.EndsAtUtc <= DateTimeOffset.UtcNow;

    private static string NormalizeUser(string value) =>
        (value ?? "").Trim().TrimStart('@').ToLowerInvariant();

    private static string UserKey(ChatMessage message) =>
        string.IsNullOrWhiteSpace(message.UserId)
            ? NormalizeUser(message.UserLogin) : message.UserId;

    private static string NormalizeCommandText(string value) =>
        WhiteSpace.Replace((value ?? "").Trim(), " ").ToLowerInvariant();

    private static string SafeError(Exception exception) =>
        exception is HttpRequestException or IOException or InvalidOperationException
            ? exception.Message : "Unbekannter Giveaway-Fehler.";

    private static GiveawayRuntimeState CloneState(GiveawayRuntimeState source) => new()
    {
        Id = source.Id,
        Status = source.Status,
        Title = source.Title,
        Description = source.Description,
        Prize = source.Prize,
        Command = source.Command,
        StartedAtUtc = source.StartedAtUtc,
        EndsAtUtc = source.EndsAtUtc,
        PausedRemainingSeconds = source.PausedRemainingSeconds,
        LastParticipantAnnouncementUtc = source.LastParticipantAnnouncementUtc,
        Participants = source.Participants.Select(item => new GiveawayParticipant
        {
            UserId = item.UserId,
            UserLogin = item.UserLogin,
            DisplayName = item.DisplayName,
            JoinedAtUtc = item.JoinedAtUtc,
            Role = item.Role,
            IsSubscriber = item.IsSubscriber,
            IsVip = item.IsVip,
            IsBroadcaster = item.IsBroadcaster,
            PointsUsed = item.PointsUsed,
            ExtraTickets = item.ExtraTickets,
            IsValid = item.IsValid,
            InvalidReason = item.InvalidReason
        }).ToList(),
        Winners = source.Winners.Select(item => new GiveawayWinner
        {
            UserId = item.UserId,
            UserLogin = item.UserLogin,
            DisplayName = item.DisplayName,
            DrawnAtUtc = item.DrawnAtUtc,
            DrawNumber = item.DrawNumber
        }).ToList()
    };

    public void Dispose()
    {
        if (_disposed) return;
        _gate.Dispose();
        _disposed = true;
    }
}
