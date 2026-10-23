using System.Text.Json;
namespace SnapshotProtocol;
public static class AdminPermissions
{
 public static bool ModeratorMay(RpgIntent intent)
 {
  string Arg(string key)=>intent.Arguments.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
  return intent.Kind switch{
   "adminDashboard" or "adminPlayers" or "adminHealAll"=>true,
   "adminStart"=>Arg("event") is "boss" or "mimic" or "race" or "heist" or "hotTime",
   "adminPlayer"=>Arg("operation")=="profile",
   "adminControl"=>Arg("operation") is "finishboss" or "cancelboss" or "cancelrace" or "cancelheist" or "finishheist" or "finishrace" or "hotTimeStop",
   "adminZoneDebug"=>Arg("operation") is "enemy" or "chest" or "mimic" or "simulate",
   _=>false};
 }
 // Only the authenticated plugin heartbeat can publish this catalogue; browser flags are ignored.
 public static bool IsModerator(string catalog,string user)
 {
  try{using var doc=JsonDocument.Parse(catalog);return doc.RootElement.TryGetProperty("staffAccess",out var access)&&access.TryGetProperty("moderators",out var users)&&users.ValueKind==JsonValueKind.Array&&users.GetArrayLength()<=50&&users.EnumerateArray().Any(v=>v.ValueKind==JsonValueKind.String&&v.GetString()==user);}
  catch(JsonException){return false;}
 }
}
