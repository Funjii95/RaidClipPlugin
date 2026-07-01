using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class LocalPlayerServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly int _port;
    private bool _disposed;

    public LocalPlayerServer(int port = 5055)
    {
        _port = port;
        _listener.Prefixes.Add(IdleUrl);
    }

    public string IdleUrl => $"http://127.0.0.1:{_port}/";

    public string GetClipUrl(
        Clip clip,
        int volumePercent = 100)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (string.IsNullOrWhiteSpace(clip.Id))
        {
            throw new ArgumentException(
                "Der Clip enthält keine Twitch-Clip-ID.",
                nameof(clip));
        }

        var volume = Math.Clamp(volumePercent, 0, 100);

        return $"{IdleUrl}clip" +
               $"?clip={Uri.EscapeDataString(clip.Id)}" +
               $"&video={Uri.EscapeDataString(clip.VideoUrl)}" +
               $"&volume={volume}";
    }

    public string GetClipUrl(
        string videoUrl,
        int volumePercent = 100)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            throw new ArgumentException(
                "Die Video-URL darf nicht leer sein.",
                nameof(videoUrl));
        }

        var volume = Math.Clamp(volumePercent, 0, 100);

        return $"{IdleUrl}clip" +
               $"?video={Uri.EscapeDataString(videoUrl)}" +
               $"&volume={volume}";
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LocalPlayerServer));
        }

        if (_listener.IsListening)
        {
            return;
        }

        _listener.Start();
        Console.WriteLine($"🌐 LocalPlayer läuft auf {IdleUrl}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var contextTask = _listener.GetContextAsync();
                var cancellationTask = Task.Delay(
                    Timeout.Infinite,
                    cancellationToken);

                var completedTask = await Task.WhenAny(
                    contextTask,
                    cancellationTask);

                if (completedTask != contextTask)
                {
                    break;
                }

                var context = await contextTask;
                _ = HandleRequestAsync(context);
            }
        }
        catch (HttpListenerException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Stop();
        }
    }

    public void Stop()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
    }

    private static async Task HandleRequestAsync(
        HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            var clipId = context.Request.QueryString["clip"];
            var videoUrl = context.Request.QueryString["video"];
            var volumeText = context.Request.QueryString["volume"];
            var volumePercent = int.TryParse(volumeText, out var parsedVolume)
                ? Math.Clamp(parsedVolume, 0, 100)
                : 100;

            var hasClip = !string.IsNullOrWhiteSpace(clipId) ||
                          !string.IsNullOrWhiteSpace(videoUrl);

            var html = path.Equals(
                           "/clip",
                           StringComparison.OrdinalIgnoreCase) &&
                       hasClip
                ? CreatePlayerPage(videoUrl, clipId, volumePercent)
                : CreateIdlePage();

            var bytes = Encoding.UTF8.GetBytes(html);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = bytes.Length;

            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"❌ LocalPlayer-Fehler: {exception.Message}");

            try
            {
                context.Response.StatusCode = 500;
            }
            catch
            {
            }
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
            }
        }
    }

    private static string CreatePlayerPage(
        string? videoUrl,
        string? clipId,
        int volumePercent)
    {
        var safeVideoUrl = JsonSerializer.Serialize(videoUrl ?? "");
        var safeClipId = JsonSerializer.Serialize(clipId ?? "");
        var safePlayerVolume = (Math.Clamp(volumePercent, 0, 100) / 100d)
            .ToString("0.##", CultureInfo.InvariantCulture);

        return $$"""
        <!DOCTYPE html>
        <html lang="de">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width">
            <style>
                html,
                body {
                    width: 100%;
                    height: 100%;
                    margin: 0;
                    overflow: hidden;
                    background: transparent;
                }

                video {
                    width: 100%;
                    height: 100%;
                    object-fit: contain;
                }
            </style>
        </head>
        <body>
            <video id="player" autoplay playsinline></video>

            <script>
                const directUrl = {{safeVideoUrl}};
                const clipId = {{safeClipId}};
                const player = document.getElementById("player");
                let fallbackStarted = false;

                function useTwitchPlayer() {
                    if (fallbackStarted || !clipId) {
                        return;
                    }

                    fallbackStarted = true;

                    const twitchUrl =
                        "https://clips.twitch.tv/embed?clip=" +
                        encodeURIComponent(clipId) +
                        "&parent=127.0.0.1" +
                        "&autoplay=true" +
                        "&muted=" + ({{safePlayerVolume}} <= 0 ? "true" : "false") +
                        "&preload=auto";

                    window.location.replace(twitchUrl);
                }

                player.volume = {{safePlayerVolume}};
                player.muted = {{safePlayerVolume}} <= 0;
                player.addEventListener("error", useTwitchPlayer, { once: true });

                if (directUrl) {
                    player.src = directUrl;
                    player.play().catch(useTwitchPlayer);

                    window.setTimeout(() => {
                        if (player.readyState === 0) {
                            useTwitchPlayer();
                        }
                    }, 3000);
                } else {
                    useTwitchPlayer();
                }
            </script>
        </body>
        </html>
        """;
    }

    private static string CreateIdlePage()
    {
        return """
        <!DOCTYPE html>
        <html lang="de">
        <head>
            <meta charset="utf-8">
            <style>
                html,
                body {
                    width: 100%;
                    height: 100%;
                    margin: 0;
                    background: transparent;
                }
            </style>
        </head>
        <body></body>
        </html>
        """;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _listener.Close();
        _disposed = true;
    }
}
