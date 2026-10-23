namespace RaidClipPlugin.Models;

public sealed class RpgItemSet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> ItemIds { get; set; } = new();
    public List<RpgSetBonus> Bonuses { get; set; } = new();
}
public sealed class RpgSetBonus
{
    public string Description { get; set; } = "";
    public int RequiredPieces { get; set; }
    public RpgAttributes Attributes { get; set; } = new();
}
public sealed record RpgSetProgress(RpgItemSet Set, int Equipped, IReadOnlyList<string> OwnedItemIds);
