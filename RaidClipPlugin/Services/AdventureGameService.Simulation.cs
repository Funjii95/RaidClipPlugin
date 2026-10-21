using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class AdventureGameService
{
    private DateTimeOffset _nextSimulation;
    private async Task SimulateBossAsync(ChatMessage actor,string[] parts,CancellationToken ct)
    {
        // Independently enforce the broadcaster restriction, even with a registry override.
        if(actor.CommandAuthorization==CommandAuthorization.Denied || string.IsNullOrWhiteSpace(_broadcasterId) || actor.UserId!=_broadcasterId){
            await Say("Nur der Broadcaster darf !sim verwenden.",ct);return;
        }
        if(parts.Length!=1){await Say("Nutzung: !sim.",ct);return;}
        if(!_config.Adventures.Boss.Enabled){await Say("Bosskampf ist deaktiviert.",ct);return;}
        if(_now()<_nextSimulation){await Say("Bitte 60 Sekunden zwischen Boss-Simulationen warten.",ct);return;}
        _nextSimulation=_now().AddSeconds(60);
        var config=JsonSerializer.Deserialize<MinigameConfig>(JsonSerializer.Serialize(_config))!;
        config.Adventures.Boss.AutoStartEnabled=false;
        config.Adventures.Rpg.HotTime.Enabled=false;
        var original=(await _points.GetTopAsync(int.MaxValue,ct)).FirstOrDefault(p=>p.UserId==actor.UserId)?.Rpg??new RpgProfile();
        var clock=_now();var start=clock;
        var directory=Path.Combine(Path.GetTempPath(),"otter-boss-sim",Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ViewerPointStore(directory);
            var actors=Enumerable.Range(1,5).Select(i=>(Id:(9000000000L+i).ToString(System.Globalization.CultureInfo.InvariantCulture),Name:"SimOtter"+i)).ToArray();
            foreach(var player in actors)
                await store.ApplyRpgAsync(player.Id,player.Name,"sim-seed",account=>{
                    account.Rpg=JsonSerializer.Deserialize<RpgProfile>(JsonSerializer.Serialize(original))!;
                    account.Rpg.Health=new();account.Rpg.Cooldowns.Clear();
                    RpgHealthService.Refresh(account.Rpg,config.Adventures.Rpg.Health,clock);
                    return new(true);
                },ct);
            // All combat mutations, loot and experience stay in this temporary store.
            // Use independent randomness so simulation does not consume the real round's RNG.
            var game=new AdventureGameService(store,config,(_,_)=>Task.CompletedTask,new CryptoDuelRandom(),()=>clock,_broadcasterId);
            await game.ControlAsync("boss",0,ct);
            await Say($"[SIM] Bosskampf startet: {game.Status.BossName}, {game.Status.BossHp} HP. Fünf Testotter mit deinen Charakterwerten und vollen HP; Zeit läuft beschleunigt.",ct);
            var attacks=0;var crits=0;var damage=0L;var victory=false;var rounds=0;
            var deadline=game.Status.BossEndsAt!.Value;
            var step=Math.Clamp(config.Adventures.Boss.AttackCooldownSeconds,1,86400);
            var progressEvery=Math.Max(20,(int)Math.Ceiling((deadline-start).TotalSeconds/step/6));
            while(game.Status.BossActive && clock<deadline)
            {
                ct.ThrowIfCancellationRequested();rounds++;var accepted=0;
                foreach(var player in actors)
                {
                    if(!game.Status.BossActive)break;
                    var result=await game.ExecuteBossAttackAsync(player.Id,player.Name,"sim-hit-"+rounds,ct,communityVictory:false);
                    if(result.Success){accepted++;attacks++;damage+=result.Damage;if(result.Critical)crits++;}
                    victory|=result.Defeated;
                }
                if(rounds==1 || rounds%progressEvery==0 || !game.Status.BossActive || accepted==0)
                    await Say($"[SIM] Runde {rounds}: Boss {game.Status.BossHp} HP · {attacks} Angriffe · {crits} kritische Treffer · {damage} Schaden.",ct);
                if(accepted==0)break;
                clock=clock.AddSeconds(step);
            }
            if(game.Status.BossActive){clock=deadline.AddSeconds(1);await game.TickAsync(ct);}
            var survivors=0;
            foreach(var player in actors){var p=(await store.GetProfileAsync(player.Id,ct)).Entry.Rpg;if(!config.Adventures.Rpg.Health.Enabled || p.Health.CurrentHp>0)survivors++;}
            await Say($"[SIM] {(victory?"Sieg – Boss besiegt!":"Niederlage – Boss entkommen.")} {survivors}/5 Testotter überleben · {attacks} Angriffe · {(int)(clock-start).TotalSeconds}s simuliert. Keine echten Belohnungen, keine Hot Time.",ct);
        }
        finally
        {
            // Only the exact randomly created simulation directory is removed.
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
