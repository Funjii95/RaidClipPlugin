namespace RaidClipPlugin.Models;

public sealed class RaidEvent
{
    public string FromBroadcasterId { get; init; } = "";
    public string FromBroadcasterLogin { get; init; } = "";
    public string FromBroadcasterName { get; init; } = "";
    public int Viewers { get; init; }
}
