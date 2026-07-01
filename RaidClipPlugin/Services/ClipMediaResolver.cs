using System.Net.Http.Json;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed class ClipMediaResolver
{
    private const string GraphQlUrl = "https://gql.twitch.tv/gql";
    private const string TwitchWebClientId =
        "kimne78kx3ncx6brgo4mv6wki5h1ko";
    private const string PersistedQueryHash =
        "4f35f1ac933d76b1da008c806cd5546a7534dfaff83e033a422a81f24e5991b3";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<string?> ResolveAsync(
        string clipId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clipId))
        {
            return null;
        }

        var payload = new[]
        {
            new
            {
                operationName = "VideoAccessToken_Clip",
                variables = new
                {
                    platform = "web",
                    slug = clipId
                },
                extensions = new
                {
                    persistedQuery = new
                    {
                        version = 1,
                        sha256Hash = PersistedQueryHash
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            GraphQlUrl);
        request.Headers.Add("Client-ID", TwitchWebClientId);
        request.Content = JsonContent.Create(payload);

        using var response = await Http.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        var firstResult = document.RootElement[0];

        if (!firstResult.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("clip", out var clip) ||
            clip.ValueKind != JsonValueKind.Object ||
            !clip.TryGetProperty("playbackAccessToken", out var accessToken))
        {
            return null;
        }

        var token = accessToken.GetProperty("value").GetString();
        var signature = accessToken.GetProperty("signature").GetString();

        if (string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(signature) ||
            !clip.TryGetProperty("videoQualities", out var qualities))
        {
            return null;
        }

        string? firstSource = null;
        string? preferredSource = null;

        foreach (var quality in qualities.EnumerateArray())
        {
            var sourceUrl = quality.GetProperty("sourceURL").GetString();
            var qualityName = quality.GetProperty("quality").GetString();

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                continue;
            }

            firstSource ??= sourceUrl;

            if (qualityName == "720")
            {
                preferredSource = sourceUrl;
                break;
            }
        }

        var source = preferredSource ?? firstSource;

        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var separator = source.Contains('?') ? "&" : "?";

        return source + separator +
               "token=" + Uri.EscapeDataString(token) +
               "&sig=" + Uri.EscapeDataString(signature);
    }
}
