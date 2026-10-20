using System.Net;
using System.Net.Http.Json;
using SnapshotProtocol;
using Xunit;

namespace Backend.Tests;
public sealed class PrivacyActionTests
{
    [Fact]public async Task Erasure_needs_plugin_credential_explicit_feature_flag_and_allowed_identity(){
        await using var off=new ActionApiFixture();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await off.Plugin.PostAsync("/api/v1/plugin/actions/privacy/1001",null)).StatusCode);
        await using var on=new ActionApiFixture(privacy:true);
        Assert.Equal(HttpStatusCode.Unauthorized,(await on.Viewer.PostAsync("/api/v1/plugin/actions/privacy/1001",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await on.Plugin.PostAsync("/api/v1/plugin/actions/privacy/9999",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await on.Plugin.PostAsync("/api/v1/plugin/actions/privacy/not-a-user",null)).StatusCode);
    }
    [Fact]public async Task Erasure_removes_private_results_blocks_old_and_new_actions_and_is_idempotent(){
        await using var f=new ActionApiFixture(privacy:true);var session=await f.ReadyAsync();var intent=ActionApiFixture.Intent();
        (await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).EnsureSuccessStatusCode();
        (await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).EnsureSuccessStatusCode();
        var result=new RelayComplete(session,intent.ActionId,new(true,"applied","private display name","{\"privateProfile\":true}"));
        (await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",result)).EnsureSuccessStatusCode();
        for(var i=0;i<2;i++)(await f.Plugin.PostAsync("/api/v1/plugin/actions/privacy/1001",null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound,(await f.Viewer.GetAsync("/api/v1/play/actions/"+intent.ActionId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",result)).StatusCode);
        await f.ReadyAsync();Assert.Equal(HttpStatusCode.Forbidden,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
        f.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token("1002"));
        Assert.Equal(HttpStatusCode.Accepted,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
    }
}
