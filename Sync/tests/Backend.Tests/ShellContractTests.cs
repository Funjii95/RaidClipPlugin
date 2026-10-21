using System.Net.Http.Json;
using System.Text.Json;
using Backend;
using SnapshotProtocol;
namespace Backend.Tests;
public sealed class ShellContractTests
{
    [Fact]
    public async Task Shell_action_is_denied_even_with_valid_identity_and_ready_plugin()
    {
        await using var f=new ActionApiFixture();var session=await f.ReadyAsync();
        var response=await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",ActionApiFixture.Intent("openShells","{\"count\":\"10\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden,response.StatusCode);
        Assert.Contains("chat_only",await response.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.NoContent,(await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).StatusCode);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("10")]
    [InlineData("100")]
    public void Canonical_shell_count_is_bound_to_the_action(string count)
    {
        var intent=new RpgIntent(Guid.NewGuid(),"openShells",JsonSerializer.SerializeToElement(new{count}));
        Assert.Contains(count,RpgIntents.Canonical(intent));
        var changed=intent with{Arguments=JsonSerializer.SerializeToElement(new{count=count=="1"?"10":"1"})};
        Assert.NotEqual(RpgIntents.Hash(intent),RpgIntents.Hash(changed));
    }
    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("01")]
    [InlineData("1.0")]
    [InlineData("100000000000")]
    public void Invalid_shell_count_never_enters_the_queue(string count)
        =>Assert.Throws<ArgumentException>(()=>RpgIntents.Hash(new(Guid.NewGuid(),"openShells",JsonSerializer.SerializeToElement(new{count}))));
    [Fact]
    public async Task Materials_roundtrip_through_projection_firestore_and_backend_without_private_tokens()
    {
        await using var fixture=await TransactionalFirestoreFixture.StartAsync();
        var node=System.Text.Json.Nodes.JsonNode.Parse(PublisherTests.Account)!;node["Rpg"]!["Materials"]=JsonSerializer.SerializeToNode(new{Shells=100,WhitePearls=3,BluePearls=2,RedPearls=1,StoneChips=47});var account=node.ToJsonString();
        var message=PublisherTests.Message() with{AccountJson=account};
        Assert.Equal(SnapshotDecision.Applied,await fixture.Sink.ApplyAsync(message));
        var view=await fixture.Reader.GetAsync("1001",default);
        Assert.Equal(new MaterialView(100,3,2,1,47),view!.Rpg.Materials);
    }
    [Fact]
    public void Negative_materials_are_rejected_and_legacy_profiles_remain_readable()
    {
        var old=SnapshotParser.Parse(PublisherTests.Account,"1001");Assert.Null(old.Rpg.Materials);
        var node=System.Text.Json.Nodes.JsonNode.Parse(PublisherTests.Account)!;node["Rpg"]!["Materials"]=JsonSerializer.SerializeToNode(new{Shells=-1,WhitePearls=0,BluePearls=0,RedPearls=0,StoneChips=0});var corrupt=node.ToJsonString();
        Assert.Throws<InvalidDataException>(()=>SnapshotValidation.Validate(PublisherTests.Message() with{AccountJson=corrupt},"42"));
        Assert.Throws<InvalidDataException>(()=>SnapshotParser.Parse(corrupt,"1001"));
    }
}
