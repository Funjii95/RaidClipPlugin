namespace RaidClipPlugin.Models;

public enum RpgEffectTrigger { DoubleAttack, TripleAttack, ExtraLoot, SushiPreservation }

public sealed class RpgItemEffect
{
    public bool RequiresCritical { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public RpgEffectTrigger Trigger { get; set; }
    public decimal Chance { get; set; }
    public decimal Strength { get; set; } = 0.5m;
    public long MinimumStake { get; set; }
    public int CooldownSeconds { get; set; }
    public RpgActivity? Activity { get; set; }
}

public sealed class RpgAttributeRange
{
    public string Attribute { get; set; } = nameof(RpgAttributes.Damage);
    public int Minimum { get; set; } = 1;
    public int Maximum { get; set; } = 5;
}

public sealed class RpgItemGeneration
{
    public List<RpgAttributeRange> BaseAttributes { get; set; } = new();
    public int? MinimumAttributes { get; set; }
    public int? MaximumAttributes { get; set; }
    public int? AttributeCount { get; set; }
    public bool GenerateEffect { get; set; } = true;
    public int DoubleAttackMinimumChance { get; set; } = 2;
    public int DoubleAttackMaximumChance { get; set; } = 4;
    public List<RpgAttributeRange> Attributes { get; set; } = new();
    public List<string> EffectIds { get; set; } = new();
}
