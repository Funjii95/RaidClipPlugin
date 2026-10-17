using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgGermanPresentationTests
{
    [Fact]
    public void AllAttributeNamesAreGermanAndValuesComeFromAttributes()
    {
        var a = new RpgAttributes();
        foreach (var property in typeof(RpgAttributes).GetProperties()) property.SetValue(a, 2.5m);
        var text = RpgChatPresentation.Bonuses(a);
        foreach (var label in new[] { "Schaden", "Kritische Trefferchance", "Kritischer Schaden", "Glück", "Boss-Schaden", "Perlenbonus",
            "Angelglück", "Tauchglück", "Doppelangriffschance", "Dreifachangriffschance", "Zusätzlicher Lootwurf", "Sushi-Erhaltung" }) Assert.Contains(label,text);
        foreach (var property in typeof(RpgAttributes).GetProperties()) Assert.DoesNotContain(property.Name,text);
        Assert.Contains("2,5",text);
    }

    [Fact]
    public void EffectsExplainActualChancesStrengthsAndConditionsWithoutInternalIds()
    {
        var e = new RpgItemEffect { Id="internal-proc-id", Name="Gezeitenkraft", Trigger=RpgEffectTrigger.DoubleAttack,
            Chance=7.5m, Strength=0.35m, RequiresCritical=true, MinimumStake=250, CooldownSeconds=12 };
        var text = RpgPassiveService.Describe(e);
        Assert.Contains("7,5 %",text); Assert.Contains("35 %",text); Assert.Contains("kritischem Treffer",text);
        Assert.Contains("250",text); Assert.Contains("12 s",text); Assert.DoesNotContain(e.Id,text);
        e.Trigger = RpgEffectTrigger.ExtraLoot;
        Assert.Contains("Einsatzanforderungen gelten weiterhin",RpgPassiveService.Describe(e));
        e.Trigger = RpgEffectTrigger.SushiPreservation;
        Assert.Contains("Aufladung diese zu behalten",RpgPassiveService.Describe(e));
    }

    [Fact]
    public async Task CompactInventoryAndEquipReplyKeepStructuredDetailsAndHandleUnknownNumbers()
    {
        var dir = Path.Combine(Path.GetTempPath(),"RaidClipGerman-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ViewerPointStore(dir); var config = new RpgConfig { Enabled=true };
            var item = RpgEquipmentCatalog.Items().Single(i=>i.ItemId=="leviathan-grip");
            await store.ApplyRpgAsync("42","Otter","seed",a=> { a.Rpg.Inventory.Add(item); a.Rpg.Inventory.Add(new() { InstanceId="legacy",DisplayName="Alte Muschel" }); return new(true); },default);
            Task<string> Chat(string text) => new RpgChatCommand(store,config).ExecuteAsync(new() { UserId="42",UserName="Otter",Id=Guid.NewGuid().ToString("N") },text.Split(' ')[0],text.Split(' '),default);
            var inv = await Chat("!inv");
            Assert.Contains("1. 🧤 Leviathans Griff [Legendary]",inv); Assert.DoesNotContain("Schaden",inv); Assert.DoesNotContain(item.InstanceId,inv);
            Assert.Contains("existiert nicht",await Chat("!item 99"));
            var equipped = await Chat("!equip 1"); Assert.Contains("Spezialeffekt",equipped); Assert.Contains("10 %",equipped); Assert.Contains("50 %",equipped);
            var detail = await Chat("!item 1"); Assert.Contains("Handschuhe",detail); Assert.Contains("ausgerüstet",detail); Assert.Contains("+8 % Schaden",detail);
            Assert.DoesNotContain(item.InstanceId,detail);
            var legacy = await Chat("!item 2"); Assert.Contains("Schatz",legacy); Assert.Contains("keine Attributboni",legacy);
        }
        finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
    }
}
