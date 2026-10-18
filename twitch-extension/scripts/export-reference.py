"""Export pure RPG defaults/sample rolls from the pinned release; never reads player files.
Requires Python and .NET 10 for maintainers only. The extension consumes checked-in JSON.
"""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
COMMIT = '48ac7ad1445285a1b560257ac33e2aa5f9d7ce77'
DEST = ROOT / '.reference-export'
DEST.mkdir(exist_ok=True)
files = ['Config/'+name+'.cs' for name in ['RpgConfig','RpgLootConfig','RpgHealthConfig','RpgEnemyConfig','RpgHotTimeConfig','RpgSalvageConfig','AdventureGamesConfig']]
files += ['Models/'+name+'.cs' for name in ['RpgProfile','RpgItemSet','RpgItemEffect']]
files += ['Services/'+name+'.cs' for name in ['RpgEquipmentCatalog','RpgHealthEquipmentCatalog','RpgLootExpansionCatalog','RpgCoreStatService','RpgItemGenerationService']]
def source(path):
    return subprocess.check_output(['git','show',COMMIT+':RaidClipPlugin/'+path], cwd=ROOT, encoding='utf-8')
for path in files:
    (DEST / Path(path).name).write_text(source(path),encoding='utf-8')
sushi = source('Services/RpgSushiService.cs')
def method(signature):
    start = sushi.index(signature)
    brace = sushi.index('{',start)
    depth=1; end=brace+1
    while depth:
        if sushi[end]=='{': depth+=1
        if sushi[end]=='}': depth-=1
        end+=1
    return sushi[start:end]
# These are the exact pure default/validation methods; no store or live integration.
(DEST/'RpgSushiService.cs').write_text('using RaidClipPlugin.Config; using RaidClipPlugin.Models; namespace RaidClipPlugin.Services; public static class RpgSushiService {\n'+method('public static void AddMissing(')+'\n'+method('public static void Validate(')+'\npublic static IReadOnlyDictionary<string,int> Ingredients(RpgRecipe recipe) => recipe.Ingredients.Count>0 ? recipe.Ingredients : new Dictionary<string,int>{{recipe.FishId,recipe.FishQuantity}};\n}',encoding='utf-8')
(DEST/'Export.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>',encoding='utf-8')
(DEST/'Program.cs').write_text(r'''
using System.Text.Json;
using System.Text.Json.Serialization;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;
var config = new RpgConfig(); config.Validate();
var options = new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase, DictionaryKeyPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true };
options.Converters.Add(new JsonStringEnumConverter());
var samples=new List<RpgItem>(); var rng=new SampleRandom();
foreach(var definition in config.DiveLoot.Items) {
 definition.InstanceId="definition-"+definition.ItemId;
 var item=JsonSerializer.Deserialize<RpgItem>(JsonSerializer.Serialize(definition))!;
 RpgItemGenerationService.Generate(item,config.DiveLoot,rng);
 item.InstanceId="demo-"+item.ItemId;
 samples.Add(item);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new {source=new {tag="v2.1.21",commit="48ac7ad1445285a1b560257ac33e2aa5f9d7ce77",mainAtAudit="5f8a92305dc8c48b3849bb97b58c9bc0bcaf12d6",sampleSeed=221},config,adventures=new {fishing=new SoloAdventureConfig(),treasure=new DiveAdventureConfig(),boss=new BossAdventureConfig()},samples},options));
namespace RaidClipPlugin.Services {
 public interface IDuelRandom { int NextInclusive(int min,int max); }
 public sealed class SampleRandom : IDuelRandom {private readonly Random rng=new(221);public int NextInclusive(int min,int max)=>rng.Next(min,max+1);}
}
''',encoding='utf-8')
subprocess.run(['dotnet','run','--project',str(DEST/'Export.csproj'),'--',str(ROOT/'src/reference/v2.1.21.json')],check=True,cwd=ROOT)
print('Exported defaults and deterministic samples:',ROOT/'src/reference/v2.1.21.json')
