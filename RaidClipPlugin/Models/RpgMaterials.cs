namespace RaidClipPlugin.Models;

// Additive 0.0.3 resource storage; legacy profiles start with zero resources.
public sealed class RpgMaterials
{
    public int PearlDust { get; set; }
    public int LuminousScale { get; set; }
    public int RuneShell { get; set; }
    public int EternalDepthCore { get; set; }
    public int TrueCoralCore { get; set; }
    public int CursedSilk { get; set; }
    public int AbyssShard { get; set; }
    public int ReinforcementCore { get; set; }
    public int Shells { get; set; }
    public int WhitePearls { get; set; }
    public int BluePearls { get; set; }
    public int RedPearls { get; set; }
    public int StoneChips { get; set; }
    public void Validate()
    {
        if (typeof(RpgMaterials).GetProperties().Any(p=>(int)p.GetValue(this)!<0))
            throw new InvalidDataException("Invalid RPG resource balance.");
    }
}
