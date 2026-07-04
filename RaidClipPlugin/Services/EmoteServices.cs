using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record ExternalEmote(string Code, string Url, bool Animated, string Provider, string StaticUrl = "");

public interface IExternalEmoteProvider
{
    string Name { get; }
    Task<IReadOnlyDictionary<string, ExternalEmote>> LoadAsync(string channelId, CancellationToken cancellationToken);
}

public sealed class BttvEmoteProvider : IExternalEmoteProvider
{
    private readonly HttpClient _http;
    public string Name => "BTTV";
    public BttvEmoteProvider(HttpClient http) => _http = http;

    public async Task<IReadOnlyDictionary<string, ExternalEmote>> LoadAsync(string channelId, CancellationToken token)
    {
        var result = new Dictionary<string, ExternalEmote>(StringComparer.Ordinal);
        using (var global = await _http.GetAsync("https://api.betterttv.net/3/cached/emotes/global", token))
            if (global.IsSuccessStatusCode) AddArray(result, JsonDocument.Parse(await global.Content.ReadAsStringAsync(token)).RootElement);
        using (var channel = await _http.GetAsync("https://api.betterttv.net/3/cached/users/twitch/" + Uri.EscapeDataString(channelId), token))
            if (channel.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await channel.Content.ReadAsStringAsync(token));
                if (document.RootElement.TryGetProperty("channelEmotes", out var own)) AddArray(result, own);
                if (document.RootElement.TryGetProperty("sharedEmotes", out var shared)) AddArray(result, shared);
            }
        Console.WriteLine($"{result.Count} BTTV-Emotes geladen.");
        return result;
    }

    private static void AddArray(Dictionary<string, ExternalEmote> result, JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? "" : "";
            var code = item.TryGetProperty("code", out var codeValue) ? codeValue.GetString() ?? "" : "";
            if (id.Length == 0 || code.Length == 0) continue;
            var animated = (item.TryGetProperty("imageType", out var type) && type.GetString() == "gif") ||
                (item.TryGetProperty("animated", out var animatedValue) && animatedValue.GetBoolean());
            var imageUrl = $"https://cdn.betterttv.net/emote/{id}/2x";
            result[code] = new ExternalEmote(code, imageUrl, animated, "BTTV", imageUrl);
        }
    }
}

public sealed class SevenTvEmoteProvider : IExternalEmoteProvider
{
    private readonly HttpClient _http;
    public string Name => "7TV";
    public SevenTvEmoteProvider(HttpClient http) => _http = http;

    public async Task<IReadOnlyDictionary<string, ExternalEmote>> LoadAsync(string channelId, CancellationToken token)
    {
        var result = new Dictionary<string, ExternalEmote>(StringComparer.Ordinal);
        await LoadSetAsync("https://7tv.io/v3/emote-sets/global", result, token);
        using var response = await _http.GetAsync("https://7tv.io/v3/users/twitch/" + Uri.EscapeDataString(channelId), token);
        if (response.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (document.RootElement.TryGetProperty("emote_set", out var set)) AddSet(set, result);
        }
        Console.WriteLine($"{result.Count} 7TV-Emotes geladen.");
        return result;
    }

    private async Task LoadSetAsync(string url, Dictionary<string, ExternalEmote> result, CancellationToken token)
    {
        using var response = await _http.GetAsync(url, token);
        if (!response.IsSuccessStatusCode) return;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        AddSet(document.RootElement, result);
    }

    private static void AddSet(JsonElement set, Dictionary<string, ExternalEmote> result)
    {
        if (!set.TryGetProperty("emotes", out var emotes)) return;
        foreach (var item in emotes.EnumerateArray())
        {
            var code = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
            if (code.Length == 0 || !item.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("host", out var host)) continue;
            var baseUrl = host.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "";
            if (baseUrl.StartsWith("//")) baseUrl = "https:" + baseUrl;
            if (baseUrl.Length == 0) continue;
            var animated = data.TryGetProperty("animated", out var animatedValue) && animatedValue.GetBoolean();
            var animatedFile = FindGifFile(host, "name", "2x.gif");
            var staticFile = FindGifFile(host, "static_name", "2x_static.gif");
            if (animatedFile.Length == 0) animatedFile = animated ? staticFile : "";
            if (staticFile.Length == 0) staticFile = animatedFile;
            if (animatedFile.Length == 0) continue;
            result[code] = new ExternalEmote(code, baseUrl + "/" + animatedFile,
                animated, "7TV", baseUrl + "/" + staticFile);
        }
    }

