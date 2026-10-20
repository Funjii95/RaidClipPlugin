using System.Net;
using System.Net.Http.Json;
using SnapshotProtocol;
namespace Backend.Tests;
public sealed class SelfDeletionTests
{
    [Fact]public async Task Offline_request_is_identity_bound_idempotent_and_only_plugin_can_complete(){
        await using var f=new ActionApiFixture(privacy:true,selfDeletion:true);
        var intent=new SelfDeletionIntent(Guid.NewGuid(),"DELETE_MY_RPG_ACCOUNT");
        var response=await f.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent);
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
        var job=(await response.Content.ReadFromJsonAsync<SelfDeletionJob>())!;
        Assert.Equal("1001",job.UserId);Assert.False(job.Completed);
        var repeat=(await (await f.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent with{RequestId=Guid.NewGuid()})).Content.ReadFromJsonAsync<SelfDeletionJob>())!;
        Assert.Equal(job,repeat);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Viewer.GetAsync("/api/v1/plugin/actions/privacy-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/privacy-requests/1001/complete",intent)).StatusCode);
        Assert.Single((await f.Plugin.GetFromJsonAsync<SelfDeletionJob[]>("/api/v1/plugin/actions/privacy-requests"))!);
        await f.ReadyAsync();Assert.Equal(HttpStatusCode.Forbidden,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
        f.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token("1002"));
        var own=await f.Viewer.GetStringAsync("/api/v1/play/privacy/deletion");Assert.Contains("\"request\":null",own);
        Assert.Equal(HttpStatusCode.Accepted,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
        (await f.Plugin.PostAsync("/api/v1/plugin/actions/privacy/1001",null)).EnsureSuccessStatusCode();
        var done=await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/privacy-requests/1001/complete",intent);done.EnsureSuccessStatusCode();
        Assert.True((await done.Content.ReadFromJsonAsync<SelfDeletionJob>())!.Completed);
        Assert.Empty((await f.Plugin.GetFromJsonAsync<SelfDeletionJob[]>("/api/v1/plugin/actions/privacy-requests"))!);
    }
    [Fact]public async Task Feature_flag_confirmation_jwt_and_untrusted_identity_fail_closed(){
        await using var off=new ActionApiFixture(privacy:true);
        var intent=new SelfDeletionIntent(Guid.NewGuid(),"DELETE_MY_RPG_ACCOUNT");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await off.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent)).StatusCode);
        await using var f=new ActionApiFixture(privacy:true,selfDeletion:true);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent with{Confirmation=""})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",new{intent.RequestId,intent.Confirmation,userId="1002"})).StatusCode);
        f.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token(channel:"99"));
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent)).StatusCode);
        f.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token("9999"));
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Viewer.PostAsJsonAsync("/api/v1/play/privacy/deletion",intent)).StatusCode);
    }
}
