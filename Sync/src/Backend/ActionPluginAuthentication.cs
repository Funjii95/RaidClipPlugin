using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace Backend;
public sealed class ActionPluginAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,BackendSettings settings)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers=Request.Headers.Authorization;
        if(!settings.Actions.Enabled || headers.Count!=1 || headers[0] is not {} header || header.Length>512 || !header.StartsWith("Bearer ",StringComparison.Ordinal))return Task.FromResult(AuthenticateResult.NoResult());
        try {
            var key=Convert.FromBase64String(header[7..]);
            try {
                if(key.Length is <32 or >64 || header[7..]!=Convert.ToBase64String(key) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(key),Convert.FromHexString(settings.Actions.PluginKeyHash)))
                    return Task.FromResult(AuthenticateResult.Fail("Invalid action credential."));
            } finally {CryptographicOperations.ZeroMemory(key);}
        } catch(FormatException){return Task.FromResult(AuthenticateResult.Fail("Invalid action credential."));}
        var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name,"action-plugin")],Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user,Scheme.Name)));
    }
}
