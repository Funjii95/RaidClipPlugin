using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class ReleaseEndpointTests
{
    [Theory]
    [InlineData("https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app",true)]
    [InlineData("https://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app",true)]
    [InlineData("http://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app",false)]
    [InlineData("https://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app.attacker.invalid",false)]
    public void Action_endpoint_remains_exactly_allowlisted(string endpoint,bool allowed)
    {
        var options=new ActionChannelOptions{BackendUrl=endpoint,ChannelId="42812664",AllowedUserIds=["1001"],ProtectedKeyFile=Path.Combine(Path.GetTempPath(),"synthetic.dpapi")};
        if(allowed)options.Validate();else Assert.Throws<InvalidDataException>(options.Validate);
        options.ChannelId="42";Assert.Throws<InvalidDataException>(options.Validate);
    }
    [Theory]
    [InlineData("https://funjii-otter-rpg-publisher-test-134126914046.europe-west3.run.app/api/v1/snapshots",true)]
    [InlineData("https://rc1---funjii-otter-rpg-publisher-test-cwzl75ajla-ey.a.run.app/api/v1/snapshots",true)]
    [InlineData("https://rc1---funjii-otter-rpg-publisher-test-cwzl75ajla-ey.a.run.app/api/v1/snapshots?target=other",false)]
    [InlineData("https://publisher.attacker.invalid/api/v1/snapshots",false)]
    public void Snapshot_endpoint_remains_exactly_allowlisted(string endpoint,bool allowed)
    {
        var options=new CloudSyncOptions{Endpoint=endpoint,ChannelId="42812664",AllowedUserIds=["1001"],ProtectedKeyFile=Path.Combine(Path.GetTempPath(),"synthetic.dpapi")};
        if(allowed)options.Validate();else Assert.Throws<InvalidDataException>(options.Validate);
        options.ChannelId="42";Assert.Throws<InvalidDataException>(options.Validate);
    }
}
