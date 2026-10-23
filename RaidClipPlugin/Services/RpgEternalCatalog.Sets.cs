using RaidClipPlugin.Models;
using RaidClipPlugin.Config;
namespace RaidClipPlugin.Services;
public static partial class RpgEternalCatalog
{
    public static void AddMissing(RpgConfig c)
    {
        if(c.AreaBosses.SchemaVersion==0){c.AreaBosses.SchemaVersion=1;c.AreaBosses.Enabled=true;}
        AddMissing(c.DiveLoot);
        RpgWeaponCatalog.AddMissing(c.DiveLoot);
        foreach(var item in RpgEternalEightCatalog.Items()){c.DiveLoot.Items.RemoveAll(old=>old.ItemId==item.ItemId);c.DiveLoot.Items.Add(item);}
        RpgZoneService.AddMissing(c);
        foreach(var s in Sets().Concat(RpgEternalEightCatalog.Sets())) {c.Sets.RemoveAll(old=>old.Id==s.Id);c.Sets.Add(s);}
        RpgItemSettings.Apply(c);
        foreach(var r in new[]{ItemRarity.Eternal,ItemRarity.Divine}) {
            c.Salvage.Values.TryAdd(r,new RpgSalvageConfig().Values[r]);
            c.Upgrades.RarityCosts.TryAdd(r.ToString(),[2,3,5]);
        }
    }
    public static void AddMissing(RpgLootConfig c)
    {
        foreach(var item in Items().Concat(RpgEternalEightCatalog.Items())) {c.Items.RemoveAll(old=>old.ItemId==item.ItemId);c.Items.Add(item);}
    }
    public static List<RpgItemSet> Sets()
    {
        var result=new List<RpgItemSet>();
        void S(string id,string name,params RpgSetBonus[] bonuses)=>result.Add(new(){Id=id,Name=name,ItemIds=Items().Where(i=>i.SetId==id).Select(i=>i.ItemId).ToList(),Bonuses=bonuses.ToList()});
        RpgSetBonus B(int n,string description,RpgAttributes? a=null)=>new(){RequiredPieces=n,Description=description,Attributes=a??new()};
        S("eternal_star_hunter","Sternenjäger des Urmeers",
            B(1,"Sternenglück: +35 % Luck, +20 % Perlenfund",new(){Luck=35,PearlBonus=20}),
            B(2,"Meister der Tiefenjagd: 35 % zusätzliche normale Tauchbeute, 30 % zusätzlicher Fisch, +30 % relative Materialfundchance",new(){MaterialFind=30}),
            B(3,"Schatzsegen: +40 % normale Perlen, +30 % Boss-Schaden, +25 % Angriff",new(){PearlBonus=40,BossDamage=30,AttackPercent=25}),
            B(4,"Jäger der Urtiefe: +35 % Krit-Chance, +40 % Krit-Schaden, +20 % Doppelangriff",new(){CritChance=35,CritDamage=40,DoubleAttackChance=20}),
            B(5,"Unendliche Ausbeute: 25 % zusätzlicher normaler Angel-/Tauchroll; +50 % EXP",new(){ExperienceBonus=50}),
            B(6,"Ewiger Sternenjäger: +75 % Gesamtschaden, +50 % Luck, +50 % relative Materialchance, 30 % weiterer normaler Material-/Fischroll, +35 % Boss-Schaden",new(){Damage=75,Luck=50,MaterialFind=50,BossDamage=35}));
        S("eternal_tide_blessing","Gezeitensegen der Ewigkeit",
            B(1,"Ewige Regeneration: +35 % Heilung; alle 5 Sekunden 5 % maximale HP",new(){HealingPower=35}),
            B(2,"Lebendige Gezeiten: +35 % maximale HP; direkte Treffer heilen 12 % tatsächlich verursachten Schadens",new(){MaximumHpPercent=35,BossLifeleech=12}),
            B(3,"Lebensstrom: Krits heilen 8 % maximale HP, 3 Sekunden Abklingzeit; +25 % Krit-Chance",new(){CritChance=25}),
            B(4,"Unzerbrechlicher Lebenswille: unter 40 % HP sofort 35 % maximale HP heilen, 20 Sekunden Abklingzeit; +35 % Gesamtschaden",new(){Damage=35}),
            B(5,"Macht der Heilflut: tatsächliche Eigenheilungen geben +5 % Schaden, maximal 5 Stapel für 10 Sekunden; +30 % Angriffstempo",new(){AttackSpeed=30}),
            B(6,"Unsterblicher Gezeitensegen: +75 % Gesamtschaden, +50 % Heilung; alle 8 Sekunden 20 % maximale HP; einmal pro Kampf tödlichen Treffer verhindern und auf 50 % HP heilen, danach 10 Sekunden +50 % Boss-Schaden",new(){Damage=75,HealingPower=50}));
        S("eternal_primordial_wrath","Zorn der Ewigen Urtiefe",
            B(1,"Zorn der Urtiefe: +50 % Angriff, +35 % Krit-Schaden",new(){AttackPercent=50,CritDamage=35}),
            B(2,"Meister der Vernichtung: +35 % Krit-Chance, +75 % Krit-Schaden",new(){CritChance=35,CritDamage=75}),
            B(3,"Bossschlächter: +75 % Boss-Schaden, +25 % Angriffstempo",new(){BossDamage=75,AttackSpeed=25}),
            B(4,"Ewiger Doppelangriff: +35 % Doppelangriff; Doppelangriffe verursachen zusätzlich 150 % ursprünglichen Angriffsschaden",new(){DoubleAttackChance=35}),
            B(5,"Unaufhaltsamer Zorn: Krits geben 10 Sekunden +8 % Gesamtschaden, maximal 10 Stapel; bei 10 Stapeln +30 % Angriffstempo"),
            B(6,"Weltenbrecher: +125 % Gesamtschaden, +100 % Boss-Schaden; alle 6 Sekunden nächster direkter Treffer +450 % Angriffsschaden; Krits ignorieren 30 % Schadensreduktion",new(){Damage=125,BossDamage=100}));
        S("divine_infinity","Erhabenheit der Unendlichkeit",
            B(1,"Göttliche Macht: +1.000 % Angriff, maximales Leben, Rüstung und relative Gegnerchance (höchstens 100 %)",new(){AttackPercent=1000,MaximumHpPercent=1000,ArmorPercent=1000,EnemyEncounterChance=1000}),
            B(2,"Allwissender Schatzsucher: +500 % Luck, Fischfund, Tauchbeute und normale Perlen",new(){Luck=500,FishingLuck=500,DiveLuck=500,PearlBonus=500}),
            B(3,"Schöpfung ohne Grenzen: garantierte normale Materialbelohnung, dreifache normale Lootmengen; +500 % relative Materialchance",new(){MaterialFind=500}),
            B(4,"Unberührbarkeit: 100 % Ausweichen, Immunität gegen gewöhnlichen Kampfschaden",new(){BossDodgeChance=100}),
            B(5,"Unsterblichkeit: nach jedem eigenen erfolgreichen Angriff vollständig heilen; tödlichen Kampfschaden verhindern; +500 % Boss-Schaden",new(){BossDamage=500}),
            B(6,"Urteil der Götter: garantierter Sofortsieg gegen normale Gegner, Eliten, Mimics und Bosse mit regulären Belohnungen; +10.000 % göttlicher Anzeigeschaden",new(){DivineDamage=10000}),
            B(7,"Herrscher aller Gezeiten: +500 % Erfahrung, +100 % Bewegungstempo (verkürzt auch Angel- und Tauchpausen)",new(){ExperienceBonus=500,MovementSpeed=100}),
            B(8,"Vollendung der Schöpfung: sechsfache reguläre Gebietsbeute statt dreifacher Menge – Fische, Perlen, normale Materialien und Ausrüstung; Eternal-Drops und Mimic-Materialsonderwürfe bleiben unverändert"));
        return result;
    }
}
