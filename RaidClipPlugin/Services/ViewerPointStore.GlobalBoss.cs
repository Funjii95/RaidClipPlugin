using System.Text.Json;
using System.Text.Json.Nodes;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;
public sealed partial class ViewerPointStore
{
 private RpgGlobalBossState? _globalBoss;
 private const string GlobalBossKey="$globalBoss";
 private Dictionary<string,ViewerPointEntry> ReadAccounts(JsonElement root)
 {
  var node=JsonNode.Parse(root.GetRawText())!.AsObject();
  _globalBoss=node[GlobalBossKey]?.Deserialize<RpgGlobalBossState>(JsonOptions);node.Remove(GlobalBossKey);
  if(_globalBoss is not null&&(_globalBoss.Remaining is <0 or >1||string.IsNullOrWhiteSpace(_globalBoss.Id)))throw new InvalidDataException("Ungültiger globaler Bossfortschritt.");
  return node.Deserialize<Dictionary<string,ViewerPointEntry>>(JsonOptions)??throw new InvalidDataException("Konten fehlen.");
 }
 private JsonObject AccountsWithBoss()
 {
  if(_globalBoss is not null)foreach(var id in _globalBoss.Participants.Keys.Where(IsPrivacyBlocked).ToArray()){
   var name=_globalBoss.Participants[id].Name;_globalBoss.Participants.Remove(id);
   if(!string.IsNullOrEmpty(name))_globalBoss.Log.RemoveAll(line=>line.Contains(name,StringComparison.Ordinal));
  }
  var node=JsonSerializer.SerializeToNode(_entries,JsonOptions)!.AsObject();
  if(_globalBoss is not null)node[GlobalBossKey]=JsonSerializer.SerializeToNode(_globalBoss,JsonOptions);
  return node;
 }
 internal async Task<RpgGlobalBossState?> ReadGlobalBossAsync(CancellationToken ct)
 {
  await _lock.WaitAsync(ct);try{await EnsureLoadedAsync(ct);return _globalBoss is null?null:JsonSerializer.Deserialize<RpgGlobalBossState>(JsonSerializer.Serialize(_globalBoss))!;}finally{_lock.Release();}
 }
 internal async Task SaveGlobalBossAsync(RpgGlobalBossState state,CancellationToken ct)
 {
  await _lock.WaitAsync(ct);try{await EnsureLoadedAsync(ct);_globalBoss=JsonSerializer.Deserialize<RpgGlobalBossState>(JsonSerializer.Serialize(state))!;await SaveAsync(ct);}finally{_lock.Release();}
 }
}
