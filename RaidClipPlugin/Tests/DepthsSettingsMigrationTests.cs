using System.Text.Json.Nodes;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
using Xunit;
namespace RaidClipPlugin.Tests;
[CollectionDefinition("settings-migration",DisableParallelization=true)]
public sealed class SettingsMigrationCollection{}
[Collection("settings-migration")]
public sealed class DepthsSettingsMigrationTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void SavingOverRealLegacySettingsMigratesNewRaritiesWithoutAcceptingCorruption(bool corrupt)
    {
        Assert.True(TestDataPaths.IsTestBuild);
        var file=Path.Combine(TestDataPaths.Root,"settings.json");var backup=file+".bak";
        var before=File.Exists(file)?File.ReadAllBytes(file):null;var beforeBackup=File.Exists(backup)?File.ReadAllBytes(backup):null;
        try
        {
            if(File.Exists(file))File.Delete(file);
            var service=new ConfigurationService();var cfg=new AppConfig();cfg.Twitch.BroadcasterLogin="synthetic";cfg.OBS.Host="127.0.0.1";cfg.OBS.Port=4455;cfg.Minigame.Adventures.Rpg.Salvage.Values[ItemRarity.Common]=new(){Minimum=777,Maximum=888};
            service.SaveGuiSettings(cfg);
            var legacy=JsonNode.Parse(File.ReadAllText(file))!;var values=legacy["Minigame"]!["Adventures"]!["Rpg"]!["Salvage"]!["Values"]!.AsObject();
            foreach(var rarity in new[]{"True","Cursed","Abyss"})values.Remove(rarity);
            if(corrupt)values["Common"]!["Minimum"]=-1;
            File.WriteAllText(file,legacy.ToJsonString());var old=File.ReadAllBytes(file);
            if(corrupt){Assert.ThrowsAny<Exception>(()=>service.SaveGuiSettings(cfg));Assert.Equal(old,File.ReadAllBytes(file));}
            else{service.SaveGuiSettings(cfg);var restored=JsonNode.Parse(File.ReadAllText(file))!["Minigame"]!["Adventures"]!["Rpg"]!["Salvage"]!["Values"]!;
                Assert.Equal(777,restored["Common"]!["Minimum"]!.GetValue<int>());Assert.Equal(2500,restored["True"]!["Minimum"]!.GetValue<int>());Assert.Equal(4000,restored["Abyss"]!["Minimum"]!.GetValue<int>());Assert.Equal(old,File.ReadAllBytes(backup));}
        }
        finally {if(before is null){if(File.Exists(file))File.Delete(file);}else File.WriteAllBytes(file,before);if(beforeBackup is null){if(File.Exists(backup))File.Delete(backup);}else File.WriteAllBytes(backup,beforeBackup);}
    }
}
