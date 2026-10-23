namespace RaidClipPlugin.Config;
public sealed class RpgAdminAccess
{
 public List<string> Moderators {get;set;}=[];
 public List<string> TestPlayers {get;set;}=[];
 public void Validate(){if(Moderators.Count>50||TestPlayers.Count>50||Moderators.Concat(TestPlayers).Any(id=>!System.Text.RegularExpressions.Regex.IsMatch(id,"^[0-9]{1,25}$"))||Moderators.Distinct().Count()!=Moderators.Count||TestPlayers.Distinct().Count()!=TestPlayers.Count)throw new InvalidOperationException("Ungültige Twitch-IDs in der Verwaltungsfreigabe.");}
}
