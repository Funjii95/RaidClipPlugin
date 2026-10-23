using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SnapshotProtocol;
namespace Backend.Tests;
public sealed class LargeStatusTests
{
 [Fact]public async Task Full_status_larger_than_old_limit_survives_completion_and_viewer_read()
 {
  await using var f=new ActionApiFixture();var session=await f.ReadyAsync();var intent=ActionApiFixture.Intent("status","{}");
  (await f.Viewer.PostAsJsonAsync("/api/v1/play/actions",intent)).EnsureSuccessStatusCode();
  (await f.Plugin.PostAsync("/api/v1/plugin/actions/claim/"+session,null)).EnsureSuccessStatusCode();
  string data=JsonSerializer.Serialize(new{profile=new{UserId="1001",Inventory=Enumerable.Range(0,100).Select(i=>new{InstanceId="item-"+i,Description=new string('x',700)})},stats=new{Damage=17}});
  var outcome=new ActionOutcome(true,"read","Aktueller Plugin-Stand.",data);
  Assert.True(JsonSerializer.SerializeToUtf8Bytes(outcome).Length>65536);
  Assert.Equal(HttpStatusCode.OK,(await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",new RelayComplete(session,intent.ActionId,outcome))).StatusCode);
  var receipt=await f.Viewer.GetFromJsonAsync<ActionReceipt>("/api/v1/play/actions/"+intent.ActionId);
  Assert.Equal(data,receipt!.Outcome!.DataJson);
  (await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",new RelayComplete(session,intent.ActionId,outcome))).EnsureSuccessStatusCode();
 }
 [Fact]public async Task Oversized_completions_remain_bounded()
 {
  await using var f=new ActionApiFixture();var session=await f.ReadyAsync();
  var result=await f.Plugin.PostAsJsonAsync("/api/v1/plugin/actions/complete",new RelayComplete(session,Guid.NewGuid(),new(true,"read","",new string('x',ActionResultLimits.MaximumCompletionBodyBytes))));
  Assert.Equal(HttpStatusCode.RequestEntityTooLarge,result.StatusCode);
 }
}
