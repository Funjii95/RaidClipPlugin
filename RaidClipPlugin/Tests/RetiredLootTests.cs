using RaidClipPlugin.Config;
using Xunit;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
namespace RaidClipPlugin.Tests;
public sealed class RetiredLootTests
{
 [Fact] public void Saved_pool_removes_old_shell_without_removing_other_collectibles()
 {
  var config=new RpgLootConfig();
  config.Items.Add(new(){ItemId="shell",DisplayName="Alte Muschel"});
  config.Items.Add(new(){ItemId="old-shell",DisplayName="Legacy shell"});
  var collectibles=config.Items.Count(i=>i.Collector is not null);
  config.Validate();config.Validate();
  Assert.DoesNotContain(config.Items,RpgLootConfig.IsRetiredDrop);
  Assert.Equal(collectibles,config.Items.Count(i=>i.Collector is not null));
 }
 [Fact] public void Every_quest_item_has_a_unique_name_and_existing_catalog_stats_stay_intact()
 {
  var config=new RpgConfig();var item=config.DiveLoot.Items.First(i=>i.ItemId=="quest_abyss_0");
  item.Attributes.Power=123;item.DisplayName="quest_abyss_0";
  RpgQuestCatalog.AddMissing(config);
  Assert.Equal("Krone des bezwungenen Abgrunds",item.DisplayName);
  Assert.Equal(123,item.Attributes.Power);
  var items=RpgQuestCatalog.Items();Assert.Equal(80,items.Select(i=>i.DisplayName).Distinct().Count());
 }
}
