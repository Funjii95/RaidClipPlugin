using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using SnapshotProtocol;
using Xunit;
namespace RaidClipPlugin.Tests;
public sealed class AdminControlCenterTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"otter-admin-center-"+Guid.NewGuid().ToString("N"));
    private readonly ViewerPointStore points;private readonly MinigameConfig config=new(){Enabled=true,PointsEnabled=true};
    private readonly StreamAdmission admission=new();private readonly Guid epoch;
    public AdminControlCenterTests(){points=new(root);config.Adventures.Rpg.Enabled=true;config.Adventures.Rpg.AdminAccess.Moderators.Add("1001");epoch=admission.Begin(()=>true);}
    private RemoteRpgExecutor Executor()=>new(points,()=>config,admission,epoch,"42",new HashSet<string>{"42","1001"},Path.Combine(root,"receipts"),(_,_)=>true);
    private ActionEnvelope Envelope(string kind,object args,string user="42"){
        var intent=new RpgIntent(Guid.NewGuid(),kind,JsonSerializer.SerializeToElement(args));
        return new("42",user,Guid.NewGuid(),"",DateTimeOffset.UtcNow.AddSeconds(8),intent,RpgIntents.Hash(intent));
    }
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    [Fact]public async Task OwnerGrantsOnlyMissingSetPiecesAndModeratorCannotCreateThem()
    {
        await points.SetPointsAsync("1001","Test",100,0,default);
        var args=new{setId="eternal_cosmic_forge",targetUserId="1001"};
        Assert.False((await Executor().ExecuteAsync(Envelope("adminGrantSet",args,"1001"),default)).Success);
        Assert.Empty((await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory);
        var e=Envelope("adminGrantSet",args);Assert.True((await Executor().ExecuteAsync(e,default)).Success);
        var before=(await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory.Select(i=>i.InstanceId).ToArray();Assert.Equal(8,before.Length);
        Assert.True((await Executor().ExecuteAsync(Envelope("adminGrantSet",args),default)).Success);
        Assert.Equal(before,(await points.GetProfileAsync("1001",default)).Entry.Rpg.Inventory.Select(i=>i.InstanceId));
        Assert.Equal(100,(await points.GetProfileAsync("1001",default)).Entry.Points);
    }
    [Fact]public void ModeratorPolicyDenyListProtectsItemsDivineRolesAndConfiguration()
    {
        foreach(var kind in new[]{"adminGrant","adminGrantSet","adminMaterialGrant","adminDivine","adminConfigRead","adminConfigSave","adminRoles"})
            Assert.False(AdminPermissions.ModeratorMay(new(Guid.NewGuid(),kind,JsonSerializer.SerializeToElement(new{}))));
        Assert.True(AdminPermissions.ModeratorMay(new(Guid.NewGuid(),"adminHealAll",JsonSerializer.SerializeToElement(new{}))));
        Assert.False(AdminPermissions.IsModerator("{\"isModerator\":true}","1001"));
        Assert.True(AdminPermissions.IsModerator("{\"staffAccess\":{\"moderators\":[\"1001\"]}}","1001"));
    }
    [Fact]public async Task ReadConfigurationCanonicalValidationAcceptsPageAfterParsingArguments()
    {
        var outcome=await Executor().ExecuteAsync(Envelope("adminConfigRead",new{section="zones",filter="EternalEightChances",page="0"}),default);
        Assert.True(outcome.Success);using var doc=JsonDocument.Parse(outcome.DataJson!);
        Assert.Equal(4,doc.RootElement.GetProperty("total").GetInt32());
    }
    [Fact]public void ConfigurationCannotWriteSecretsOrUseStaleVersions()
    {
        var app=new AppConfig();var hash=RpgAdminConfiguration.Version(app,"zones");
        Assert.ThrowsAny<Exception>(()=>RpgAdminConfiguration.Apply(app,"zones",hash,[new("Twitch.ClientSecret","changed")]));
        var field=RpgAdminConfiguration.Fields(app,"zones").First(f=>f.Path.EndsWith("EternalEightChances.normal"));
        Assert.ThrowsAny<Exception>(()=>RpgAdminConfiguration.Apply(app,"zones","stale",[new(field.Path,"1")]));
        Assert.ThrowsAny<Exception>(()=>RpgAdminConfiguration.Apply(app,"zones",hash,[new(field.Path,"101")]));
        Assert.Equal(.01m,app.Minigame.Adventures.Rpg.Zones.EternalEightChances["normal"]);
    }
}
