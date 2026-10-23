using System.Reflection;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgItemGenerationService
{
    public static RpgConfig EnableForFutureDrops(RpgConfig current)
    {
        var copy = JsonSerializer.Deserialize<RpgConfig>(JsonSerializer.Serialize(current))!;
        foreach (var effect in new RpgLootConfig().Effects)
            if (!copy.DiveLoot.Effects.Any(e => e.Id == effect.Id)) copy.DiveLoot.Effects.Add(effect);
        var fixedExpansion=RpgLootExpansionCatalog.Items().Select(i=>i.ItemId).ToHashSet();
        foreach (var item in copy.DiveLoot.Items.Where(i => !fixedExpansion.Contains(i.ItemId) && i.Slot.HasValue && i.Kind != RpgItemKind.Consumable && i.Generation is null && i.Effects.Count == 0 && i.Attributes.MaximumHp == 0))
            item.Generation = DefaultRules(item.Slot!.Value, item.Rarity);
        copy.Validate(); return copy;
    }
    private static readonly Dictionary<string, PropertyInfo> Attributes = typeof(RpgAttributes).GetProperties().ToDictionary(p => p.Name, StringComparer.Ordinal);
    public static (int Min, int Max, int EffectsMin, int EffectsMax) Counts(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => (1, 1, 0, 0), ItemRarity.Uncommon => (1, 2, 0, 0),
        ItemRarity.Rare => (2, 3, 0, 1), ItemRarity.Epic => (3, 4, 1, 1), _ => (4, 5, 1, 1)
    };
    public static RpgItemGeneration DefaultRules(EquipmentSlot slot, ItemRarity rarity)
    {
        string[] pool = slot switch
        {
            EquipmentSlot.Weapon => ["Damage", "CritDamage", "BossDamage", "CritChance", "Luck"],
            EquipmentSlot.Helmet => ["Luck", "FishingLuck", "DiveLuck", "CritChance", "Damage"],
            EquipmentSlot.Chest => ["Damage", "BossDamage", "CritDamage", "Luck", "CritChance"],
            EquipmentSlot.Gloves => ["CritChance", "Damage", "CritDamage", "BossDamage", "Luck"],
            EquipmentSlot.Boots => ["DiveLuck", "FishingLuck", "PearlBonus", "Luck", "Damage"],
            EquipmentSlot.Amulet => ["Luck", "PearlBonus", "DiveLuck", "FishingLuck", "CritChance"],
            _ => ["CritChance", "CritDamage", "Luck", "Damage", "BossDamage"]
        };
        var max = 2 + (int)rarity * 2;
        var effects = slot is EquipmentSlot.Helmet or EquipmentSlot.Boots ? new[] { "extra-loot" } :
            slot == EquipmentSlot.Amulet ? new[] { "sushi-save" } : rarity == ItemRarity.Legendary ? new[] { "double", "triple" } : new[] { "double" };
        return new() { Attributes = pool.Select(a => new RpgAttributeRange { Attribute = a, Maximum = max }).ToList(), EffectIds = effects.ToList(),
            DoubleAttackMinimumChance = rarity == ItemRarity.Legendary ? 7 : rarity == ItemRarity.Epic ? 4 : 2,
            DoubleAttackMaximumChance = rarity == ItemRarity.Legendary ? 12 : rarity == ItemRarity.Epic ? 7 : 4 };
    }
    public static void Validate(RpgLootConfig config)
    {
        if (config.Effects.Select(e => e.Id).Distinct().Count() != config.Effects.Count || config.Effects.Any(e =>
            string.IsNullOrWhiteSpace(e.Id) || !Enum.IsDefined(e.Trigger) || e.Chance is < 0 or > 100 || e.Strength is <= 0 or > 1 ||
            e.MinimumStake < 0 || e.CooldownSeconds is < 0 or > 86400 || e.Activity.HasValue && !Enum.IsDefined(e.Activity.Value) ||
            e.Trigger is RpgEffectTrigger.DoubleAttack or RpgEffectTrigger.TripleAttack &&
                (e.MinimumStake > 0 || e.Activity.HasValue && e.Activity != RpgActivity.Boss) ||
            e.Trigger == RpgEffectTrigger.ExtraLoot && e.Activity.HasValue && e.Activity != RpgActivity.Dive ||
            e.Trigger == RpgEffectTrigger.TripleAttack && e.Chance > 3))
            throw new InvalidOperationException("Ungültige Itemeffekte.");
        foreach (var item in config.Items.Where(i => i.Generation is not null))
        {
            var rules = item.Generation!; var counts = Counts(item.Rarity);
            if (rules.MinimumAttributes.HasValue!=rules.MaximumAttributes.HasValue || rules.MinimumAttributes is {} lo && (lo<1 || lo>5) || rules.MaximumAttributes is {} hi && (hi<(rules.MinimumAttributes??1) || hi>5) ||
                rules.BaseAttributes.Any(a=>!Attributes.ContainsKey(a.Attribute)||a.Minimum<1||a.Maximum<a.Minimum||a.Maximum>1000) ||
                rules.BaseAttributes.Select(a=>a.Attribute).Distinct().Count()!=rules.BaseAttributes.Count ||
                !item.Slot.HasValue || item.Kind == RpgItemKind.Consumable || rules.DoubleAttackMinimumChance < 0 || rules.DoubleAttackMaximumChance > 20 || rules.DoubleAttackMaximumChance < rules.DoubleAttackMinimumChance ||
                rules.AttributeCount is < 1 or > 5 || rules.Attributes.Count < (rules.AttributeCount ?? rules.MaximumAttributes ?? counts.Max) || rules.Attributes.Select(a => a.Attribute).Distinct().Count() != rules.Attributes.Count ||
                rules.Attributes.Any(a => !Attributes.ContainsKey(a.Attribute) || a.Minimum < 1 || a.Maximum < a.Minimum || a.Maximum > 1000) ||
                rules.Attributes.Any(a => a.Attribute == nameof(RpgAttributes.TripleAttackChance) && (item.Rarity != ItemRarity.Legendary || a.Maximum > 3)) ||
                rules.EffectIds.Any(id => !config.Effects.Any(e => e.Id == id)) || rules.GenerateEffect && counts.EffectsMin > 0 && rules.EffectIds.Count == 0 ||
                item.Rarity != ItemRarity.Legendary && rules.EffectIds.Any(id => config.Effects.Single(e => e.Id == id).Trigger == RpgEffectTrigger.TripleAttack))
                throw new InvalidOperationException("Ungültige dynamische Itemregeln: " + item.ItemId);
        }
    }
    public static void Generate(RpgItem item, RpgLootConfig config, IDuelRandom random, string? requiredEffectId=null)
    {
        if (item.Generation is not { } rules)
        {
            if(requiredEffectId is not null&&!item.Effects.Any(e=>e.Id==requiredEffectId))throw new InvalidOperationException("Der gewählte Effekt gehört nicht zu diesem festen Item.");
            RpgCoreStatService.Apply(item);
            return;
        }
        var count = Counts(item.Rarity);
        if(requiredEffectId is not null&&(!rules.GenerateEffect||count.EffectsMax==0||!rules.EffectIds.Contains(requiredEffectId)))throw new InvalidOperationException("Der gewählte Effekt ist für diese Itemdefinition/Seltenheit nicht erlaubt.");
        var candidates = rules.Attributes.ToList(); item.Attributes = new(); item.CoreStatsVersion=rules.BaseAttributes.Count>0?1:0;
        foreach(var range in rules.BaseAttributes) Attributes[range.Attribute].SetValue(item.Attributes,(decimal)random.NextInclusive(range.Minimum,range.Maximum));
        var selected = rules.AttributeCount ?? random.NextInclusive(rules.MinimumAttributes??count.Min, rules.MaximumAttributes??count.Max);
        for (var i = 0; i < selected; i++)
        {
            var index = random.NextInclusive(0, candidates.Count - 1); var range = candidates[index]; candidates.RemoveAt(index);
            Attributes[range.Attribute].SetValue(item.Attributes, (decimal)random.NextInclusive(range.Minimum, range.Maximum));
        }
        item.Effects.Clear();
        if (requiredEffectId is not null || rules.GenerateEffect && rules.EffectIds.Count > 0 && random.NextInclusive(count.EffectsMin, count.EffectsMax) > 0)
        {
            var effectId=requiredEffectId??rules.EffectIds[random.NextInclusive(0,rules.EffectIds.Count-1)];
            var effect = JsonSerializer.Deserialize<RpgItemEffect>(JsonSerializer.Serialize(config.Effects.Single(e => e.Id == effectId)))!;
            // Rarity-specific chance ranges apply to newly generated attack effects only.
            if (effect.Trigger == RpgEffectTrigger.DoubleAttack)
                effect.Chance = random.NextInclusive(rules.DoubleAttackMinimumChance, rules.DoubleAttackMaximumChance);
            if (effect.Trigger == RpgEffectTrigger.TripleAttack) effect.Chance = Math.Clamp(effect.Chance, 1, 3);
            item.Effects.Add(effect);
        }
        item.Generation = null;
        RpgCoreStatService.Apply(item);
    }
}
