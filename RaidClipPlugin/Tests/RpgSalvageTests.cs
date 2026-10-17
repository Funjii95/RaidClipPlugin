using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgSalvageTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipSalvage-"+Guid.NewGuid().ToString("N"));
    private readonly ViewerPointStore _points;
    private readonly RpgConfig _config=new() { Enabled=true };
    private DateTimeOffset _now=DateTimeOffset.UtcNow;
    public RpgSalvageTests() => _points=new(_dir);
    public void Dispose() { if(Directory.Exists(_dir)) Directory.Delete(_dir,true); }
    private RpgSalvageService Service(long max=long.MaxValue)=>new(_points,_config,()=>_now,max);
    private Task<RpgTransactionResult> Change(Action<ViewerPointEntry> change)=>_points.ApplyRpgAsync("42","Otter",Guid.NewGuid().ToString("N"),a=> { change(a);return new(true); },default);
    private async Task Seed(params RpgItem[] items)
    {
        await Change(a=> { a.Points=1000;a.Rpg.Inventory.AddRange(items); });
        RpgInventoryNumbers.For(_points).Remember("42",(await _points.GetProfileAsync("42",default)).Entry.Rpg);
    }
    private static RpgItem Item(string id,ItemRarity rarity=ItemRarity.Common)=>new() { InstanceId=id,ItemId="gloves",DisplayName="Otterhandschuhe",Slot=EquipmentSlot.Gloves,Rarity=rarity,Attributes=new() { Damage=1 } };
    private async Task<RpgSalvageQuote> Preview(string selection="1")
    { var result=await Service().PreviewAsync("42",selection,default);Assert.True(result.Success,result.Error);return result.Quote!; }

    [Theory]
    [InlineData(ItemRarity.Common,25,50)] [InlineData(ItemRarity.Uncommon,75,150)] [InlineData(ItemRarity.Rare,200,400)]
    [InlineData(ItemRarity.Epic,600,1200)] [InlineData(ItemRarity.Legendary,2000,4000)]
    public void ValuesAreDeterministicAndWithinRange(ItemRarity rarity,long min,long max)
    {
        var item=Item("old-item-without-value",rarity);
        var value=RpgSalvageService.Value(item,_config.Salvage);
        Assert.InRange(value,min,max);Assert.Equal(value,RpgSalvageService.Value(item,new RpgSalvageConfig()));
        item.SalvageValue=42;Assert.Equal(42,RpgSalvageService.Value(item,_config.Salvage));
    }
    [Fact]
    public async Task ConfirmationRemovesExactlyTheQuotedItemAndCreditsOnceAcrossParallelRequestsAndRestart()
    {
        await Seed(Item("one"),Item("two"));var q=await Preview();
        Assert.Equal(1000,await _points.GetPointsAsync("42",default));
        var results=await Task.WhenAll(Enumerable.Range(0,12).Select(i=>Service().ConfirmAsync("42","Otter","request"+i,q.Token,default)));
        Assert.Single(results,r=>r.Success);
        var saved=(await new ViewerPointStore(_dir).GetProfileAsync("42",default)).Entry;
        Assert.Equal(1000+q.Total,saved.Points);Assert.Equal("two",Assert.Single(saved.Rpg.Inventory).InstanceId);
        Assert.False((await Service().ConfirmAsync("42","Otter","again",q.Token,default)).Success);
        Assert.False((await new RpgSalvageService(new(_dir),_config).ConfirmAsync("42","Otter","restart",q.Token,default)).Success);
    }
    [Theory]
    [InlineData("equipped")] [InlineData("locked")] [InlineData("favorite")]
    public async Task ProtectedItemsCannotBePreviewedOrConfirmedAfterProtectionChanges(string mode)
    {
        await Seed(Item("one"));var q=await Preview();
        await Change(a=> { if(mode=="equipped") a.Rpg.Equipment[EquipmentSlot.Gloves]="one";else if(mode=="locked") a.Rpg.Inventory[0].IsLocked=true;else a.Rpg.Inventory[0].IsFavorite=true; });
        Assert.False((await Service().PreviewAsync("42","1",default)).Success);
        Assert.False((await Service().ConfirmAsync("42","Otter","confirm",q.Token,default)).Success);
        Assert.Single((await _points.GetProfileAsync("42",default)).Entry.Rpg.Inventory);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task LegendaryAndSetPartsRequireSpecificUnexpiredConfirmationAndNeverEnterBulk(bool legendary)
    {
        var item=Item("sensitive",legendary?ItemRarity.Legendary:ItemRarity.Common);if(!legendary)item.SetId="custom-set";
        await Seed(item);var q=await Preview();Assert.True(q.Items[0].RequiresWarning);
        Assert.False((await Service().PreviewAsync("42","all common",default)).Success);
        Assert.False((await Service().ConfirmAsync("43","Other","other",q.Token,default)).Success);
        _now=_now.AddSeconds(61);Assert.False((await Service().ConfirmAsync("42","Otter","expired",q.Token,default)).Success);
        q=await Preview();Assert.True((await Service().ConfirmAsync("42","Otter","valid",q.Token,default)).Success);
    }
    [Fact]
    public async Task DuplicateSelectionKeepsOneAndPreservesDifferentRollsAndEffects()
    {
        var a=Item("a");var b=Item("b");var c=Item("c");var different=Item("different");different.Attributes.Damage=2;
        var proc=Item("proc");proc.Effects.Add(new() {Id="double",Chance=5,Trigger=RpgEffectTrigger.DoubleAttack});
        await Seed(a,b,c,different,proc);
        var q=await Preview("duplicates");Assert.Equal(new[] {"b","c"},q.Items.Select(i=>i.InstanceId));
        Assert.True((await Service().ConfirmAsync("42","Otter","batch",q.Token,default)).Success);
        var saved=(await _points.GetProfileAsync("42",default)).Entry;Assert.Equal(3,saved.Rpg.Inventory.Count);Assert.Equal(1000+q.Total,saved.Points);
        Assert.Equal(2,saved.Rpg.Inventory.Single(i=>i.InstanceId=="different").Attributes.Damage);
    }
    [Fact]
    public async Task InventoryChangesAndAccountLimitRejectWholeTransaction()
    {
        await Seed(Item("a"),Item("b"));var q=await Preview();
        Assert.False((await Service(1000).ConfirmAsync("42","Otter","cap",q.Token,default)).Success);
        await Change(a=>a.Rpg.Inventory.RemoveAt(0));
        Assert.False((await Service().ConfirmAsync("42","Otter","stale",q.Token,default)).Success);
        Assert.Equal(1000,await _points.GetPointsAsync("42",default));Assert.Equal("b",Assert.Single((await _points.GetProfileAsync("42",default)).Entry.Rpg.Inventory).InstanceId);
    }
    [Fact]
    public async Task FailedSaveRollsBackItemsAndCurrencyAndAllowsSafeRetry()
    {
        await Seed(Item("a"));var q=await Preview();var temp=Path.Combine(_dir,"viewer-points.json.tmp");Directory.CreateDirectory(temp);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service().ConfirmAsync("42","Otter","save",q.Token,default));
        var current=(await _points.GetProfileAsync("42",default)).Entry;Assert.Single(current.Rpg.Inventory);Assert.Equal(1000,current.Points);
        Directory.Delete(temp);Assert.True((await Service().ConfirmAsync("42","Otter","save",q.Token,default)).Success);
    }
    [Fact]
    public async Task ChatShowsPreviewPriceAndOnlyConfirmationExecutes()
    {
        await Seed(Item("a"));
        Task<string> Chat(string text)=>new RpgChatCommand(_points,_config).ExecuteAsync(new() {UserId="42",UserName="Otter",Id=Guid.NewGuid().ToString("N")},text.Split(' ')[0],text.Split(' '),default);
        var text=await Chat("!salvage 1");Assert.Contains("Otterhandschuhe",text);Assert.Contains("Perlen",text);Assert.Contains("!salvage 1 confirm",text);
        Assert.Single((await _points.GetProfileAsync("42",default)).Entry.Rpg.Inventory);
        Assert.Contains("zerlegt:",await Chat("!salvage 1 confirm"));
        Assert.Contains("keine passende gültige Vorschau",await Chat("!salvage 1 confirm"));
    }
}
