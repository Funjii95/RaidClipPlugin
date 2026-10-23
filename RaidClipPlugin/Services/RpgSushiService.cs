using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public static class RpgSushiService
{
    public const int DurationSeconds=3600;
    public static int Uses(RpgRecipe r,RpgConfig c)=>r.BossUses>0?r.BossUses:c.BuffUses;
    public static bool Active(RpgBuff b,DateTimeOffset now) => b.ExpiresAt is {} end ? now<end : b.RemainingUses>0;
    public static IEnumerable<RpgBuff> Defensive(RpgProfile p,DateTimeOffset now) => p.Buffs.Where(b=>b.Category==SushiCategory.Defensive && Active(b,now));
    public static int ChatBonus(RpgProfile p,DateTimeOffset now) => Math.Clamp(Defensive(p,now).Select(b=>b.ChatHealing).DefaultIfEmpty().Max(),0,1000);
    public static decimal PassiveBonus(RpgProfile p,DateTimeOffset now) => Math.Clamp(Defensive(p,now).Select(b=>b.PassiveHealing).DefaultIfEmpty().Max(),0,1000);
    public static int BossDamage(int raw,RpgProfile p,DateTimeOffset now)
    {
        var reduction=Math.Clamp(Defensive(p,now).Select(b=>b.BossDamageReduction).DefaultIfEmpty().Max(),0,80);
        return (int)decimal.Ceiling(raw*(1-reduction/100));
    }
    /// <summary>Authoritative boss attack only: no per-hit healing cap, just actual damage and missing HP.</summary>
    public static int HealFromBossAttack(int damage,int remainingBossHp,RpgProfile profile,RpgHealthConfig health,string bossId,DateTimeOffset now,decimal criticalHealing=0)
    {
        var h=profile.Health;
        if(damage<=0 || remainingBossHp<=0 || h.CurrentHp is null or <=0 || h.Defeated && h.BossId==bossId)return 0;
        var percent=Math.Clamp(Defensive(profile,now).Select(b=>b.BossLifeleech).DefaultIfEmpty().Max(),0,100)+Math.Clamp(criticalHealing,0,100)+RpgPerkCatalog.Combined(profile,"lifeleech");
        var actualDamage=Math.Min(damage,remainingBossHp);
        var healing=decimal.Floor(actualDamage*percent/100);
        return RpgSetCombatService.Heal(profile,health,(int)Math.Min(int.MaxValue,healing),now);
    }
    public static IReadOnlyDictionary<string,int> Ingredients(RpgRecipe recipe) => recipe.Ingredients.Count>0 ? recipe.Ingredients : new Dictionary<string,int>{{recipe.FishId,recipe.FishQuantity}};
    public static string IngredientsText(RpgRecipe recipe,RpgConfig config) => string.Join(" + ",Ingredients(recipe).Select(p=>$"{p.Value}× {config.FishSpecies.FirstOrDefault(f=>f.Id==p.Key)?.Name??p.Key}"));
    public static string Effects(RpgRecipe r) =>
        (r.PassiveHealing>0?$"+{RpgChatPresentation.Number(r.PassiveHealing)} HP/Minute passiv. ":"")+
        (r.ChatHealing>0?$"+{r.ChatHealing} HP je gültiger Chat-Heilung. ":"")+
        (r.BossDamageReduction>0?$"{RpgChatPresentation.Number(r.BossDamageReduction)} % weniger großer Boss-Gegenschaden. ":"")+
        (r.BossLifeleech>0?$"{RpgChatPresentation.Number(r.BossLifeleech)} % Lebensraub aus tatsächlichem großen Boss-Schaden, ohne Overkill und ohne festes Heilungscap. ":"")+
        RpgChatPresentation.Bonuses(r.Attributes);
    public static string Status(RpgProfile p,DateTimeOffset now) => string.Join(" | ",p.Buffs.Where(b=>Active(b,now)).Select(b=>
        $"{(b.Category==SushiCategory.Defensive?"Defensiv":"Offensiv")}: {b.RecipeId}, {b.RemainingUses} Boss-Teilnahmen"+(b.RemainingUses==0?" (letzter Boss noch aktiv)":"")));
    public static void Participate(RpgProfile p,string bossId,DateTimeOffset end,DateTimeOffset now)
    {
        foreach(var b in p.Buffs.Where(b=>b.EncounterBased && Active(b,now) && b.LastBossId!=bossId))
        {
            b.LastBossId=bossId;b.RemainingUses--;
            if(b.RemainingUses<=0)b.ExpiresAt=end;
        }
    }
    public static void Close(RpgProfile p,string bossId,DateTimeOffset end,DateTimeOffset now,RpgHealthConfig health)
    {
        foreach(var b in p.Buffs.Where(b=>b.EncounterBased && b.LastBossId==bossId && b.RemainingUses<=0))
            b.ExpiresAt=b.ExpiresAt is {} previous && previous<end ? previous : end;
        RpgHealthService.Refresh(p,health,now);
        p.Buffs.RemoveAll(b=>b.EncounterBased && b.ExpiresAt is {} until && until<=now);
    }
    public static void Upgrade(RpgProfile p)
    {
        if(p.SushiVersion>=2)return;
        p.Buffs.RemoveAll(b=>b.ExpiresAt is {} end && end<=DateTimeOffset.UtcNow || b.RemainingUses<=0 && b.ExpiresAt is null);
        foreach(var b in p.Buffs)
        {if(b.ActivatedAt==default)b.ActivatedAt=DateTimeOffset.UtcNow;b.EncounterBased=false;b.ExpiresAt=b.ActivatedAt.AddHours(1);}
        // Existing multi-buff profiles remain intact; their slot stays occupied until all legacy buffs end.
        p.SushiVersion=2;
    }
    public static void Validate(RpgConfig c)
    {
        if(c.FreeFishingReward is <0 or >1000000 || c.FishSpecies.Any(f=>f.MinimumStake<0 || f.PearlReward is <0 or >1000000) ||
            c.Recipes.Any(r=>!Enum.IsDefined(r.Category)||r.BossUses is <0 or >100 || r.PassiveHealing is <0 or >1000 || r.ChatHealing is <0 or >1000 || r.BossDamageReduction is <0 or >80 || r.BossLifeleech is <0 or >100 ||
                Ingredients(r).Any(i=>i.Value<1 || !c.FishSpecies.Any(f=>f.Id==i.Key))))
            throw new InvalidOperationException("Ungültige Sushi-Zutaten, Buffwerte oder Fangbelohnungen.");
    }
    public static void AddMissing(RpgConfig c)
    {
        var fish=new[]{
            new RpgFishDefinition{Id="sea_cucumber",Name="Seegurke",Weight=50,PearlReward=40,Zone="Flachwasser"},
            new RpgFishDefinition{Id="shrimp",Name="Garnele",Weight=25,PearlReward=100,Zone="Korallenriff",Rarity=ItemRarity.Uncommon},
            new RpgFishDefinition{Id="giant_shrimp",Name="Riesengarnele",Weight=12,PearlReward=250,Zone="Tiefsee",Rarity=ItemRarity.Rare},
            new RpgFishDefinition{Id="pufferfish",Name="Kugelfisch",Weight=10,PearlReward=350,Zone="Tiefsee",Rarity=ItemRarity.Rare},
            new RpgFishDefinition{Id="golden_pufferfish",Name="Goldener Kugelfisch",Weight=3,PearlReward=800,Zone="Abgrund",Rarity=ItemRarity.Epic},
            new RpgFishDefinition{Id="vampire_squid",Name="Vampirtintenfisch",Weight=8,PearlReward=300,Zone="Tiefsee",Rarity=ItemRarity.Rare},
            new RpgFishDefinition{Id="tuna",Name="Thunfisch",Weight=25,PearlReward=150,Zone="Offenes Meer",Rarity=ItemRarity.Uncommon}};
        foreach(var f in fish)if(!c.FishSpecies.Any(x=>x.Id==f.Id))c.FishSpecies.Add(f);
        var recipes=new[]{
            new RpgRecipe{Id="kappa",DisplayName="Kappa-Maki",FishId="sea_cucumber",FishQuantity=2,PearlCost=300,BossUses=3,Category=SushiCategory.Defensive,Activity=RpgActivity.Boss,PassiveHealing=3},
            new RpgRecipe{Id="tempura",DisplayName="Garnelen-Tempura-Rolle",FishId="shrimp",FishQuantity=2,Ingredients=new(){{"shrimp",2},{"giant_shrimp",1}},PearlCost=450,BossUses=3,Category=SushiCategory.Defensive,Activity=RpgActivity.Boss,ChatHealing=5},
            new RpgRecipe{Id="kugelfisch",DisplayName="Kugelfisch-Sashimi",FishId="pufferfish",FishQuantity=2,PearlCost=700,BossUses=3,Category=SushiCategory.Defensive,Activity=RpgActivity.Boss,BossDamageReduction=15},
            new RpgRecipe{Id="vampir",DisplayName="Vampir-Maki",FishId="vampire_squid",FishQuantity=2,Ingredients=new(){{"vampire_squid",2},{"tuna",1}},PearlCost=750,BossUses=3,Category=SushiCategory.Defensive,Activity=RpgActivity.Boss,BossLifeleech=5}};
        foreach(var r in recipes)if(!c.Recipes.Any(x=>x.Id==r.Id))c.Recipes.Add(r);
        RpgZoneCuisine.AddMissing(c);
    }
}
