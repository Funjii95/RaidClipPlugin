namespace RaidClipPlugin.Models;

// Additive 0.0.3 resource storage; legacy profiles start with zero resources.
public sealed class RpgMaterials
{
    public int Shells { get; set; }
    public int WhitePearls { get; set; }
    public int BluePearls { get; set; }
    public int RedPearls { get; set; }
    public int StoneChips { get; set; }
    public void Validate()
    {
        if (Shells < 0 || WhitePearls < 0 || BluePearls < 0 || RedPearls < 0 || StoneChips < 0)
            throw new InvalidDataException("Invalid RPG resource balance.");
    }
}
