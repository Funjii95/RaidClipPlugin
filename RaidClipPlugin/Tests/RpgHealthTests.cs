using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class RpgHealthTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"RaidClipHealth-"+Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now=DateTimeOffset.UtcNow;
    private sealed class Low:IDuelRandom {public int NextInclusive(int min,int max)=>min;}
    private static MinigameConfig Config()
    {
        var c=new MinigameConfig{Enabled=true,PointsEnabled=true};c.Adventures.Rpg.Enabled=true;c.Adventures.Rpg.Upgrades.EquipmentDropChances.Keys.ToList().ForEach(k=>c.Adventures.Rpg.Upgrades.EquipmentDropChances[k]=0);
        c.Adventures.Boss.HitPoints=200;c.Adventures.Boss.RewardPool=100;c.Adventures.Boss.AttackCooldownSeconds=1;
        c.Adventures.Rpg.BossLootChance=100;return c;
    }
    private AdventureGameService Game(ViewerPointStore s,MinigameConfig c)=>new(s,c,(_,_)=>Task.CompletedTask,new Low(),()=>_now,"b");
    private async Task Start(AdventureGameService game)=>await game.HandleAsync(new(){UserId="b",IsBroadcaster=true},"!start boss",new[]{"!start boss"},default);
    private async Task Seed(ViewerPointStore s,string id,int hp)
    {
        await s.ApplyRpgAsync(id,id,Guid.NewGuid().ToString("N"),a=>
        {RpgHealthService.Refresh(a.Rpg,new(),_now);a.Rpg.Health.CurrentHp=hp;return new(true);},default);
    }
    public void Dispose(){if(Directory.Exists(_dir))Directory.Delete(_dir,true);}

    [Theory]
    [InlineData(1,100)] [InlineData(10,118)] [InlineData(25,148)] [InlineData(50,198)] [InlineData(100,298)]
    public void LegacyInitializationAndMaximum(int level,int expected)
    {
        var p=new RpgProfile{PlayerLevel=level};RpgHealthService.Refresh(p,new(),_now);
        Assert.Equal(expected,p.Health.CurrentHp);Assert.Equal(expected,RpgHealthService.Maximum(p,new()));
    }
    [Fact]
    public void LevelGainHealsOnlyNewMaximumAndRegenKeepsRemainder()
    {
        var p=new RpgProfile();var c=new RpgHealthConfig();RpgHealthService.Refresh(p,c,_now);p.Health.CurrentHp=50;
        RpgHealthService.Experience(p,c,1000,_now);Assert.Equal(2,p.PlayerLevel);Assert.Equal(52,p.Health.CurrentHp);
        RpgHealthService.Refresh(p,c,_now.AddSeconds(119));Assert.Equal(54,p.Health.CurrentHp);
        RpgHealthService.Refresh(p,c,_now.AddSeconds(120));Assert.Equal(56,p.Health.CurrentHp);
        RpgHealthService.Refresh(p,c,_now.AddYears(1));Assert.Equal(102,p.Health.CurrentHp);
    }
    [Fact]
    public async Task RestartKeepsWoundsAndAppliesOnlyElapsedRegen()
    {
        var s=new ViewerPointStore(_dir);await Seed(s,"u",50);
        s=new(_dir);var p=await RpgHealthService.ReadAsync(s,"u","U",new(),_now.AddMinutes(5),default);
        Assert.Equal(60,p.Health.CurrentHp);
        p=await RpgHealthService.ReadAsync(new(_dir),"u","U",new(),_now.AddMinutes(5),default);
        Assert.Equal(60,p.Health.CurrentHp);
    }
    [Theory]
    [InlineData("hello",false,60)] [InlineData("!unknown",false,50)] [InlineData("  !hp",false,50)] [InlineData("hello",true,50)]
    public async Task ChatExcludesEveryCommandAndBot(string text,bool bot,int expected)
    {
        var s=new ViewerPointStore(_dir);await Seed(s,"u",50);var c=new RpgConfig{Enabled=true};
        await RpgHealthService.HealChatAsync(s,c,new(){UserId="u",Id="msg",Text=text,IsBot=bot},_now,default);
        Assert.Equal(expected,(await s.GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
    }
    [Fact]
    public async Task ChatCooldownIsPersonalPersistentAndDuplicateSafe()
    {
        var s=new ViewerPointStore(_dir);await Seed(s,"u",50);await Seed(s,"v",50);var c=new RpgConfig{Enabled=true};c.Health.PassiveHealing=false;
        ChatMessage Msg(string who,string id)=>new(){UserId=who,Id=id,Text="hello"};
        await RpgHealthService.HealChatAsync(s,c,Msg("u","a"),_now,default);
        await RpgHealthService.HealChatAsync(s,c,Msg("u","b"),_now.AddSeconds(15),default);
        await RpgHealthService.HealChatAsync(s,c,Msg("v","c"),_now.AddSeconds(15),default);
        s=new(_dir);
        await RpgHealthService.HealChatAsync(s,c,Msg("u","a"),_now.AddSeconds(30),default);
        Assert.Equal(60,(await s.GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
        await Task.WhenAll(Enumerable.Range(0,4).Select(_=>RpgHealthService.HealChatAsync(s,c,Msg("u","d"),_now.AddSeconds(30),default)));
        Assert.Equal(70,(await s.GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
        Assert.Equal(60,(await s.GetProfileAsync("v",default)).Entry.Rpg.Health.CurrentHp);
    }
    [Fact]
    public async Task CounterattackDeathHealingAndRestartCannotBypassBossLock()
    {
        var s=new ViewerPointStore(_dir);var c=Config();var game=Game(s,c);await Seed(s,"u",10);await Start(game);
        var hit=await game.ExecuteBossAttackAsync("u","U","one",default);Assert.True(hit.Success);
        var p=(await s.GetProfileAsync("u",default)).Entry.Rpg;Assert.Equal(0,p.Health.CurrentHp);Assert.True(p.Health.Defeated);
        _now=_now.AddSeconds(2);
        await RpgHealthService.HealChatAsync(s,c.Adventures.Rpg,new(){UserId="u",Id="heal",Text="hello"},_now,default);
        Assert.Equal(10,(await s.GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
        game=Game(new(_dir),c);Assert.False((await game.ExecuteBossAttackAsync("u","U","two",default)).Success);
        Assert.Equal(190,game.Status.BossHp);
    }
    [Fact]
    public async Task SurvivorsReceiveAtomicRewardsTopBonusAndDeadReceiveNothing()
    {
        var s=new ViewerPointStore(_dir);var c=Config();c.Adventures.Boss.HitPoints=30;
        var game=Game(s,c);await Seed(s,"dead",10);await Start(game);
        await game.ExecuteBossAttackAsync("dead","DEAD","d",default);
        await game.ExecuteBossAttackAsync("live","LIVE","l",default);_now=_now.AddSeconds(2);
        await game.ExecuteBossAttackAsync("live","LIVE","last",default);
        var dead=(await s.GetProfileAsync("dead",default)).Entry;var live=(await s.GetProfileAsync("live",default)).Entry;
        Assert.Equal(0,dead.Points);Assert.Equal(0,dead.Rpg.Experience);Assert.Empty(dead.Rpg.Inventory);
        Assert.Equal(100,live.Points);Assert.Equal(1200,live.Rpg.Experience);Assert.Equal(2,live.Rpg.Inventory.Count);Assert.Single(live.Rpg.Inventory,i=>i.Rarity==ItemRarity.Legendary);
        // Last hit kills the boss: only one counterattack against the survivor.
        Assert.Equal(92,live.Rpg.Health.CurrentHp); // 90 HP +2 on level-up.
        game=Game(new(_dir),c);await game.TickAsync(default);
        Assert.Equal(100,(await s.GetProfileAsync("live",default)).Entry.Points);
    }
    [Fact]
    public async Task DuplicateAttackCannotDamageTwiceAndTimeoutRewardsInactiveSurvivor()
    {
        var s=new ViewerPointStore(_dir);var c=Config();var game=Game(s,c);await Start(game);
        Assert.True((await game.ExecuteBossAttackAsync("u","U","same",default)).Success);
        _now=_now.AddSeconds(2);
        Assert.False((await game.ExecuteBossAttackAsync("u","U","same",default)).Success);
        Assert.Equal(190,game.Status.BossHp);Assert.Equal(90,(await s.GetProfileAsync("u",default)).Entry.Rpg.Health.CurrentHp);
        _now=_now.AddHours(1);await game.TickAsync(default);
        var p=(await s.GetProfileAsync("u",default)).Entry;
        Assert.Equal(100,p.Rpg.Experience);Assert.Equal(0,p.Points);
    }
    [Fact]
    public async Task SavingFailureRollsBackHpAndBossDamage()
    {
        var s=new ViewerPointStore(_dir);var c=Config();var game=Game(s,c);await Seed(s,"u",100);await Start(game);
        Directory.CreateDirectory(Path.Combine(_dir,"viewer-points.json.tmp"));
        await Assert.ThrowsAnyAsync<Exception>(()=>game.ExecuteBossAttackAsync("u","U","fail",default));
        var p=(await s.GetProfileAsync("u",default)).Entry.Rpg;
        Assert.Equal(100,p.Health.CurrentHp);Assert.Equal(0,p.Health.BossDamage);Assert.Equal(200,game.Status.BossHp);
        Assert.DoesNotContain("fail",p.CompletedActions);
    }
    [Fact]
    public async Task DeadTopDamagePlayerCannotReceiveTopExperience()
    {
        var s=new ViewerPointStore(_dir);var c=Config();c.Adventures.Boss.HitPoints=50;var game=Game(s,c);await Seed(s,"dead",30);await Start(game);
        for(int n=0;n<3;n++){await game.ExecuteBossAttackAsync("dead","D","d"+n,default);_now=_now.AddSeconds(2);}
        for(int n=0;n<2;n++){await game.ExecuteBossAttackAsync("live","L","l"+n,default);_now=_now.AddSeconds(2);}
        Assert.Equal(0,(await s.GetProfileAsync("dead",default)).Entry.Rpg.Experience);
        Assert.Equal(1200,(await s.GetProfileAsync("live",default)).Entry.Rpg.Experience);
    }
    [Fact]
    public void EnemyHookSharesHpAndAwardsExperienceOnlyToLivingVictor()
    {
        var p=new RpgProfile();var c=new RpgHealthConfig();
        RpgHealthService.ResolveEnemy(p,c,20,true,100,_now);Assert.Equal(80,p.Health.CurrentHp);Assert.Equal(100,p.Experience);
        RpgHealthService.ResolveEnemy(p,c,100,false,500,_now);Assert.Equal(0,p.Health.CurrentHp);Assert.Equal(100,p.Experience);
        Assert.Throws<InvalidOperationException>(()=>RpgHealthService.ResolveEnemy(p,c,10,true,100,_now));
    }
    [Fact]
    public async Task ZeroHpDiveDoesNotDebitStakeAndHpCommandIsRegistered()
    {
        var s=new ViewerPointStore(_dir);var c=Config();await Seed(s,"u",0);await s.AddPointsAsync("u","U",1000,0,default);
        var result=await new RpgAdventureService(s,c,new Low(),()=>_now).ExecuteAsync("u","U","dive",RpgActivity.Dive,250,default);
        Assert.False(result.Success);Assert.Equal(1000,await s.GetPointsAsync("u",default));
        Assert.True(RpgChatCommand.Recognizes("!hp"));
        var text=await new RpgChatCommand(s,c.Adventures.Rpg).ExecuteAsync(new(){UserId="u",UserName="U"},"!hp",new[]{"!hp"},default);
        Assert.Contains("HP: 0/100",text);
    }
}
