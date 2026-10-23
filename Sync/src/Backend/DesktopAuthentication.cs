using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Backend;

public sealed record DesktopIdentity(string UserId,string Login);
public interface ITwitchDesktopVerifier
{
    Task<DesktopIdentity?> VerifyAsync(string token,CancellationToken ct);
}
public sealed class TwitchDesktopVerifier(IHttpClientFactory clients,BackendSettings settings) : ITwitchDesktopVerifier,IDisposable
{
    private readonly MemoryCache cache=new(new MemoryCacheOptions {SizeLimit=1024});
    public async Task<DesktopIdentity?> VerifyAsync(string token,CancellationToken ct)
    {
        if(!settings.Desktop.Enabled)return null;
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        if(cache.TryGetValue(key,out DesktopIdentity? cached))return cached;
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://id.twitch.tv/oauth2/validate");
        request.Headers.Authorization=new AuthenticationHeaderValue("OAuth",token);
        using var response=await clients.CreateClient("TwitchDesktop").SendAsync(request,ct);
        if(response.StatusCode==HttpStatusCode.Unauthorized)return null;
        response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var root=json.RootElement;
        if(root.GetProperty("client_id").GetString()!=settings.Desktop.ClientId ||
            !Ids.IsNumeric(root.GetProperty("user_id").GetString()) || root.GetProperty("expires_in").GetInt32()<=0)return null;
        var identity=new DesktopIdentity(root.GetProperty("user_id").GetString()!,root.GetProperty("login").GetString()??"");
        cache.Set(key,identity,new MemoryCacheEntryOptions {Size=1,AbsoluteExpirationRelativeToNow=TimeSpan.FromSeconds(Math.Min(60,root.GetProperty("expires_in").GetInt32()))});
        return identity;
    }
    public void Dispose()=>cache.Dispose();
}
public sealed class DesktopAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,
    BackendSettings settings,ITwitchDesktopVerifier verifier) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers=Request.Headers.Authorization;
        if(!settings.Desktop.Enabled || headers.Count!=1 || headers[0] is not {} header || !header.StartsWith("Desktop ",StringComparison.Ordinal))return AuthenticateResult.NoResult();
        string token=header[8..];
        if(token.Length is <10 or >2048 || token.Any(c=>!char.IsAsciiLetterOrDigit(c)))return AuthenticateResult.Fail("Invalid desktop credential.");
        try
        {
            var identity=await verifier.VerifyAsync(token,Context.RequestAborted);
            if(identity==null || !Ids.IsNumeric(identity.UserId))return AuthenticateResult.Fail("Desktop identity not verified.");
            // Same Viewer policy, profile reader and action relay; channel is server-owned.
            var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("user_id",identity.UserId),new Claim("channel_id",settings.Twitch.ChannelId),
                new Claim("opaque_user_id","U"+identity.UserId),new Claim("role","viewer"),new Claim("is_unlinked","false")},Scheme.Name));
            return AuthenticateResult.Success(new AuthenticationTicket(principal,Scheme.Name));
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return AuthenticateResult.Fail("Twitch verification unavailable."); }
    }
}
