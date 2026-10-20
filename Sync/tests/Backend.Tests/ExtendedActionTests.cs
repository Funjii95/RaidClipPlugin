using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SnapshotProtocol;

namespace Backend.Tests;

public sealed class ExtendedActionTests
{
    public static TheoryData<string,string> Operations=>new(){
        {"bossStatus","{}"},{"bossAttack","{}"},
        {"favorite","{\"instanceId\":\"owned-one\",\"enabled\":\"true\"}"},
        {"itemlock","{\"instanceId\":\"owned-one\",\"enabled\":\"false\"}"},
        {"salvagePreview","{\"instanceId\":\"owned-one\"}"},
        {"salvageBatch","{\"selection\":\"common\"}"},
        {"salvageConfirm","{\"token\":\"synthetic-quote\"}"},
        {"salvageCancel","{\"token\":\"synthetic-quote\"}"},
        {"community","{\"game\":\"heist\",\"operation\":\"join\",\"eventId\":\"synthetic-round\",\"stake\":\"0\",\"target\":\"-\"}"},
        {"community","{\"game\":\"race\",\"operation\":\"start\",\"eventId\":\"-\",\"stake\":\"100\",\"target\":\"-\"}"},
        {"community","{\"game\":\"duel\",\"operation\":\"challenge\",\"eventId\":\"-\",\"stake\":\"100\",\"target\":\"synthetic\"}"}
    };
    [Theory][MemberData(nameof(Operations))]
    public async Task New_operations_use_existing_authenticated_identity_and_idempotent_queue(string kind,string args)
    {
        await using var api=new ActionApiFixture();var session=await api.ReadyAsync();
        var intent=new RpgIntent(Guid.NewGuid(),kind,JsonSerializer.Deserialize<JsonElement>(args));
        Assert.Equal(HttpStatusCode.Accepted,(await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,(await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
        var envelope=await (await api.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).Content.ReadFromJsonAsync<ActionEnvelope>();
        Assert.Equal("1001",envelope!.UserId);Assert.Equal(intent.ActionId,envelope.Intent.ActionId);Assert.Equal(RpgIntents.Hash(intent),envelope.IntentHash);
        (await api.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",new RelayComplete(session,intent.ActionId,new(true,"applied","synthetic")))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent,(await api.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).StatusCode);
        api.Viewer.DefaultRequestHeaders.Authorization=new("Bearer",ActionApiFixture.Token(user:"1002"));
        Assert.Equal(HttpStatusCode.NotFound,(await api.Viewer.GetAsync("/api/v1/play/actions/"+intent.ActionId)).StatusCode);
        api.Viewer.DefaultRequestHeaders.Authorization=new("Bearer","invalid");
        Assert.Equal(HttpStatusCode.Unauthorized,(await api.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).StatusCode);
    }
    [Theory]
    [InlineData("favorite","{\"instanceId\":\"x\",\"enabled\":\"toggle\"}")]
    [InlineData("bossAttack","{\"userId\":\"1002\"}")]
    [InlineData("salvageBatch","{\"selection\":\"all\"}")]
    [InlineData("community","{\"game\":\"heist\",\"operation\":\"start\",\"eventId\":\"-\",\"stake\":\"0\",\"target\":\"-\"}")]
    [InlineData("community","{\"game\":\"duel\",\"operation\":\"challenge\",\"eventId\":\"-\",\"stake\":\"100\",\"target\":\"target !reset\"}")]
    public void Invalid_or_privileged_inputs_are_rejected_by_shared_protocol(string kind,string args)=>
        Assert.Throws<ArgumentException>(()=>RpgIntents.Hash(new(Guid.NewGuid(),kind,JsonSerializer.Deserialize<JsonElement>(args))));
}
