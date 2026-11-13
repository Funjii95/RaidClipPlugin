using System.Net;
using System.Net.Http.Headers;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public class TwitchRenewingHandlerTests{
 sealed class Transport:HttpMessageHandler{
  internal int Calls;internal readonly List<string> Bodies=[];
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){
   Interlocked.Increment(ref Calls);if(r.Content is not null)lock(Bodies)Bodies.Add(r.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult());
   await Task.Yield();return new(r.Headers.Authorization?.Parameter=="fresh"?HttpStatusCode.OK:HttpStatusCode.Unauthorized);
  }
 }
 [Fact]public async Task Concurrent_unauthorized_modules_renew_once_and_keep_request_body(){
  var old=Guid.NewGuid().ToString();var renewals=0;
  TwitchRenewingHandler.Register(old,async ct=>{Interlocked.Increment(ref renewals);await Task.Delay(20,ct);return "fresh";});
  var transport=new Transport();using var client=new HttpClient(new TwitchRenewingHandler(transport));client.DefaultRequestHeaders.Authorization=new("Bearer",old);
  var results=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>client.PostAsync("https://api.twitch.tv/helix/chat/messages",new StringContent("synthetic message"))));
  Assert.All(results,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));Assert.Equal(1,renewals);Assert.All(transport.Bodies,b=>Assert.Equal("synthetic message",b));
 }
 [Fact]public async Task Invalid_refresh_does_not_retry_chat_or_open_login(){
  var old=Guid.NewGuid().ToString();TwitchRenewingHandler.Register(old,_=>Task.FromResult<string?>(null));var transport=new Transport();using var client=new HttpClient(new TwitchRenewingHandler(transport));client.DefaultRequestHeaders.Authorization=new("Bearer",old);
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("https://api.twitch.tv/helix/users")).StatusCode);Assert.Equal(1,transport.Calls);
 }
}
