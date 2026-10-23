using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SnapshotProtocol;

namespace SnapshotPublisher;

public partial class PublisherProgram
{
    public static void Main(string[] args)
    {
        // Local prototype or explicitly restricted synthetic Cloud Run test.
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://127.0.0.1:8181");
        builder.Services.AddSingleton<ISnapshotSink>(services =>
        {
            var settings = new PublisherSettings();
            services.GetRequiredService<IConfiguration>().GetSection("Publisher").Bind(settings);
            settings.Validate();
            return settings.CreateSink();
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            // Authenticated revision checks must not consume the write budget shared by players.
            options.AddFixedWindowLimiter("snapshot-heads", limiter =>
            {
                limiter.PermitLimit = 600;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
            options.AddFixedWindowLimiter("snapshots", limiter =>
            {
                limiter.PermitLimit = 60;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });
        var app = builder.Build();
        var settings = new PublisherSettings();
        app.Configuration.GetSection("Publisher").Bind(settings);
        settings.Validate();
        var channel = settings.ChannelId;
        var keyHash = Convert.FromHexString(settings.ApiKeyHash);
        if (settings.Mode is "FirestoreTest" or "FirestoreProfilesTest")
        {
            var portText = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Invalid PORT.");
            app.Urls.Clear();
            app.Urls.Add($"http://0.0.0.0:{port}");
        }
        var store = app.Services.GetRequiredService<ISnapshotSink>();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (context.Request.Path != "/health/live")
            {
                var header = context.Request.Headers.Authorization;
                if (header.Count != 1 || !ValidCredential(header[0], keyHash))
                {
                    context.Response.StatusCode = 401;
                    return; // Do not read, parse or log an unauthenticated body.
                }
            }
            await next();
        });
        app.UseRateLimiter();
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/v1/snapshots/capabilities", () => Results.Json(new { rpgProjectionVersion = 2, supportedRpgProjectionVersions = new[] { 2, 3, 4, 5, 6, SnapshotValidation.RpgProjectionVersion }, channelId = channel, automaticPlayerEnrollment = settings.AutomaticPlayerEnrollment, allowedUserIds = settings.AllowedUserIds, maxMessageBytes = SnapshotValidation.MaxMessageBytes }));
        app.MapGet("/api/v1/snapshots/{userId}/head", async (string userId, CancellationToken ct) =>
        {
            if (settings.Mode is not ("FirestoreProfilesTest" or "FirestoreTest")) return Results.NotFound();
            if (!settings.AutomaticPlayerEnrollment && !settings.AllowedUserIds.Contains(userId)) return Results.StatusCode(403);
            if (store is not SnapshotFirestore.FirestoreSnapshotSink reader) return Results.StatusCode(503);
            try
            {
                var current = await reader.ReadCurrentAsync(userId, ct);
                return current is null ? Results.NotFound() : Results.Json(new SnapshotHead(1, current.ChannelId,
                    current.UserId, current.Revision, current.SnapshotId, current.CapturedAt, current.Deleted, current.ContentHash));
            }
            catch (Exception) when (!ct.IsCancellationRequested) { return Results.StatusCode(503); }
        }).RequireRateLimiting("snapshot-heads");
        app.MapPost("/api/v1/snapshots", async (HttpRequest request, CancellationToken ct) =>
        {
            if (settings.Mode is "FirestoreTest" or "FirestoreProfilesTest" && !settings.WritesEnabled) return Results.StatusCode(503);
            if (request.ContentLength > SnapshotValidation.MaxMessageBytes) return Results.StatusCode(413);
            if (!request.HasJsonContentType()) return Results.StatusCode(415);
            try
            {
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                while (true)
                {
                    var count = await request.Body.ReadAsync(chunk.AsMemory(0,
                        Math.Min(chunk.Length, SnapshotValidation.MaxMessageBytes + 1 - (int)buffer.Length)), ct);
                    if (count == 0) break;
                    buffer.Write(chunk, 0, count);
                    if (buffer.Length > SnapshotValidation.MaxMessageBytes) return Results.StatusCode(413);
                }
                var declared = JsonSerializer.Deserialize<SnapshotMessage>(buffer.ToArray(), new JsonSerializerOptions
                { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException();
                buffer.Position = 0;
                // Validate fully (including duplicate keys) before deciding channel access.
                var validated = await SnapshotValidation.ReadAsync(buffer, declared.ChannelId, ct);
                if (validated.ChannelId != channel) return Results.StatusCode(403);
                if (!settings.AutomaticPlayerEnrollment && settings.AllowedUserIds.Length > 0 && !settings.AllowedUserIds.Contains(validated.UserId))
                    return Results.StatusCode(403);
                if(settings.Mode=="FirestoreProfilesTest" && validated.Kind==SnapshotKind.Delete && !settings.PrivacyDeletionEnabled)return Results.StatusCode(503);
                var clean = declared with { AccountJson = validated.AccountJson };
                SnapshotDecision decision;
                try { decision = await store.ApplyAsync(clean, ct); }
                catch (Exception) when (!ct.IsCancellationRequested)
                { return Results.StatusCode(503); }
                var status = decision is SnapshotDecision.Conflict or SnapshotDecision.RestoreRequired ? 409 : 200;
                return Results.Json(new SnapshotAcknowledgement(1, channel, validated.UserId,
                    validated.Revision, validated.SnapshotId, decision.ToString()), statusCode: status);
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
            { return Results.BadRequest(); }
            catch (IOException) { return Results.StatusCode(503); }
            catch (UnauthorizedAccessException) { return Results.StatusCode(503); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Results.StatusCode(503); }
        }).RequireRateLimiting("snapshots");
        app.Run();

        static bool ValidCredential(string? header, byte[] expectedHash)
        {
            if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length > 512)
                return false;
            try
            {
                var key = Convert.FromBase64String(header[7..]);
                return key.Length is >= 32 and <= 64 &&
                    CryptographicOperations.FixedTimeEquals(SHA256.HashData(key), expectedHash);
            }
            catch (FormatException) { return false; }
        }

    }
}
