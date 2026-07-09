namespace RaidClipPlugin.Models;

public enum CommandRole { Viewer, Follower, Subscriber, Vip, Moderator, Broadcaster }

public sealed record ChatCommandDefinition(
    string CommandId, string CommandText, IReadOnlyList<string> Aliases,
    string ModuleId, string ModuleDisplayName, string DisplayName,
    string Description, string Usage, string Example, bool Enabled,
    bool IsVisible, CommandRole RequiredRole, TimeSpan UserCooldown,
    TimeSpan GlobalCooldown, long PointCost, int SortOrder);

public sealed record CommandCollision(
    string Command, ChatCommandDefinition First, ChatCommandDefinition Second)
{
    public string Message => $"Der Command {Command} wird sowohl vom {First.ModuleDisplayName}-Modul als auch vom {Second.ModuleDisplayName}-Modul verwendet.";
}

