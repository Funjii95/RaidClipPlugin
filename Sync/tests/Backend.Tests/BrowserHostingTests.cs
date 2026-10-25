using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
namespace Backend.Tests;
public class BrowserHostingTests
{
 [Fact]
 public async Task Browser_entry_has_one_unambiguous_route_and_apis_remain_protected()
 {
  await using var factory=new ApiFactory();
  using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
  var redirect=await client.GetAsync("/browser");Assert.Equal(HttpStatusCode.Redirect,redirect.StatusCode);Assert.Equal("/browser/",redirect.Headers.Location?.ToString());
  var page=await client.GetAsync("/browser/");Assert.Contains(page.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.NotFound});
  Assert.Contains("DENY",page.Headers.GetValues("X-Frame-Options"));
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/me")).StatusCode);
 }
}
