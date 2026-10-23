
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Backend;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var settings = new BackendSettings();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
builder.Services.AddSingleton(settings);
builder.Services.AddHttpClient("TwitchDesktop", client => { client.Timeout = TimeSpan.FromSeconds(8); client.MaxResponseContentBufferSize = 16384; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<ITwitchDesktopVerifier,TwitchDesktopVerifier>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRelayStore>(services=>{
    if(settings.Actions.Mode=="Synthetic" && builder.Environment.IsEnvironment("Testing"))return new MemoryRelayStore();
    if(settings.Actions.Mode!="Firestore")throw new InvalidOperationException("Durable action store required outside tests.");
    var db=new Google.Cloud.Firestore.FirestoreDbBuilder {ProjectId=settings.Data.ProjectId,DatabaseId=settings.Data.DatabaseId,EmulatorDetection=Google.Api.Gax.EmulatorDetection.ProductionOnly}.Build();
    return new FirestoreRelayStore(db,settings.Twitch.ChannelId,settings.Actions.Collection);
});
builder.Services.AddSingleton<ActionRelay>();
builder.Services.AddSingleton<IPlayerReader>(_ => settings.Data.Mode == "Mock"
    ? new MockPlayerReader() : new FirestorePlayerReader(settings.Data,settings.Twitch.ChannelId));
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
        RequireExpirationTime = true, ClockSkew = TimeSpan.Zero
    };
    o.Events = new JwtBearerEvents { OnTokenValidated = context =>
    {
        var p = context.Principal!;
        if (p.FindFirstValue("channel_id") != settings.Twitch.ChannelId ||
            p.FindFirstValue("role") is not ("viewer" or "moderator" or "broadcaster"))
            context.Fail("Invalid Extension context.");
        return Task.CompletedTask;
    }};
}).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, PluginAuthentication>("Plugin", _ => { })
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ActionPluginAuthentication>("ActionPlugin", _=>{})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DesktopAuthentication>("Desktop", _=>{});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Viewer", p => p.AddAuthenticationSchemes("Bearer","Desktop").RequireAuthenticatedUser()
        .RequireAssertion(c => Ids.IsNumeric(c.User.FindFirstValue("user_id")) &&
            c.User.FindFirstValue("opaque_user_id")?.StartsWith('U') == true &&
            c.User.FindFirstValue("is_unlinked") != "true"));
    o.AddPolicy("ActionPlugin",p=>p.AddAuthenticationSchemes("ActionPlugin").RequireAuthenticatedUser());
    o.AddPolicy("Plugin", p => p.AddAuthenticationSchemes("Plugin").RequireAuthenticatedUser());
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (settings.Twitch.AllowedOrigins.Length > 0)
        p.WithOrigins(settings.Twitch.AllowedOrigins).WithMethods("GET","POST").WithHeaders("Authorization","Content-Type","X-Rpg-Session");
}));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("actions",c=>RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirstValue("user_id")??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddFixedWindowLimiter("plugin-actions",o=>{o.PermitLimit=480;o.Window=TimeSpan.FromMinutes(1);o.QueueLimit=0;});
    o.AddPolicy("reads", c => RateLimitPartition.GetFixedWindowLimiter(
        c.User.FindFirstValue("user_id") ?? c.User.Identity?.Name ?? c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
builder.Configuration.Bind(settings);
settings.Validate();
if(settings.Actions.Enabled && settings.Actions.Mode=="Synthetic" && !app.Environment.IsEnvironment("Testing"))throw new InvalidOperationException("Synthetic relay storage is test-host only.");
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
app.MapGet("/api/v1/desktop/config", () => Results.Ok(new {
    enabled=settings.Desktop.Enabled, clientId=settings.Desktop.Enabled?settings.Desktop.ClientId:null,
    channelId=settings.Twitch.ChannelId, protocolVersion=1
})).RequireRateLimiting("reads");
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
app.MapActionEndpoints(settings);
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
