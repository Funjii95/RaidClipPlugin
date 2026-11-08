using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
namespace RaidClipPlugin.Services;
// All modules sharing an OAuth token share one renewal; only explicit 401s retry.
internal sealed class TwitchRenewingHandler(HttpMessageHandler? inner=null):DelegatingHandler(inner??new HttpClientHandler())
{
 private sealed class Session(string token,Func<CancellationToken,Task<string?>> renew){
  internal string Token=token;internal readonly Func<CancellationToken,Task<string?>> Renew=renew;internal readonly SemaphoreSlim Gate=new(1,1);
 }
 private static readonly ConcurrentDictionary<string,Session> Sessions=new();
 internal static void Register(string token,Func<CancellationToken,Task<string?>> renew)=>Sessions.TryAdd(token,new(token,renew));
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){
  var original=request.Headers.Authorization?.Parameter;
  if(request.RequestUri?.Host!="api.twitch.tv"||original is null||!Sessions.TryGetValue(original,out var session))return await base.SendAsync(request,ct);
  var used=session.Token;request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",used);
  var body=request.Content is null?null:await request.Content.ReadAsByteArrayAsync(ct);
  var response=await base.SendAsync(request,ct);
  if(response.StatusCode!=HttpStatusCode.Unauthorized)return response;
  await session.Gate.WaitAsync(ct);
  try{if(session.Token==used){var token=await session.Renew(ct);if(string.IsNullOrEmpty(token))return response;session.Token=token;Sessions[token]=session;}}
  catch{response.Dispose();throw;}
  finally{session.Gate.Release();}
  response.Dispose();
  using var retry=new HttpRequestMessage(request.Method,request.RequestUri){Version=request.Version};
  foreach(var header in request.Headers)retry.Headers.TryAddWithoutValidation(header.Key,header.Value);
  retry.Headers.Authorization=new AuthenticationHeaderValue("Bearer",session.Token);
  if(body is not null){retry.Content=new ByteArrayContent(body);foreach(var header in request.Content!.Headers)retry.Content.Headers.TryAddWithoutValidation(header.Key,header.Value);}
  return await base.SendAsync(retry,ct);
 }
}
