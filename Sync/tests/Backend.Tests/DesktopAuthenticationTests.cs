using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SnapshotProtocol;

namespace Backend.Tests;
public sealed class FakeDesktopVerifier : ITwitchDesktopVerifier
{
 public Task<DesktopIdentity?> VerifyAsync(string token,CancellationToken ct)=>Task.FromResult<DesktopIdentity?>(token=="validdesktopcredential"?new("1001","Otter"):null);
}
public sealed class DesktopAuthenticationTests
{
 [Fact]public async Task Desktop_reads_own_profile_and_cannot_use_plugin_routes()
 {
  await using var original=new ApiFactory();
  await using var factory=original.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Desktop:Enabled"]="true",["Desktop:ClientId"]="publicclient12345"}))
   .ConfigureServices(s=>{s.RemoveAll<ITwitchDesktopVerifier>();s.AddSingleton<ITwitchDesktopVerifier,FakeDesktopVerifier>();}));
  using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new("Desktop","validdesktopcredential");
  var response=await client.GetAsync("/api/v1/me?userId=9999");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("\"userId\":\"1001\"",await response.Content.ReadAsStringAsync());
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/plugin/players/1001")).StatusCode);
  client.DefaultRequestHeaders.Authorization=new("Desktop","wrongdesktopcredential");Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/me")).StatusCode);
  client.DefaultRequestHeaders.Authorization=new("Bearer","validdesktopcredential");Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/me")).StatusCode);
 }
 [Fact]public async Task Desktop_uses_existing_action_queue_and_deduplication()
 {
  var store=new MemoryRelayStore();await using var f=new ActionApiFixture(store);var session=await f.ReadyAsync();
  await using var factory=f.Factory.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Desktop:Enabled"]="true",["Desktop:ClientId"]="publicclient12345"}))
   .ConfigureServices(s=>{s.RemoveAll<ITwitchDesktopVerifier>();s.AddSingleton<ITwitchDesktopVerifier,FakeDesktopVerifier>();}));
  using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new("Desktop","validdesktopcredential");client.DefaultRequestHeaders.Add("X-Rpg-Session",session.ToString());
  var intent=ActionApiFixture.Intent();Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
  Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
  var claim=await (await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).Content.ReadFromJsonAsync<ActionEnvelope>();
  Assert.Equal("1001",claim!.UserId);Assert.Equal(intent.ActionId,claim.Intent.ActionId);
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/api/v1/plugin/actions/session",null)).StatusCode);
 }
 [Fact]public async Task Disabled_desktop_auth_is_rejected_and_config_exposes_no_secrets()
 {
  await using var factory=new ApiFactory();using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new("Desktop","validdesktopcredential");
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/me")).StatusCode);
  string config=await client.GetStringAsync("/api/v1/desktop/config");Assert.Contains("\"enabled\":false",config);Assert.DoesNotContain(ApiFactory.Secret,config);Assert.DoesNotContain(ApiFactory.PluginKey,config);
 }
 [Theory][InlineData("wrongclient","1001",3600,false)][InlineData("publicclient12345","",3600,false)][InlineData("publicclient12345","1001",0,false)][InlineData("publicclient12345","1001",3600,true)]
 public async Task Real_verifier_requires_registered_app_user_and_unexpired_token(string clientId,string userId,int expires,bool allowed)
 {
  var settings=new BackendSettings{Desktop=new(){Enabled=true,ClientId="publicclient12345"}};
  using var handler=new ValidateHandler(JsonSerializer.Serialize(new{client_id=clientId,user_id=userId,login="Otter",expires_in=expires}));
  using var http=new HttpClient(handler);using var verifier=new TwitchDesktopVerifier(new ClientFactory(http),settings);
  var result=await verifier.VerifyAsync("credential",default);Assert.Equal(allowed,result!=null);
  if(allowed){await verifier.VerifyAsync("credential",default);Assert.Equal(1,handler.Calls);}
 }
 private sealed class ClientFactory(HttpClient http):IHttpClientFactory{public HttpClient CreateClient(string name)=>http;}
 private sealed class ValidateHandler(string body):HttpMessageHandler
 {
  public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;Assert.Equal("https://id.twitch.tv/oauth2/validate",request.RequestUri!.AbsoluteUri);Assert.Equal("OAuth",request.Headers.Authorization!.Scheme);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});}
 }
}