    private static string FindGifFile(JsonElement host, string property, string preferred)
    {
        if (!host.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            return "";
        var candidates = files.EnumerateArray()
            .Select(file => file.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "")
            .Where(name => name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return candidates.FirstOrDefault(name => name.Equals(preferred, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(name => name.StartsWith("2x", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault() ?? "";
    }
}

public sealed class EmoteCatalogService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly IReadOnlyList<IExternalEmoteProvider>? _injectedProviders;
    public EmoteCatalogService(IEnumerable<IExternalEmoteProvider>? providers = null) =>
        _injectedProviders = providers?.ToArray();
    private IReadOnlyDictionary<string, ExternalEmote> _emotes = new Dictionary<string, ExternalEmote>();
    public IReadOnlyDictionary<string, ExternalEmote> Emotes => _emotes;

    public async Task InitializeAsync(string channelId, LiveChatConfig config, CancellationToken token)
    {
        var merged = new Dictionary<string, ExternalEmote>(StringComparer.Ordinal);
        var providers = _injectedProviders ?? new IExternalEmoteProvider[]
        { new BttvEmoteProvider(_http), new SevenTvEmoteProvider(_http) };
        foreach (var provider in providers)
        {
            if (provider.Name.Equals("BTTV", StringComparison.OrdinalIgnoreCase) && !config.EnableBttvEmotes) continue;
            if (provider.Name.Equals("7TV", StringComparison.OrdinalIgnoreCase) && !config.EnableSevenTvEmotes) continue;
            try
            {
                foreach (var item in await provider.LoadAsync(channelId, token)) merged[item.Key] = item.Value;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { Console.WriteLine($"Fehler beim Laden von {provider.Name}-Emotes: {exception.Message}"); }
        }
        _emotes = merged;
    }

    public void Dispose() => _http.Dispose();
}

public sealed class EmoteCacheService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly ConcurrentDictionary<string, Lazy<Task<Image?>>> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _failures = new(StringComparer.Ordinal);

    public Task<Image?> GetAsync(string url, bool cache, CancellationToken token)
    {
        if (_failures.ContainsKey(url)) return Task.FromResult<Image?>(null);
        return cache
            ? _cache.GetOrAdd(url, key => new Lazy<Task<Image?>>(() => DownloadAsync(key, token))).Value
            : DownloadAsync(url, token);
    }

    private async Task<Image?> DownloadAsync(string url, CancellationToken token)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, token);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _failures.TryAdd(url, 0);
            Console.WriteLine("Emote konnte nicht geladen werden; Text-Fallback wird verwendet: " + exception.Message);
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var lazy in _cache.Values)
            if (lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully) lazy.Value.Result?.Dispose();
        _http.Dispose();
    }
}

public sealed class ChatMessageRenderer
{
    private readonly EmoteCacheService _cache;
    public ChatMessageRenderer(EmoteCacheService cache) => _cache = cache;

    public async Task<Control> RenderAsync(LiveChatMessage message, LiveChatConfig config,
        IReadOnlyDictionary<string, ExternalEmote> external, CancellationToken token)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true,
            MaximumSize = new Size(1050, 0), BackColor = Color.FromArgb(18, 18, 20),
            ForeColor = Color.Gainsboro, Margin = new Padding(2), Padding = new Padding(5) };
        if (config.ShowTimestamps) row.Controls.Add(TextLabel($"[{message.Timestamp:HH:mm}] ", Color.Gray));
        if (config.ShowBadges)
        {
            var roles = new List<string>();
            if (message.IsBroadcaster) roles.Add("Broadcaster");
            if (message.IsModerator) roles.Add("Mod");
            if (message.IsVip) roles.Add("VIP");
            if (message.IsSubscriber) roles.Add("Sub");
            if (roles.Count > 0) row.Controls.Add(TextLabel("[" + string.Join("/", roles) + "] ", Color.OrangeRed));
        }
        var userColor = Color.IndianRed;
        if (config.ShowUserColors && !string.IsNullOrWhiteSpace(message.UserColor))
            try { userColor = ColorTranslator.FromHtml(message.UserColor); } catch { }
        row.Controls.Add(TextLabel(message.DisplayName + ": ", userColor, true));

        var native = message.Emotes.ToDictionary(x => x.Code, x =>
            new ExternalEmote(x.Code, $"https://static-cdn.jtvnw.net/emoticons/v2/{x.Id}/default/dark/2.0", false, "Twitch"), StringComparer.Ordinal);
        foreach (var tokenText in message.Message.Split(' '))
        {
            ExternalEmote? emote = null;
            if (config.EnableTwitchEmotes) native.TryGetValue(tokenText, out emote);
            if (emote is null) external.TryGetValue(tokenText, out emote);
            if (emote is null)
            { row.Controls.Add(TextLabel(tokenText + " ", Color.Gainsboro)); continue; }
            var imageUrl = emote.Animated && !config.EnableAnimatedEmotes
                ? emote.StaticUrl : emote.Url;
            if (string.IsNullOrWhiteSpace(imageUrl))
            { row.Controls.Add(TextLabel(tokenText + " ", Color.Gainsboro)); continue; }
            var image = await _cache.GetAsync(imageUrl, config.CacheEmotes, token);
            if (image is null) { row.Controls.Add(TextLabel(tokenText + " ", Color.Gainsboro)); continue; }
            row.Controls.Add(new PictureBox { Width = config.EmoteSize, Height = config.EmoteSize,
                SizeMode = PictureBoxSizeMode.Zoom, Image = image, Margin = new Padding(1) });
        }
        return row;
    }

    private static Label TextLabel(string text, Color color, bool bold = false) => new()
    { Text = text, AutoSize = true, ForeColor = color, BackColor = Color.Transparent,
      Font = new Font("Segoe UI", 9.5F, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 5, 0, 0) };
}
