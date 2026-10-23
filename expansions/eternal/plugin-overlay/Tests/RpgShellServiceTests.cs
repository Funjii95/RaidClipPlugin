using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgShellServiceTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-shells-"+Guid.NewGuid().ToString("N"));
    private static RpgConfig Config()=>new(){Enabled=true};
    private ViewerPointStore Store()=>new(root);
    private async Task<ViewerPointStore> Seed(int shells,long pearls=100)
    {
        var store=Store();
        await store.ApplyRpgAsync("1001","SYNTHETIC","seed",a=>{a.Points=pearls;a.Rpg.Materials.Shells=shells;return new(true);},default);
        return store;
    }
    private sealed class Rolls : IDuelRandom
    {
        private int next;
        public int Calls;
        public int NextInclusive(int min,int max){Calls++;return min==10?10:++next;}
    }
    private sealed class Fixed(int roll,int pearls=10) : IDuelRandom
    {
        public int Calls;
        public int NextInclusive(int min,int max){Calls++;return min==10?pearls:roll;}
    }
    [Fact]
    public async Task Hundred_openings_follow_exact_weights_and_survive_restart_and_projection()
    {
        var store=await Seed(100);var result=await new RpgShellService(store,Config(),new Rolls()).OpenAsync("1001","SYNTHETIC","open",100,default);
        Assert.True(result.Success);
        Assert.Equal(new ShellRewards(3,2,1,47,470),result.Rewards);
        var account=(await Store().GetProfileAsync("1001",default)).Entry;
        Assert.Equal(570,account.Points);
        var m=account.Rpg.Materials;
        Assert.Equal((0,3,2,1,47),(m.Shells,m.WhitePearls,m.BluePearls,m.RedPearls,m.StoneChips));
        var projected=SnapshotValidation.Validate(new(1,"42","1001",1,Guid.NewGuid(),DateTimeOffset.UtcNow,SnapshotKind.Upsert,JsonSerializer.Serialize(account)),"42");
        using var document=JsonDocument.Parse(projected.AccountJson!);
        Assert.Equal(47,document.RootElement.GetProperty("Rpg").GetProperty("Materials").GetProperty("StoneChips").GetInt32());
        Assert.False(document.RootElement.GetProperty("Rpg").TryGetProperty("CompletedActions",out _));
    }
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    public async Task Normal_pearl_reward_includes_both_range_endpoints(int reward)
    {
        var store=await Seed(1);
        Assert.True((await new RpgShellService(store,Config(),new Fixed(7,reward)).OpenAsync("1001","SYNTHETIC","open",1,default)).Success);
        Assert.Equal(100+reward,(await store.GetProfileAsync("1001",default)).Entry.Points);
    }
    [Fact]
    public async Task Retried_action_after_restart_never_consumes_or_awards_again()
    {
        var store=await Seed(10);var dice=new Fixed(1);
        Assert.True((await new RpgShellService(store,Config(),dice).OpenAsync("1001","SYNTHETIC","same",10,default)).Success);
        var before=await File.ReadAllBytesAsync(Path.Combine(root,"viewer-points.json"));
        var duplicate=await new RpgShellService(Store(),Config(),dice).OpenAsync("1001","SYNTHETIC","same",10,default);
        Assert.False(duplicate.Success);Assert.True(duplicate.Duplicate);Assert.Equal(10,dice.Calls);
        Assert.Equal(before,await File.ReadAllBytesAsync(Path.Combine(root,"viewer-points.json")));
    }
    [Fact]
    public async Task Concurrent_requests_share_one_authoritative_stock()
    {
        var store=await Seed(10);
        var results=await Task.WhenAll(
            new RpgShellService(store,Config(),new Fixed(1)).OpenAsync("1001","SYNTHETIC","a",10,default),
            new RpgShellService(store,Config(),new Fixed(1)).OpenAsync("1001","SYNTHETIC","b",10,default));
        Assert.Single(results,r=>r.Success);
        var m=(await store.GetProfileAsync("1001",default)).Entry.Rpg.Materials;
        Assert.Equal(0,m.Shells);Assert.Equal(10,m.WhitePearls);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(11)]
    public async Task Invalid_or_insufficient_count_changes_nothing_and_does_not_roll(int count)
    {
        var store=await Seed(10);var dice=new Fixed(1);
        var before=await File.ReadAllBytesAsync(Path.Combine(root,"viewer-points.json"));
        Assert.False((await new RpgShellService(store,Config(),dice).OpenAsync("1001","SYNTHETIC","bad",count,default)).Success);
        Assert.Equal(0,dice.Calls);
        Assert.Equal(before,await File.ReadAllBytesAsync(Path.Combine(root,"viewer-points.json")));
    }
    [Fact]
    public async Task Balance_limit_and_overflow_never_consume_stock()
    {
        var store=await Seed(1,100);
        Assert.False((await new RpgShellService(store,Config(),new Fixed(7),maximumBalance:105).OpenAsync("1001","SYNTHETIC","cap",1,default)).Success);
        await store.ApplyRpgAsync("1001","SYNTHETIC","overflow-seed",a=>{a.Rpg.Materials.WhitePearls=int.MaxValue;return new(true);},default);
        var before=await File.ReadAllBytesAsync(Path.Combine(root,"viewer-points.json"));
        await Assert.ThrowsAsync<OverflowException>(()=>new RpgShellService(store,Config(),new Fixed(1)).OpenAsync("1001","SYNTHETIC","overflow",1,default));
        Assert.Equal(before,await File.ReadAllBytesAsync(Path.Combine(root,"viewer-points.json")));
    }
    [Fact]
    public async Task Fishing_loss_awards_one_shell_without_changing_stake_rules_or_replaying()
    {
        var store=await Seed(0,1000);
        var config=new MinigameConfig{Enabled=true,PointsEnabled=true};
        config.Adventures.Rpg.Enabled=true;
        var fish=config.Adventures.Fishing;
        fish.Enabled=true;fish.MinimumBet=1;fish.MaximumBet=10000;
        fish.LossChance=100;fish.SmallChance=0;fish.LargeChance=0;fish.RareChance=0;
        var service=new RpgAdventureService(store,config,new Fixed(1));
        var caught=await service.ExecuteAsync("1001","SYNTHETIC","fish",RpgActivity.Fishing,10,default);
        Assert.True(caught.Success);Assert.Equal(1,caught.ShellsAwarded);
        Assert.Contains("Muschel",RpgChatPresentation.AdventureOutcome(caught,RpgActivity.Fishing));
        var account=(await Store().GetProfileAsync("1001",default)).Entry;
        Assert.Equal(1000,account.Points);Assert.Equal(1,account.Rpg.Materials.Shells);
        Assert.False((await service.ExecuteAsync("1001","SYNTHETIC","fish",RpgActivity.Fishing,10,default)).Success);
        Assert.Equal(1,(await store.GetProfileAsync("1001",default)).Entry.Rpg.Materials.Shells);
    }
    [Fact]
    public async Task Another_user_cannot_spend_the_owners_shells()
    {
        var store=await Seed(10);
        Assert.False((await new RpgShellService(store,Config(),new Fixed(1)).OpenAsync("1002","OTHER","open",10,default)).Success);
        Assert.Equal(10,(await store.GetProfileAsync("1001",default)).Entry.Rpg.Materials.Shells);
    }
    [Fact]
    public async Task Incomplete_material_save_fails_closed_without_resetting_or_rewriting_resources()
    {
        await Seed(10);
        var path=Path.Combine(root,"viewer-points.json");
        var node=System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        ((System.Text.Json.Nodes.JsonObject)node["1001"]!["Rpg"]!["Materials"]!).Remove("WhitePearls");
        await File.WriteAllTextAsync(path,node.ToJsonString());
        var before=await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAnyAsync<Exception>(()=>Store().GetProfileAsync("1001",default));
        Assert.Equal(before,await File.ReadAllBytesAsync(path));
    }
    [Fact]
    public async Task Chat_alias_consumes_stock_once_and_disabled_rpg_and_denied_users_cannot_open()
    {
        var store=await Seed(10);var config=Config();var chat=new RpgChatCommand(store,config);
        var user=new ChatMessage{Id="synthetic-chat-message",UserId="1001",UserName="SYNTHETIC"};
        Assert.Contains("Muscheln: 10",await chat.ExecuteAsync(user,"!muscheln",["!muscheln"],default));
        Assert.Contains("10 Muscheln geöffnet",await chat.ExecuteAsync(user,"!shells",["!shells","10"],default));
        var before=await store.ReadCommittedFileAsync(default);
        await chat.ExecuteAsync(user,"!muscheln",["!muscheln","10"],default);
        Assert.Equal(before,await store.ReadCommittedFileAsync(default));
        config.Enabled=false;
        Assert.Contains("deaktiviert",await chat.ExecuteAsync(user,"!muscheln",["!muscheln","1"],default));
        config.Enabled=true;user.CommandAuthorization=CommandAuthorization.Denied;
        Assert.Contains("nicht erlaubt",await chat.ExecuteAsync(user,"!muscheln",["!muscheln","1"],default));
        Assert.Equal(before,await store.ReadCommittedFileAsync(default));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
