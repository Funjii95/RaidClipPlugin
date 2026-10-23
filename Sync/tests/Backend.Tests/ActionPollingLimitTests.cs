using System.Net;
using System.Net.Http.Json;

namespace Backend.Tests;

public sealed class ActionPollingLimitTests
{
    [Fact] public async Task StatusAndReceiptPollingDoNotExhaustTheMutationBudget()
    {
        await using var f=new ActionApiFixture();await f.ReadyAsync();
        for(var i=0;i<130;i++)Assert.Equal(HttpStatusCode.OK,(await f.Viewer.GetAsync("/api/v1/play/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent())).StatusCode);
    }
    [Fact] public async Task SubmissionBudgetRemainsBoundedIndependentlyOfReads()
    {
        await using var f=new ActionApiFixture();await f.ReadyAsync();
        var intent=ActionApiFixture.Intent();
        for(var i=0;i<120;i++)Assert.Equal(HttpStatusCode.Accepted,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Viewer.GetAsync("/api/v1/play/state")).StatusCode);
    }
}
