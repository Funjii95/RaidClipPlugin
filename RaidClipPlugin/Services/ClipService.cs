using Newtonsoft.Json.Linq;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public class ClipService
{
    private readonly HttpClient _http;

    public ClipService(HttpClient http)
    {
        _http = http;
    }

    public async Task<Clip?> GetRandomClipAsync(string userId)
    {
        var response = await _http.GetAsync(
            $"https://api.twitch.tv/helix/clips?broadcaster_id={userId}&first=100");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();

        Console.WriteLine("========== CLIP JSON ==========");
        Console.WriteLine(json);
        Console.WriteLine("===============================");

        var data = JObject.Parse(json)["data"];

        if (data == null || !data.Any())
            return null;

        var clips = new List<Clip>();

        foreach (var clip in data)
        {
            clips.Add(new Clip
            {
                Id = clip["id"]?.ToString() ?? "",
                Url = clip["url"]?.ToString() ?? "",
                EmbedUrl = clip["embed_url"]?.ToString() ?? "",
                Title = clip["title"]?.ToString() ?? ""
            });
        }

        var random = new Random();

        return clips[random.Next(clips.Count)];
    }
}