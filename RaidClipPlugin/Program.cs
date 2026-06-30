using RaidClipPlugin.Services;

namespace RaidClipPlugin;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== RaidClipPlugin ===");
        Console.WriteLine();

        // Konfiguration laden
        var configurationService = new ConfigurationService();
        var config = configurationService.Load();

        // Raider bestimmen
        string raider = args.Length > 0
            ? args[0]
            : "funjii";

        Console.WriteLine($"Raider: {raider}");
        Console.WriteLine();

        // Twitch authentifizieren
        var auth = new AuthenticationService(config);
        var accessToken = await auth.GetAccessTokenAsync();

        // Twitch Service erstellen
        var twitch = new TwitchService(
            config.Twitch.ClientId,
            accessToken);

        // User-ID laden
        var userId = await twitch.GetUserIdAsync(raider);

        if (string.IsNullOrWhiteSpace(userId))
        {
            Console.WriteLine("❌ Keine User-ID gefunden.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"User ID: {userId}");
        Console.WriteLine();

        // Zufälligen Clip holen
        var clipService = new ClipService(twitch.HttpClient);

        var clip = await clipService.GetRandomClipAsync(userId);

        if (clip == null)
        {
            Console.WriteLine("❌ Keine Clips gefunden.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("=== Zufälliger Clip ===");
        Console.WriteLine($"Titel : {clip.Title}");
        Console.WriteLine($"URL   : {clip.Url}");
        Console.WriteLine($"Embed : {clip.EmbedUrl}");
        Console.WriteLine();

        // OBS verbinden
        var obs = new ObsService(config);

        obs.Connect();

        // Browserquelle auf den Clip setzen
        obs.SetBrowserUrl(clip.EmbedUrl);

        Console.WriteLine();
        Console.WriteLine("Fertig.");
        Console.WriteLine();
        Console.WriteLine("Taste drücken...");
        Console.ReadKey();
    }
}