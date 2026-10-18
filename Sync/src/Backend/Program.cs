
using System.Security.Claims;
using System.Threading.RateLimiting;
using Backend;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var settings = new BackendSettings();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
builder.Services.AddSingleton(settings);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IPlayerReader>(_ => settings.Data.Mode == "Mock"
    ? new MockPlayerReader() : new FirestorePlayerReader(settings.Data));
builder.Services.AddAuthentication("Bearer").AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.IncludeErrorDetails = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, ValidateAudience = false, // Twitch Extension tokens have neither claim.
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.Twitch.SecretBase64)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateLifetime = true,
        RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(15)
    };
    o.Events = new JwtBearerEvents { OnTokenValidated = context =>
    {
        var p = context.Principal!;
        if (p.FindFirstValue("channel_id") != settings.Twitch.ChannelId ||
            p.FindFirstValue("role") is not ("viewer" or "moderator" or "broadcaster"))
            context.Fail("Invalid Extension context.");
        return Task.CompletedTask;
    }};
}).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, PluginAuthentication>("Plugin", _ => { });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Viewer", p => p.AddAuthenticationSchemes("Bearer").RequireAuthenticatedUser()
        .RequireAssertion(c => Ids.IsNumeric(c.User.FindFirstValue("user_id")) &&
            c.User.FindFirstValue("opaque_user_id")?.StartsWith('U') == true &&
            c.User.FindFirstValue("is_unlinked") != "true"));
    o.AddPolicy("Plugin", p => p.AddAuthenticationSchemes("Plugin").RequireAuthenticatedUser());
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (settings.Twitch.AllowedOrigins.Length > 0)
        p.WithOrigins(settings.Twitch.AllowedOrigins).WithMethods("GET").WithHeaders("Authorization");
}));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("reads", c => RateLimitPartition.GetFixedWindowLimiter(
        c.User.FindFirstValue("user_id") ?? c.User.Identity?.Name ?? c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
builder.Configuration.Bind(settings);
settings.Validate();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (IPlayerReader reader, CancellationToken ct) =>
{
    try { await reader.CheckAsync(ct); return Results.Ok(new { status = "ready", mode = settings.Data.Mode }); }
    catch (Exception) when (!ct.IsCancellationRequested) { return Results.StatusCode(503); }
});
app.MapGet("/api/v1/me", async (ClaimsPrincipal user, IPlayerReader reader, CancellationToken ct) =>
    await Read(user.FindFirstValue("user_id")!, reader, ct)).RequireAuthorization("Viewer").RequireRateLimiting("reads");
app.MapGet("/api/v1/plugin/players/{userId}", async (string userId, IPlayerReader reader, CancellationToken ct) =>
    Ids.IsNumeric(userId) ? await Read(userId, reader, ct) : Results.BadRequest())
    .RequireAuthorization("Plugin").RequireRateLimiting("reads");
app.Run();

static async Task<IResult> Read(string userId, IPlayerReader reader, CancellationToken ct)
{
    try
    {
        var player = await reader.GetAsync(userId, ct);
        return player is null ? Results.NotFound() : Results.Ok(player);
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    { return Results.Problem(statusCode: 503, title: "Player source unavailable or incompatible."); }
}
public partial class Program;
