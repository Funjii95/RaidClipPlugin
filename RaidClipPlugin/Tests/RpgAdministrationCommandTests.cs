using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;
public sealed class RpgAdministrationCommandTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-admin-command",Guid.NewGuid().ToString("N"));
    private DateTimeOffset now=DateTimeOffset.UtcNow;
    private readonly List<string> messages=[];
    private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
    private sealed class Low:IDuelRandom{public int NextInclusive(int min,int max)=>min;}
    private ViewerPointStore Store()=>new(root);
    private AdventureGameService Game(ViewerPointStore store)=>new(store,config,(m,_)=>{messages.Add(m);return Task.CompletedTask;},new Low(),()=>now,"host");
    public RpgAdministrationCommandTests(){
        config.CurrencyPlural="Perlen";config.Adventures.Rpg.Enabled=true;config.Adventures.Rpg.Health.Enabled=true;
        config.Adventures.Rpg.BossLootChance=0;config.Adventures.Boss.HitPoints=100;
        config.Adventures.Boss.RewardPool=100;config.MaximumAccountEnabled=true;config.MaximumAccountPoints=1050;
    }
    private Task Seed(ViewerPointStore store,string id,string name,int hp,long pearls=1000)=>store.ApplyRpgAsync(id,name,Guid.NewGuid().ToString("N"),a=>{
        a.Points=pearls;RpgHealthService.Refresh(a.Rpg,config.Adventures.Rpg.Health,now);a.Rpg.Health.CurrentHp=hp;return new(true);
    },default);
    private Task Send(AdventureGameService game,string command,string target,bool mod=true,string? id=null)=>
        game.HandleAsync(new(){UserId=mod?"host":"viewer",UserName="HOST",IsModerator=mod,Id=id??Guid.NewGuid().ToString("N")},command,[command,target],default);
    [Fact]public async Task Heal_target_and_all_preserve_points_cooldowns_and_boss_defeat_flags(){
        var s=Store();await Seed(s,"1001","Alpha",10);await Seed(s,"1002","Beta",20);var g=Game(s);
        await Send(g,"!heal","@Alpha",false);Assert.Equal(10,(await s.GetProfileAsync("1001",default)).Entry.Rpg.Health.CurrentHp);
        await Send(g,"!heal","@ALPHA",id:"heal-once");
        Assert.Equal(100,(await s.GetProfileAsync("1001",default)).Entry.Rpg.Health.CurrentHp);
        Assert.Equal(20,(await s.GetProfileAsync("1002",default)).Entry.Rpg.Health.CurrentHp);
        await s.ApplyRpgAsync("1001","Alpha","new-damage",a=>{a.Rpg.Health.CurrentHp=5;return new(true);},default);
        await Send(g,"!heal","@Alpha",id:"heal-once");
        Assert.Equal(5,(await s.GetProfileAsync("1001",default)).Entry.Rpg.Health.CurrentHp);
        await s.ApplyRpgAsync("1002","Beta","defeat",a=>{a.Rpg.Health.Defeated=true;a.Rpg.Health.BossId="old";a.Rpg.Cooldowns["!attack"]=now.AddMinutes(1);return new(true);},default);
        await Send(g,"!heal","all");
        var restored=Store();
        foreach(var user in new[]{"1001","1002"}){
            var a=(await restored.GetProfileAsync(user,default)).Entry;
            Assert.Equal(100,a.Rpg.Health.CurrentHp);Assert.Equal(1000,a.Points);
        }
        var beta=(await restored.GetProfileAsync("1002",default)).Entry.Rpg;
        Assert.True(beta.Health.Defeated);Assert.Equal(now.AddMinutes(1),beta.Cooldowns["!attack"]);
        await Send(g,"!heal","@unknown");Assert.Equal(2,await s.CountAsync(default));
    }
    [Fact]public async Task Kill_uses_regular_rewards_hot_time_and_actual_capped_credit_without_double_payout(){
        var s=Store();await Seed(s,"1001","Alpha",100,1040);await Seed(s,"1002","Beta",100);var g=Game(s);
        await g.ControlAsync("boss",0,default);
        Assert.True((await g.ExecuteBossAttackAsync("1001","Alpha","hit-a",default)).Success);
        Assert.True((await g.ExecuteBossAttackAsync("1002","Beta","hit-b",default)).Success);
        await Send(g,"!kill","boss",false);Assert.True(g.Status.BossActive);
        await Send(g,"!kill","boss");
        Assert.False(g.Status.BossActive);
        Assert.Equal(1050,await s.GetPointsAsync("1001",default));Assert.Equal(1050,await s.GetPointsAsync("1002",default));
        Assert.Contains(messages,m=>m.Contains("@Alpha erhält 10 Perlen"));
        Assert.Contains(messages,m=>m.Contains("@Beta erhält 50 Perlen"));
        Assert.True(s.HotTime.Status.Active);var eventId=s.HotTime.Status.Event!.EventId;
        var rewards=messages.Count(m=>m.Contains("Boss-Belohnung"));
        await Send(g,"!kill","boss");
        Assert.Equal(rewards,messages.Count(m=>m.Contains("Boss-Belohnung")));
        Assert.Equal(eventId,s.HotTime.Status.Event!.EventId);Assert.Equal(2,await s.CountAsync(default));
    }
    [Fact]public async Task Kill_without_participants_or_with_invalid_syntax_cannot_generate_rewards(){
        var s=Store();var g=Game(s);await g.ControlAsync("boss",0,default);
        await Send(g,"!kill","boss");Assert.True(g.Status.BossActive);Assert.False(s.HotTime.Status.Active);
        await Send(g,"!kill","all");Assert.True(g.Status.BossActive);
    }
    [Fact]public void Administrative_commands_cannot_be_downgraded_to_viewer_role(){
        var app=new AppConfig{Minigame=config};app.Commands.CommandRoleOverrides["admin.rpg.heal"]="Viewer";
        app.Commands.CommandRoleOverrides["admin.boss.kill"]="Viewer";
        var registry=new CommandRegistry();registry.Update(app);
        Assert.All(registry.Commands.Where(c=>c.CommandId is "admin.rpg.heal" or "admin.boss.kill"),c=>Assert.Equal(CommandRole.Moderator,c.RequiredRole));
    }
    [Fact]public async Task Dynamic_boss_adds_hp_once_per_successful_new_participant_and_recovers_damage(){
        config.Adventures.Boss.HpMode=BossHpMode.Dynamic;
        var s=Store();await Seed(s,"1001","Alpha",100);await Seed(s,"1002","Beta",100);
        var g=Game(s);await g.ControlAsync("boss",0,default);
        Assert.Equal(100,g.Status.BossMaxHp);Assert.Equal(100,g.Status.BossHp);
        Assert.True((await g.ExecuteBossAttackAsync("1001","Alpha","hit-a",default)).Success);
        Assert.Equal(90,g.Status.BossHp);Assert.Equal(100,g.Status.BossMaxHp);
        Assert.False((await g.ExecuteBossAttackAsync("1001","Alpha","hit-a-again",default)).Success);
        Assert.Equal(90,g.Status.BossHp);
        Assert.True((await g.ExecuteBossAttackAsync("1002","Beta","hit-b",default)).Success);
        Assert.Equal(180,g.Status.BossHp);Assert.Equal(200,g.Status.BossMaxHp);
        var restored=Game(Store());await restored.TickAsync(default);
        Assert.Equal(g.Status.BossEventId,restored.Status.BossEventId);
        Assert.Equal(180,restored.Status.BossHp);Assert.Equal(200,restored.Status.BossMaxHp);
        Assert.False((await restored.ExecuteBossAttackAsync("1002","Beta","hit-b",default)).Success);
        Assert.Equal(180,restored.Status.BossHp);
        await Seed(s,"1003","Gamma",0);
        Assert.False((await g.ExecuteBossAttackAsync("1003","Gamma","dead-join",default)).Success);
        Assert.Equal(180,g.Status.BossHp);Assert.Equal(200,g.Status.BossMaxHp);
    }
    [Fact]public async Task Dynamic_boss_failed_save_does_not_add_hp_or_a_participant(){
        config.Adventures.Boss.HpMode=BossHpMode.Dynamic;
        var s=Store();await Seed(s,"1001","Alpha",100);await Seed(s,"1002","Beta",100);
        var g=Game(s);await g.ControlAsync("boss",0,default);
        await g.ExecuteBossAttackAsync("1001","Alpha","first",default);
        var blocker=Path.Combine(root,"viewer-points.json.tmp");Directory.CreateDirectory(blocker);
        await Assert.ThrowsAnyAsync<Exception>(()=>g.ExecuteBossAttackAsync("1002","Beta","failed",default));
        Assert.Equal(90,g.Status.BossHp);Assert.Equal(100,g.Status.BossMaxHp);
        Assert.Equal(1,g.Status.BossPlayers);
    }
    [Fact]public void Hp_mode_dropdown_has_German_labels_and_persists_both_choices(){
        var property=System.ComponentModel.TypeDescriptor.GetProperties(typeof(BossAdventureConfig))[nameof(BossAdventureConfig.HpMode)]!;
        Assert.Equal("HP-Modus",property.DisplayName);
        var converter=property.Converter;
        Assert.True(converter.GetStandardValuesExclusive());
        Assert.Equal("Dynamisch",converter.ConvertToString(BossHpMode.Dynamic));
        Assert.Equal("Feste HP",converter.ConvertToString(BossHpMode.Fixed));
        Assert.Equal(BossHpMode.Dynamic,converter.ConvertFromString("Dynamisch"));
        Assert.Equal(BossHpMode.Fixed,converter.ConvertFromString("Feste HP"));
        Assert.Equal(BossHpMode.Fixed,System.Text.Json.JsonSerializer.Deserialize<BossAdventureConfig>("{\"HitPoints\":750}")!.HpMode);
        foreach(var mode in new[]{BossHpMode.Fixed,BossHpMode.Dynamic}){
            var original=new BossAdventureConfig{HpMode=mode,HitPoints=750};
            var restored=System.Text.Json.JsonSerializer.Deserialize<BossAdventureConfig>(System.Text.Json.JsonSerializer.Serialize(original))!;
            Assert.Equal(mode,restored.HpMode);Assert.Equal(750,restored.HitPoints);
        }
    }
    [Fact]public async Task Hp_mode_change_applies_only_to_next_round_and_preserves_fixed_hp(){
        config.Adventures.Boss.HitPoints=750;config.Adventures.Boss.RoundCooldownSeconds=0;
        var g=Game(Store());await g.ControlAsync("boss",0,default);
        Assert.Equal(750,g.Status.BossMaxHp);
        config.Adventures.Boss.HpMode=BossHpMode.Dynamic;
        Assert.Equal(750,g.Status.BossMaxHp);
        await g.ControlAsync("cancelboss",0,default);now=now.AddSeconds(2);
        await g.ControlAsync("boss",0,default);
        Assert.Equal(100,g.Status.BossMaxHp);Assert.Equal(100,g.Status.BossHp);
        config.Adventures.Boss.HpMode=BossHpMode.Fixed;
        Assert.Equal(100,g.Status.BossMaxHp);
        await g.ControlAsync("cancelboss",0,default);now=now.AddSeconds(2);
        await g.ControlAsync("boss",0,default);
        Assert.Equal(750,g.Status.BossMaxHp);
    }
    [Theory]
    [InlineData(10,true)]
    [InlineData(1000000,false)]
    public async Task Simulation_finishes_without_mutating_real_profiles_boss_or_hot_time(int hp,bool victory)
    {
        var store=Store();await Seed(store,"host","HOST",65);
        config.Adventures.Boss.HpMode=BossHpMode.Fixed;config.Adventures.Boss.HitPoints=hp;config.Adventures.Boss.DurationSeconds=10;
        var game=Game(store);await game.ControlAsync("boss",0,default);
        var status=game.Status;
        var before=File.ReadAllBytes(Path.Combine(root,"viewer-points.json"));
        var boss=File.ReadAllBytes(Path.Combine(root,"rpg-boss-round.json"));
        await game.HandleAsync(new(){UserId="host",UserName="HOST",Id="simulation"},"!sim",["!sim"],default);
        Assert.Contains(messages,m=>m.StartsWith(victory?"[SIM] Sieg":"[SIM] Niederlage"));
        Assert.Equal(before,File.ReadAllBytes(Path.Combine(root,"viewer-points.json")));
        Assert.Equal(boss,File.ReadAllBytes(Path.Combine(root,"rpg-boss-round.json")));
        Assert.Equal(status,game.Status);Assert.False(store.HotTime.Status.Active);
        Assert.True(messages.Count(m=>m.StartsWith("[SIM]"))<=10);
    }
    [Fact]public async Task Simulation_rejects_moderators_viewers_and_spoofed_broadcaster_badge()
    {
        var game=Game(Store());
        foreach(var actor in new[]{new ChatMessage{UserId="mod",IsModerator=true},new ChatMessage{UserId="viewer"},new ChatMessage{UserId="viewer",IsBroadcaster=true}})
            await game.HandleAsync(actor,"!sim",["!sim"],default);
        Assert.Equal(3,messages.Count);Assert.All(messages,m=>Assert.Contains("Nur der Broadcaster",m));
        Assert.False(File.Exists(Path.Combine(root,"viewer-points.json")));
    }
    [Fact]public async Task Simulation_validates_syntax_and_limits_repeated_calls()
    {
        var game=Game(Store());config.Adventures.Boss.HitPoints=10;
        var actor=new ChatMessage{UserId="host",UserName="HOST"};
        await game.HandleAsync(actor,"!sim",["!sim","boss"],default);
        Assert.Contains("Nutzung: !sim",messages.Last());
        await game.HandleAsync(actor,"!sim",["!sim"],default);
        await game.HandleAsync(actor,"!sim",["!sim"],default);
        Assert.Contains("60 Sekunden",messages.Last());
    }
    [Fact]public void Simulation_registry_cannot_be_downgraded_even_to_moderator()
    {
        var app=new AppConfig{Minigame=config};app.Commands.CommandRoleOverrides["admin.boss.sim"]="Moderator";
        var registry=new CommandRegistry();registry.Update(app);
        Assert.Equal(CommandRole.Broadcaster,Assert.Single(registry.Commands,c=>c.CommandText=="!sim").RequiredRole);
        Assert.True(AdventureGameService.Recognizes("!sim"));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}

