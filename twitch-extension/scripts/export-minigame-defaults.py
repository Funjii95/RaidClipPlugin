"""Snapshot scalar minigame defaults from the same pinned release as the RPG reference."""
import json,re,subprocess
from pathlib import Path
commit='48ac7ad1445285a1b560257ac33e2aa5f9d7ce77'
def read(name):
    text=subprocess.check_output(['git','show',f'{commit}:RaidClipPlugin/Config/{name}.cs'],encoding='utf-8')
    if name=="AdventureGamesConfig":
        base=text.split("public class StakeAdventureConfig",1)[1].split("public class SoloAdventureConfig",1)[0]
        race=text.split("public sealed class RaceAdventureConfig",1)[1].split("public sealed class BossAdventureConfig",1)[0]
        text=base+race
    if name=="AppConfig": text=text.split("public class MinigameConfig",1)[1].split("public class GambleRangeConfig",1)[0]
    if name=="HeistConfig": text=text.split("public sealed class CommandsConfig",1)[0]
    result={}
    for key,raw in re.findall(r'public (?:bool|int|long|decimal|string) (\w+) \{ get; set; \}\s*=\s*([^;]+);',text):
        try: result[key]=json.loads(re.sub(r'(?<=\d)m$','',raw))
        except (ValueError,TypeError): pass
    if name=="AppConfig":
        result["GambleRanges"]=[{"from":int(a),"to":int(b),"multiplier":float(c)} for a,b,c in re.findall(r"From = (\d+),\s+To = (\d+),\s+Multiplier = ([\d.]+)m",text)]
    return result
Path('src/minigames/defaults.json').write_text(json.dumps({'source':{'tag':'v2.1.21','commit':commit},'minigame':read('AppConfig'),'heist':read('HeistConfig'),'duel':read('DuelConfig'),'race':read('AdventureGamesConfig')},ensure_ascii=False,indent=2),encoding='utf-8')
