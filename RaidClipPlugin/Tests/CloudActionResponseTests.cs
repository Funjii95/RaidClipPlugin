using System.Text;
using System.Text.Json;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class CloudActionResponseTests
{
 [Fact] public async Task Large_catalog_completion_receipt_is_accepted()
 {
  var receipt=new ActionReceipt(Guid.NewGuid(),"applied",new(true,"ok","Katalog",new string('x',136000)));
  using var input=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(receipt,CloudActionClient.Json));
  Assert.Equal(receipt,await CloudActionClient.ReadResponseAsync<ActionReceipt>(input,default));
 }
 [Fact] public async Task Relay_response_remains_bounded_by_shared_protocol_limit()
 {
  using var input=new MemoryStream(Encoding.UTF8.GetBytes(new string('x',ActionResultLimits.MaximumCompletionBodyBytes+1)));
  await Assert.ThrowsAsync<InvalidDataException>(()=>CloudActionClient.ReadResponseAsync<ActionReceipt>(input,default));
 }
}
