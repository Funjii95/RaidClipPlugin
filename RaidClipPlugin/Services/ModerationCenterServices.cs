using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class LinkDetectionService
{
    private static readonly Regex UrlRegex = new(@"(?i)\b((?:https?://|www\.)[^\s<>()]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BareDomainRegex = new(@"(?i)(?<![\w@])([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:(?:\s*(?:\.|\[.\]|\(.\)|dot)\s*)[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)+)(/[^\s]*)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> KnownTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "com","de","net","org","tv","gg","io","me","co","be","app","dev","live","stream","social",
        "xyz","link","info","eu","at","ch","nl","fr","it","es","uk","us","jp","to","fm","ai","cloud",
        "shop","store","online","site","world","club","games","music","video"
    };

    public LinkDetectionResult Detect(string? text, LinkFilterConfig settings)
    {
        var input = (text ?? string.Empty).Trim();
        if (input.Length == 0) return new(Array.Empty<LinkMatch>());
        var links = new List<LinkMatch>();
        foreach (Match match in UrlRegex.Matches(input)) AddIfValid(links, match.Value, match.Value, false);
        if (settings.DetectBareDomains)
        {
            foreach (Match match in BareDomainRegex.Matches(input))
            {
                var raw = match.Value.Trim(' ', '.', ',', ';', ':', '!', '?', ')', ']', '}');
                var isObfuscated = raw.Contains("dot", StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains("[.]", StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains("(.)", StringComparison.OrdinalIgnoreCase) ||
                    Regex.IsMatch(raw, @"\s\.\s");
                if (isObfuscated && !settings.DetectObfuscatedLinks) continue;
                AddIfValid(links, raw, NormalizeObfuscated(raw), isObfuscated);
            }
        }
        return new(links.GroupBy(x => x.NormalizedText, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray());
    }

    private static void AddIfValid(List<LinkMatch> links, string raw, string normalized, bool obfuscated)
    {
        var domain = ExtractDomain(normalized);
        if (domain.Length == 0 || !HasKnownTld(domain)) return;
        links.Add(new LinkMatch(raw, normalized, domain, obfuscated));
    }

    public static string ExtractDomain(string value)
    {
        var candidate = NormalizeObfuscated(value).Trim();
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            candidate = "https://" + candidate.TrimStart('/');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return string.Empty;
        var host = uri.Host.Trim('.').ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    public static string NormalizeObfuscated(string value) =>
        Regex.Replace(value, @"(?i)\s*(?:\[.\]|\(.\)|\bdot\b|\s\.\s)\s*", ".").Replace(" ", string.Empty).Trim();

    private static bool HasKnownTld(string domain)
    {
        var parts = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && KnownTlds.Contains(parts[^1]);
    }
}

public sealed class DomainRuleMatcher
{
    public bool IsMatch(IEnumerable<string> rules, string domain)
    {
        var d = NormalizeDomain(domain);
        if (d.Length == 0) return false;
        foreach (var rule in rules ?? Array.Empty<string>())
        {
            var r = NormalizeDomain(rule);
            if (r.Length == 0) continue;
            if (d.Equals(r, StringComparison.OrdinalIgnoreCase) || d.EndsWith("." + r, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string NormalizeDomain(string value)
    {
        var text = (value ?? "").Trim().Trim('*').Trim('.').ToLowerInvariant();
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            var extracted = LinkDetectionService.ExtractDomain(text);
            return extracted.Length == 0 ? text : extracted;
        }
        return text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? text[4..] : text;
    }
}

public sealed class PermitService
{
    private readonly ConcurrentDictionary<string, PermitEntry> _permits = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public PermitEntry Grant(string channelId, string channelName, string userId, string userLogin, string displayName,
        string grantedById, string grantedByName, TimeSpan duration, PermitMode mode, string source)
    {
        var key = Key(channelId, userId, userLogin);
        lock (_sync)
        {
            var entry = new PermitEntry
            {
                ChannelId = channelId, ChannelName = channelName, TwitchUserId = userId,
                UserLogin = NormalizeLogin(userLogin), DisplayName = string.IsNullOrWhiteSpace(displayName) ? userLogin.TrimStart('@') : displayName,
                GrantedById = grantedById, GrantedByName = grantedByName, CreatedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(duration), Mode = mode, Source = source
            };
            _permits[key] = entry;
            return Clone(entry);
        }
    }

    public bool Revoke(string channelId, string userId, string userLogin, out PermitEntry? entry)
    {
        lock (_sync)
        {
            if (_permits.TryRemove(Key(channelId, userId, userLogin), out var removed))
            {
                removed.Revoked = true;
                entry = Clone(removed);
                return true;
            }
        }
        entry = null;
        return false;
    }

    public bool TryConsume(string channelId, string userId, string userLogin, string messageId, int linkCount,
        out PermitEntry? entry, out string reason)
    {
        lock (_sync)
        {
            RemoveExpiredUnsafe(DateTimeOffset.UtcNow);
            if (!_permits.TryGetValue(Key(channelId, userId, userLogin), out var current))
            {
                entry = null; reason = "Keine aktive Linkfreigabe."; return false;
            }
            if (current.Mode == PermitMode.SingleLink && linkCount > 1)
            {
                entry = Clone(current); reason = "Diese Freigabe erlaubt nur einen Link."; return false;
            }
            if (current.Mode is PermitMode.SingleMessage or PermitMode.SingleLink)
            {
                current.Used = true; current.UsedAtUtc = DateTimeOffset.UtcNow; current.UsedMessageId = messageId;
                _permits.TryRemove(Key(channelId, userId, userLogin), out _);
            }
            entry = Clone(current); reason = "Linkfreigabe verwendet."; return true;
        }
    }

    public static string NormalizeLogin(string value) => (value ?? "").Trim().TrimStart('@').ToLowerInvariant();
    private static string Key(string channelId, string userId, string userLogin) =>
        channelId.Trim() + ":" + (!string.IsNullOrWhiteSpace(userId) ? "id:" + userId.Trim() : "login:" + NormalizeLogin(userLogin));
    private void RemoveExpiredUnsafe(DateTimeOffset now)
    {
        foreach (var pair in _permits.ToArray())
            if (pair.Value.ExpiresAtUtc <= now || pair.Value.Revoked || pair.Value.Used) _permits.TryRemove(pair.Key, out _);
    }
    private static PermitEntry Clone(PermitEntry s) => new()
    {
        TwitchUserId=s.TwitchUserId,UserLogin=s.UserLogin,DisplayName=s.DisplayName,ChannelId=s.ChannelId,ChannelName=s.ChannelName,
        GrantedById=s.GrantedById,GrantedByName=s.GrantedByName,CreatedAtUtc=s.CreatedAtUtc,ExpiresAtUtc=s.ExpiresAtUtc,
        Mode=s.Mode,Used=s.Used,Revoked=s.Revoked,UsedAtUtc=s.UsedAtUtc,UsedMessageId=s.UsedMessageId,Source=s.Source,Note=s.Note
    };
}

public sealed class ModerationHistoryService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _path;
    public ModerationHistoryService(string? path = null) =>
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaidClipPlugin", "moderation-history.jsonl");
    public async Task AppendAsync(ModerationActionEntry entry, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        await _lock.WaitAsync(cancellationToken);
        try { await File.AppendAllTextAsync(_path, JsonSerializer.Serialize(entry) + Environment.NewLine, cancellationToken); }
        finally { _lock.Release(); }
    }
}

public sealed class LinkModerationService
{
    private readonly LinkDetectionService _detector = new();
    private readonly DomainRuleMatcher _domains = new();
    private readonly PermitService _permits;
    private readonly ModerationHistoryService _history;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWarning = new(StringComparer.OrdinalIgnoreCase);
    public LinkModerationService(PermitService permits, ModerationHistoryService history) { _permits = permits; _history = history; }

    public async Task ProcessAsync(ChatMessage message, AppConfig config, ChatModerationService? moderation, TwitchService twitch,
        string broadcasterId, string broadcasterLogin, string moderatorId, Func<ChatMessage, bool> musicBypass, Action<string> log, CancellationToken token)
    {
        if (!config.Moderation.Enabled || string.IsNullOrWhiteSpace(message.Text) || message.IsBot) return;
        if (await TryHandlePermitCommandAsync(message, config, twitch, broadcasterId, broadcasterLogin, moderatorId, log, token)) return;
        var filter = config.Moderation.LinkFilter;
        if (!filter.Enabled) return;
        if (musicBypass(message))
        {
            await RecordAsync(message, broadcasterId, "Musikwunsch-Ausnahme", "Linkfilter Ã¼bersprungen", "Allowed", token);
            log($"Linkfilter: Musikwunsch von {message.UserName} wurde ausgenommen.");
            return;
        }
        if (IsRoleExempt(message, config) || IsUserWhitelisted(message, config)) return;
        var detected = _detector.Detect(message.Text, filter);
        if (!detected.HasLinks) return;
        var blacklisted = detected.Links.FirstOrDefault(x => _domains.IsMatch(filter.BlacklistedDomains, x.Domain));
        if (blacklisted is null && detected.Links.All(x => _domains.IsMatch(filter.WhitelistedDomains, x.Domain)))
        {
            await RecordAsync(message, broadcasterId, "Link erlaubt", "Domain-Whitelist", "Allowed", token);
            return;
        }
        if (blacklisted is null && _permits.TryConsume(broadcasterId, message.UserId, message.UserLogin, message.Id, detected.Links.Count, out _, out var permitReason))
        {
            await RecordAsync(message, broadcasterId, "Permit verwendet", permitReason, "Allowed", token);
            log($"Linkfilter: Link von {message.UserName} durch !permit erlaubt.");
            return;
        }
        message.CommandAuthorization = CommandAuthorization.Denied;
        var domain = blacklisted?.Domain ?? detected.Links[0].Domain;
        var reason = blacklisted is null ? "Nicht freigegebener Link" : "Blacklisted Domain: " + domain;
        log($"Linkfilter: Nachricht von {message.UserName} blockiert ({reason}).");
        try
        {
            if (moderation is not null && filter.Action != LinkModerationAction.LogOnly) await moderation.DeleteMessageAsync(message.Id, token);
            if (moderation is not null && filter.Action is LinkModerationAction.DeleteAndTimeout or LinkModerationAction.DeleteWarnAndTimeout)
                await moderation.TimeoutUserAsync(message.UserId, Math.Clamp(filter.TimeoutSeconds, 1, 1_209_600), "Unerlaubter Link Ã¼ber RaidClipPlugin", token);
            if (filter.BotResponseEnabled && filter.Action is LinkModerationAction.DeleteAndWarn or LinkModerationAction.DeleteWarnAndTimeout)
                await SendWarningAsync(twitch, broadcasterId, moderatorId, message, config, domain, token);
            await RecordAsync(message, broadcasterId, "Link blockiert", reason, "Success", token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log("Linkfilter-Moderation fehlgeschlagen: " + ex.Message);
            await RecordAsync(message, broadcasterId, "Link blockiert", reason, "Failed: " + ex.Message, token);
        }
    }

    private async Task<bool> TryHandlePermitCommandAsync(ChatMessage message, AppConfig config, TwitchService twitch, string broadcasterId,
        string broadcasterLogin, string moderatorId, Action<string> log, CancellationToken token)
    {
        var permit = config.Moderation.Permit;
        var parsed = ChatCommandParser.Parse(message.Text);
        if (!parsed.IsCommand) return false;
        var isPermit = permit.Enabled && parsed.Command.Equals(CommandRegistry.Normalize(permit.Command).TrimStart('!'), StringComparison.OrdinalIgnoreCase);
        var isUnpermit = permit.UnpermitEnabled && parsed.Command.Equals(CommandRegistry.Normalize(permit.UnpermitCommand).TrimStart('!'), StringComparison.OrdinalIgnoreCase);
        if (!isPermit && !isUnpermit) return false;
        message.CommandAuthorization = CommandAuthorization.Denied;
        if (!IsPermitOperator(message, config))
        {
            log($"Moderation: {message.UserName} darf {parsed.Prefix}{parsed.Command} nicht ausfÃ¼hren.");
            if (permit.ReplyOnDenied) await SafeSendAsync(twitch, broadcasterId, moderatorId, $"@{message.UserName}, dafÃ¼r brauchst du Moderatorrechte.", token);
            return true;
        }
        var args = parsed.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (args.Length == 0)
        {
            await SafeSendAsync(twitch, broadcasterId, moderatorId, $"@{message.UserName}, Nutzung: {permit.Command} @Benutzer [30s|5m|1h].", token);
            return true;
        }
        var target = PermitService.NormalizeLogin(args[0]);
        if (isUnpermit)
        {
            var text = _permits.Revoke(broadcasterId, "", target, out _)
                ? $"Die Linkfreigabe fÃ¼r @{target} wurde aufgehoben."
                : $"FÃ¼r @{target} besteht derzeit keine aktive Linkfreigabe.";
            await SafeSendAsync(twitch, broadcasterId, moderatorId, text, token);
            log("Moderation: " + text);
            return true;
        }
        if (!TryParseDuration(args.Length > 1 ? args[1] : "", permit, out var duration, out var error))
        {
            await SafeSendAsync(twitch, broadcasterId, moderatorId, $"@{message.UserName}, {error}", token);
            return true;
        }
        _permits.Grant(broadcasterId, broadcasterLogin, "", target, target, message.UserId, message.UserName, duration, permit.Mode, "Chat-Command");
        await RecordAsync(message, broadcasterId, "Permit erteilt", target, "Success", token);
        if (permit.ReplyOnSuccess) await SafeSendAsync(twitch, broadcasterId, moderatorId, $"@{target} darf fÃ¼r {(int)duration.TotalSeconds} Sekunden Links posten.", token);
        return true;
    }

    private static bool TryParseDuration(string raw, PermitConfig permit, out TimeSpan duration, out string error)
    {
        if (string.IsNullOrWhiteSpace(raw)) { duration = TimeSpan.FromSeconds(permit.DefaultDurationSeconds); error = ""; return true; }
        raw = raw.Trim().ToLowerInvariant();
        var multiplier = raw.EndsWith('h') ? 3600 : raw.EndsWith('m') ? 60 : 1;
        if (raw.EndsWith('h') || raw.EndsWith('m') || raw.EndsWith('s')) raw = raw[..^1];
        if (!int.TryParse(raw, out var value) || value <= 0) { duration = default; error = "UngÃ¼ltige Dauer. Beispiele: 30s, 5m oder 1h."; return false; }
        var seconds = value * multiplier;
        if (seconds < permit.MinimumDurationSeconds) { duration = default; error = $"Die Dauer muss mindestens {permit.MinimumDurationSeconds} Sekunden betragen."; return false; }
        if (seconds > permit.MaximumDurationSeconds) { duration = default; error = $"Die Dauer darf maximal {permit.MaximumDurationSeconds} Sekunden betragen."; return false; }
        duration = TimeSpan.FromSeconds(seconds); error = ""; return true;
    }

    private static bool IsPermitOperator(ChatMessage m, AppConfig c) => m.IsBroadcaster || m.IsModerator ||
        (c.Moderation.Permit.AllowVips && m.IsVip) || (c.Moderation.Permit.AllowSubscribers && m.IsSubscriber) || IsUserWhitelisted(m, c);
    private static bool IsRoleExempt(ChatMessage m, AppConfig c)
    {
        var f = c.Moderation.LinkFilter;
        return (f.ExemptBroadcaster && m.IsBroadcaster) || (f.ExemptModerators && m.IsModerator) ||
            (f.ExemptVips && m.IsVip) || (f.ExemptSubscribers && m.IsSubscriber);
    }
    private static bool IsUserWhitelisted(ChatMessage m, AppConfig c)
    {
        var login = PermitService.NormalizeLogin(m.UserLogin.Length > 0 ? m.UserLogin : m.UserName);
        return c.Moderation.UserWhitelist.Concat(c.Moderation.LinkFilter.KnownBotLogins)
            .Any(x => PermitService.NormalizeLogin(x).Equals(login, StringComparison.OrdinalIgnoreCase));
    }
    private async Task SendWarningAsync(TwitchService twitch, string broadcasterId, string moderatorId, ChatMessage message, AppConfig config, string domain, CancellationToken token)
    {
        var key = broadcasterId + ":" + message.UserId + ":link";
        var now = DateTimeOffset.UtcNow;
        if (_lastWarning.TryGetValue(key, out var old) && now - old < TimeSpan.FromSeconds(config.Moderation.LinkFilter.BotResponseCooldownSeconds)) return;
        _lastWarning[key] = now;
        var text = config.Moderation.LinkFilter.WarningTemplate
            .Replace("{user}", message.UserName).Replace("{duration}", config.Moderation.Permit.DefaultDurationSeconds.ToString())
            .Replace("{reason}", "unerlaubter Link").Replace("{command}", config.Moderation.Permit.Command).Replace("{domain}", domain);
        await SafeSendAsync(twitch, broadcasterId, moderatorId, text, token);
    }
    private static async Task SafeSendAsync(TwitchService twitch, string broadcasterId, string senderId, string text, CancellationToken token)
    {
        try { if (!string.IsNullOrWhiteSpace(text)) await twitch.SendChatMessageAsync(broadcasterId, senderId, text, token); }
        catch (Exception ex) when (ex is not OperationCanceledException) { Console.WriteLine("Moderations-Chatantwort fehlgeschlagen: " + ex.Message); }
    }
    private Task RecordAsync(ChatMessage message, string broadcasterId, string action, string reason, string result, CancellationToken token) =>
        _history.AppendAsync(new ModerationActionEntry
        {
            ChannelId = broadcasterId, UserId = message.UserId, UserName = message.UserName, Action = action, Reason = reason,
            Source = "RaidClip Moderationscenter", TimestampUtc = DateTimeOffset.UtcNow, MessageId = message.Id,
            MessagePreview = Regex.Replace(message.Text ?? "", @"\s+", " ").Trim() is var p && p.Length > 180 ? p[..177] + "..." : p,
            Result = result, Rule = "Linkfilter"
        }, token);
}
