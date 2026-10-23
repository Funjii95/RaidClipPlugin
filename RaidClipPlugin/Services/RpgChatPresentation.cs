using System.Globalization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgChatPresentation
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    public static string Number(decimal value) => value.ToString("#,0.############################", German);
    public static string Clean(string text) => (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
    public static string ItemName(RpgItem item) => !string.IsNullOrWhiteSpace(item.DisplayName) ? item.DisplayName :
        !string.IsNullOrWhiteSpace(item.ItemId) ? item.ItemId : "Unbenanntes Item";
    public static string RecipeName(RpgRecipe recipe) => !string.IsNullOrWhiteSpace(recipe.DisplayName) ? Clean(recipe.DisplayName) : recipe.Id switch
    { "fishing" => "Angler-Sushi", "fighter" => "Kämpfer-Sushi", "critical" => "Krit-Sushi", "dive" => "Taucher-Sushi", _ => "Sushi " + Clean(recipe.Id) };
    public static string PerkName(RpgPerkDefinition perk) => !string.IsNullOrWhiteSpace(perk.DisplayName) ? Clean(perk.DisplayName) : perk.Id switch
    { "damage" => "Schaden", "crit" => "Kritische Treffer", "critdamage" => "Kritischer Schaden", "luck" => "Glück",
        "boss" => "Boss-Schaden", "dive" => "Tauchglück", "fishing" => "Angelglück", _ => Clean(perk.Id) };
    public static string Activity(RpgActivity activity) => activity switch
    { RpgActivity.Fishing => "Angelaktionen", RpgActivity.Dive => "Tauchgänge", RpgActivity.Boss => "Bossangriffe", _ => "Aktionen" };
    public static string SlotName(EquipmentSlot slot) => slot switch
    { EquipmentSlot.Weapon => "Waffe", EquipmentSlot.Helmet => "Helm", EquipmentSlot.Chest => "Brust", EquipmentSlot.Gloves => "Handschuhe",
        EquipmentSlot.Boots => "Schuhe", EquipmentSlot.Ring1 => "Ring 1", EquipmentSlot.Ring2 => "Ring 2", EquipmentSlot.Amulet => "Amulett", _ => slot.ToString() };
    public static string ItemIcon(EquipmentSlot? slot) => slot switch
    {
        EquipmentSlot.Gloves => "🧤", EquipmentSlot.Helmet => "🪖", EquipmentSlot.Chest => "🛡️",
        EquipmentSlot.Weapon => "🔱", EquipmentSlot.Boots => "👢", EquipmentSlot.Amulet => "📿",
        EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => "💍", _ => "🦦"
    };
    public static string KindName(RpgItem item) => item.Slot.HasValue ? "Ausrüstung" :
        item.Kind == RpgItemKind.Consumable ? "Verbrauchsgegenstand" : "Schatz";
    public static RpgAttributes Snapshot(RpgAttributes a, decimal levels = 1) => new()
    { Armor=a.Armor*levels,ArmorPercent=a.ArmorPercent*levels,MaximumHpPercent=a.MaximumHpPercent*levels,AttackSpeed=a.AttackSpeed*levels,HealingPower=a.HealingPower*levels,
        BossDamageReduction=a.BossDamageReduction*levels,BossDodgeChance=a.BossDodgeChance*levels,CriticalHealing=a.CriticalHealing*levels,RareEquipmentLuck=a.RareEquipmentLuck*levels,
        Power=a.Power*levels,Stamina=a.Stamina*levels,MaximumHp=a.MaximumHp*levels, Damage = a.Damage * levels, CritChance = a.CritChance * levels, CritDamage = a.CritDamage * levels, Luck = a.Luck * levels,
        BossDamage = a.BossDamage * levels, PearlBonus = a.PearlBonus * levels, FishingLuck = a.FishingLuck * levels, DiveLuck = a.DiveLuck * levels,
        DoubleAttackChance = a.DoubleAttackChance * levels, TripleAttackChance = a.TripleAttackChance * levels,
        ExtraLootChance = a.ExtraLootChance * levels, SushiPreservationChance = a.SushiPreservationChance * levels };
    public static string Bonuses(RpgAttributes? a)
    {
        if (a is null) return "keine Attributboni";
        var values = new List<string>();
        void Add(decimal value, string label, bool percent = false)
        { if (value != 0) values.Add((value > 0 ? "+" : "") + Number(value) + (percent ? " % " : " ") + label); }
        Add(a.Damage, "Schaden", true); Add(a.CritChance, "Kritische Trefferchance", true); Add(a.CritDamage, "Kritischer Schaden", true);
        Add(a.BossDamageReduction,"Boss-Gegenschadensreduktion",true);Add(a.BossDodgeChance,"Boss-Ausweichen",true);
        Add(a.CriticalHealing,"Heilung aus tatsächlichem kritischem Boss-Schaden",true);Add(a.RareEquipmentLuck,"relative seltene Ausrüstungs-Lootchance",true);
        Add(a.Luck, "Glück"); Add(a.BossDamage, "Boss-Schaden", true); Add(a.PearlBonus, "Perlenbonus", true);
        Add(a.MaximumHp,"maximale Lebenspunkte");
        Add(a.Armor,"Rüstung");Add(a.ArmorPercent,"Rüstungsbonus",true);Add(a.MaximumHpPercent,"maximale Lebenspunkte",true);
        Add(a.AttackSpeed,"Angriffstempo",true);Add(a.HealingPower,"Heilungswirkung",true);
        Add(a.Power,"Kraft");Add(a.Stamina,"Ausdauer");
        Add(a.FishingLuck, "Angelglück"); Add(a.DiveLuck, "Tauchglück");
        Add(a.DoubleAttackChance, "Doppelangriffschance", true); Add(a.TripleAttackChance, "Dreifachangriffschance", true);
        Add(a.ExtraLootChance, "Zusätzlicher Lootwurf", true); Add(a.SushiPreservationChance, "Sushi-Erhaltung", true);
        return values.Count == 0 ? "keine Attributboni" : string.Join(", ", values);
    }
    public static string Success(string userName, RpgActionDetails details, long? balance = null)
    {
        var user = "@" + Clean(userName); var name = Label(details.Name);
        return details.Kind switch
        {
            RpgActionKind.Craft => $"🍣 {user} hat {name} hergestellt! Verbraucht: {(details.IngredientsDescription.Length>0?details.IngredientsDescription:$"{Number(details.FishQuantity)}× {Label(details.FishName)}")} + {Number(details.Cost)} Perlen. Erhalten: {Number(details.Quantity)}× {name}. " +
                $"Buff nach Aktivierung: {(details.EffectDescription.Length>0?details.EffectDescription:Bonuses(details.Bonuses))}, {Number(details.RemainingUses)} {(details.EncounterBased?"Bosskampfteilnahmen":"gültige "+Activity(details.Activity??RpgActivity.Dive))} (noch nicht aktiviert).",
            RpgActionKind.ActivateSushi => $"🍣✨ {user} aktiviert {name}! {(details.EffectDescription.Length>0?details.EffectDescription:Bonuses(details.Bonuses))} für die nächsten {Number(details.RemainingUses)} {(details.EncounterBased?"Bosskampfteilnahmen":"gültigen "+Activity(details.Activity??RpgActivity.Dive))}.",
            RpgActionKind.BuyPerk => $"⭐ {user} verbessert {name} auf Level {Number(details.Level)}! {(details.EffectDescription.Length>0?details.EffectDescription:Bonuses(details.Bonuses))} insgesamt aus diesem Perk. Kosten: {Number(details.Cost)} Perlen." +
                (balance.HasValue ? $" Stand: {Number(balance.Value)} Perlen." : "") +
                (details.NextCost.HasValue ? $" Nächstes Upgrade: {Number(details.NextCost.Value)} Perlen." : details.MaximumLevel > 0 && details.Level >= details.MaximumLevel ? " Maximallevel erreicht." : ""),
            RpgActionKind.Equip => $"{ItemIcon(details.Slot)} {user} rüstet {name} aus ({SlotName(details.Slot ?? EquipmentSlot.Weapon)})! {Bonuses(details.Bonuses)}." +
                string.Concat((details.Effects ?? Array.Empty<RpgItemEffect>()).Select(e => " Spezialeffekt: " + RpgPassiveService.Describe(e) + ".")) +
                (details.SetName.Length > 0 ? " Set: " + Clean(details.SetName) + ". Fortschritt: !sets." : ""),
            _ => $"{user}, Aktion abgeschlossen."
        };
    }
    private static string Label(string name) => Clean(name);
    public static string AdventureOutcome(RpgAdventureResult result, RpgActivity activity)
    {
        if(activity==RpgActivity.Fishing && result.ShellsAwarded>0)return "Eine Muschel gefunden! Öffnen: !muscheln <1–100>.";
        if (activity == RpgActivity.Fishing)
            return (result.Item is null ? "🎣 Diesmal kein Fisch gefangen." : $"🎣 Gefangen: 1× {Label(ItemName(result.Item))} ({result.Item.Rarity}).") +
                (result.AdditionalItems.Count > 0 ? " Ausrüstung gefunden: " + string.Join(", ", result.AdditionalItems.Select(i => Label(ItemName(i)))) + "." : "");
        var enemyText=result.Enemy is {} enemy ? $"⚔️ {Label(enemy.Name)}: " + (enemy.Victory ? $"besiegt, +{enemy.Experience} EXP" : "Kampf verloren, keine Gegnerbeute/EXP") +
            $", -{enemy.DamageTaken} HP, ❤️ {enemy.CurrentHp}/{enemy.MaximumHp}. " : "";
        return enemyText + (result.Item?.Rarity == ItemRarity.Legendary ? "💎 " : "🦦 ") + result.Outcome + (result.Item is null ? "" : $" Fund: {Label(ItemName(result.Item))} ({result.Item.Rarity})" +
            (result.Item.Slot.HasValue ? " – " + Bonuses(result.Item.Attributes) : "") + ".") +
            (result.AdditionalItems.Count > 0 ? " Extra-Fund: " + string.Join(", ", result.AdditionalItems.Select(i => Label(ItemName(i)) + " [" + i.Rarity + "]")) + "." : "");
    }
}
