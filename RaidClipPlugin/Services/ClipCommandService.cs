using System.Threading.Channels;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ClipCommandService : IDisposable
{
    private readonly string _broadcasterId;
    private readonly string _chatSenderId;
    private readonly string _channelName;
    private readonly string _thumbnailUrl;
    private readonly bool _hasClipScope;
    private readonly ITwitchClipClient _twitchClient;
    private readonly IClipChatClient _chat;
    private readonly TwitchClipService _clips;
    private readonly ClipPermissionService _permissions;
    private readonly ClipCooldownService _cooldowns;
    private readonly ClipTemplateService _templates;
    private DiscordClipService? _discord;
    private readonly Channel<ClipCommandRequest> _queue;
    private ClipCommandConfig _config;
    private int _pending;
    private int _processing;
    private bool _disposed;

    public event Action<ClipCommandStatus>? StatusChanged;

    public ClipCommandService(
        string broadcasterId,
        string chatSenderId,
        string channelName,
        string thumbnailUrl,
        bool hasClipScope,
        ClipCommandConfig config,
        ITwitchClipClient twitchClient,
        IClipChatClient chat,
        TwitchClipService clips,
        ClipPermissionService permissions,
        ClipCooldownService cooldowns,
        ClipTemplateService templates,
        DiscordClipService? discord)
    {
        _broadcasterId = broadcasterId;
        _chatSenderId = chatSenderId;
        _channelName = channelName;
        _thumbnailUrl = thumbnailUrl;
        _hasClipScope = hasClipScope;
        _config = config;
        _twitchClient = twitchClient;
        _chat = chat;
        _clips = clips;
        _permissions = permissions;
        _cooldowns = cooldowns;
        _templates = templates;
        _discord = discord;
        _queue = Channel.CreateBounded<ClipCommandRequest>(
            new BoundedChannelOptions(Math.Clamp(config.MaximumQueueSize, 1, 100))
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });
    }

    public void UpdateConfig(ClipCommandConfig config) => _config = config;

    public void AttachDiscordService(DiscordClipService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _discord ??= service;
    }

    public async Task<bool> HandleMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        var config = _config;
        if (!config.Enabled ||
            !TryParseCommand(message.Text, config, out var rawTitle))
            return false;

        Console.WriteLine(
            $"Clip-Command von {message.UserLogin}: {ClipTemplateService.SanitizeTitle(rawTitle, config.MaximumTitleLength)}");

        if (!_hasClipScope)
        {
            await SendAsync(config.ChatMessages.MissingScope, message,
                null, 0, cancellationToken);
            ReportError("Twitch-Scope clips:edit fehlt.");
            return true;
        }

        var permission = message.CommandAuthorization == CommandAuthorization.Allowed
            ? new ClipPermissionDecision(true, "command-override")
            : await _permissions.CheckAsync(
                message, _broadcasterId, config, cancellationToken);
        if (!permission.Allowed)
        {
            Console.WriteLine(
                $"Clip-Command von {message.UserLogin} abgelehnt: {permission.Reason}.");
            await SendAsync(config.ChatMessages.Forbidden, message,
                null, 0, cancellationToken);
            return true;
        }

        TwitchLiveStream? stream;
        try
        {
            stream = await _twitchClient.GetLiveStreamAsync(
                _broadcasterId, cancellationToken);
        }
        catch (Exception exception)
        {
            ReportError("Twitch-Livestatus konnte nicht geprüft werden: " +
                        SafeError(exception));
            await SendAsync(config.ChatMessages.TwitchError, message,
                null, 0, cancellationToken);
            return true;
        }

        if (stream is null || !stream.IsLive)
        {
            await SendAsync(config.ChatMessages.Offline, message,
                null, 0, cancellationToken);
            return true;
        }

        if (!config.QueueEnabled &&
            (Volatile.Read(ref _processing) > 0 || Volatile.Read(ref _pending) > 0))
        {
            await SendAsync(config.ChatMessages.Busy, message,
                null, 0, cancellationToken);
            return true;
        }
        if (Volatile.Read(ref _pending) >=
            Math.Clamp(config.MaximumQueueSize, 1, 100))
        {
            await SendAsync(config.ChatMessages.QueueFull, message,
                null, 0, cancellationToken);
            return true;
        }


        var now = DateTimeOffset.UtcNow;
        var title = _templates.CreateTitle(
            rawTitle, config.DefaultTitle, config.MaximumTitleLength,
            message, stream, now);
        var userKey = string.IsNullOrWhiteSpace(message.UserId)
            ? message.UserLogin : message.UserId;
        var cooldown = _cooldowns.TryAccept(
            stream.Id, userKey, now, config);
        if (!cooldown.Accepted)
        {
            Console.WriteLine(
                $"Clip-Command von {message.UserLogin} abgelehnt: {cooldown.Reason}.");
            var response = cooldown.Reason.EndsWith("limit",
                    StringComparison.Ordinal)
                ? config.ChatMessages.LimitReached
                : config.ChatMessages.Cooldown;
            await SendAsync(response, message, null,
                cooldown.RemainingSeconds, cancellationToken);
            return true;
        }

        var request = new ClipCommandRequest(message, title, stream, now);
        Interlocked.Increment(ref _pending);
        if (!_queue.Writer.TryWrite(request))
        {
            Interlocked.Decrement(ref _pending);
            await SendAsync(config.ChatMessages.QueueFull, message,
                null, 0, cancellationToken);
            return true;
        }

        await SendAsync(config.ChatMessages.Starting, message,
            null, 0, cancellationToken);
        return true;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _pending);
            Interlocked.Exchange(ref _processing, 1);
            try
            {
                await ProcessAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ReportError("Clip-Verarbeitung ist fehlgeschlagen: " +
                            SafeError(exception));
            }
            finally
            {
                Interlocked.Exchange(ref _processing, 0);
            }
        }
    }

    private async Task ProcessAsync(
        ClipCommandRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var clip = await _clips.CreateAndWaitAsync(
                new TwitchClipRequest(
                    _broadcasterId,
                    request.Title,
                    Math.Clamp(_config.DurationSeconds, 5, 60)),
                cancellationToken);
            var userKey = string.IsNullOrWhiteSpace(request.Message.UserId)
                ? request.Message.UserLogin : request.Message.UserId;
            _cooldowns.RecordSuccess(request.Stream.Id, userKey);
            Console.WriteLine(
                $"Twitch-Clip {clip.Id} erfolgreich erstellt von {request.Message.UserLogin}: {request.Title}");

            var context = new ClipDiscordContext(
                clip,
                request.Title,
                request.Message.UserName,
                _channelName,
                request.Stream.GameName,
                DateTimeOffset.Now,
                _thumbnailUrl);
            DiscordClipPostResult? discordResult = null;
            if (_discord is not null)
                discordResult = await _discord.PostClipAsync(
                    context, cancellationToken);

            if (discordResult is { FailedChannels: > 0 })
                await SendAsync(_config.ChatMessages.PartialDiscord,
                    request.Message, context, 0, cancellationToken);
            else if (discordResult is { AnySuccess: true })
                await SendAsync(_config.ChatMessages.SuccessDiscord,
                    request.Message, context, 0, cancellationToken);
            else
                await SendAsync(_config.ChatMessages.Success,
                    request.Message, context, 0, cancellationToken);

            var discordError = discordResult is null
                ? ""
                : string.Join("; ", discordResult.Deliveries
                    .Where(delivery => !delivery.Success)
                    .Select(delivery => string.IsNullOrWhiteSpace(delivery.ChannelId)
                        ? delivery.Error
                        : $"Channel {delivery.ChannelId}: {delivery.Error}"));
            StatusChanged?.Invoke(new ClipCommandStatus(
                DateTimeOffset.Now,
                clip.Id,
                clip.Url,
                discordResult?.SuccessfulChannels ?? 0,
                discordResult?.FailedChannels ?? 0,
                discordError));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = SafeError(exception);
            Console.WriteLine(
                $"Clip-Erstellung für {request.Message.UserLogin} fehlgeschlagen: {error}");
            await SendAsync(_config.ChatMessages.TwitchError,
                request.Message, null, 0, cancellationToken);
            ReportError(error);
        }
    }

    public static bool TryParseCommand(
        string text,
        ClipCommandConfig config,
        out string title)
    {
        title = "";
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        var separator = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        var command = separator < 0 ? trimmed : trimmed[..separator];
        var commands = new[] { config.Command }
            .Concat(config.Aliases ?? new List<string>());
        if (!commands.Any(item =>
                command.Equals(item?.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;
        title = separator < 0 ? "" : trimmed[(separator + 1)..].Trim();
        return true;
    }

    private async Task SendAsync(
        ClipChatMessage configured,
        ChatMessage message,
        ClipDiscordContext? context,
        int remainingSeconds,
        CancellationToken cancellationToken)
    {
        if (!_config.ChatResponsesEnabled ||
            !configured.Enabled ||
            string.IsNullOrWhiteSpace(configured.Text)) return;
        var text = configured.Text
            .Replace("{username}", message.UserName,
                StringComparison.OrdinalIgnoreCase)
            .Replace("{channel}", _channelName,
                StringComparison.OrdinalIgnoreCase)
            .Replace("{remainingSeconds}", remainingSeconds.ToString(),
                StringComparison.OrdinalIgnoreCase);
        if (context is not null)
            text = _templates.ApplyClipTemplate(text, context);
        try
        {
            await _chat.SendChatMessageAsync(
                _broadcasterId, _chatSenderId, text, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "Clip-Chatantwort konnte nicht gesendet werden: " +
                SafeError(exception));
        }
    }

    private void ReportError(string error) =>
        StatusChanged?.Invoke(new ClipCommandStatus(
            DateTimeOffset.Now, "", "", 0, 0, error));

    private static string SafeError(Exception exception) =>
        exception switch
        {
            TimeoutException => exception.Message,
            HttpRequestException => exception.Message,
            InvalidOperationException => exception.Message,
            TaskCanceledException => "Die Anfrage wurde abgebrochen.",
            _ => "Unbekannter Clip-Fehler."
        };

    public void Dispose()
    {
        if (_disposed) return;
        _queue.Writer.TryComplete();
        _discord?.Dispose();
        _disposed = true;
    }
}

public sealed record ClipCommandStatus(
    DateTimeOffset Timestamp,
    string ClipId,
    string ClipUrl,
    int DiscordSuccessCount,
    int DiscordFailureCount,
    string Error);