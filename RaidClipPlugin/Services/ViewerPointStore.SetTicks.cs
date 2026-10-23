using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class ViewerPointStore
{
    // Timer state itself is the durable idempotency guard. No action token per second.
    internal async Task TickSetHealthAsync(RpgConfig config,DateTimeOffset now,CancellationToken ct)
    {
        if(!config.Enabled||!config.Health.Enabled)return;
        using var operationAdmission=Admission?.EnterOperation();
        await _lock.WaitAsync(ct);
        var originals=new Dictionary<string,ViewerPointEntry>();
        try
        {
            await EnsureLoadedAsync(ct);
            foreach(var entry in _entries!.Values.ToArray())
            {
                if(IsPrivacyBlocked(entry.UserId)||entry.Rpg is not {} p ||
                    !(p.Health.SetShieldHp>0||RpgSetCombatService.Active(p,config.Sets,"tide_king_regalia",6)||RpgSetCombatService.Active(p,config.Sets,"coral_oracle",4)||RpgSetCombatService.Active(p,config.Sets,"moon_tide_jewelry",2)))continue;
                var before=JsonSerializer.Serialize(p,JsonOptions);
                var draft=JsonSerializer.Deserialize<ViewerPointEntry>(JsonSerializer.Serialize(entry,JsonOptions),JsonOptions)!;
                RpgHealthService.Refresh(draft.Rpg,config.Health,now);
                if(before==JsonSerializer.Serialize(draft.Rpg,JsonOptions))continue;
                draft.UpdatedAt=now;originals[entry.UserId]=entry;_entries[entry.UserId]=draft;
            }
            if(originals.Count>0)await SaveAsync(ct);
        }
        catch {foreach(var entry in originals)_entries![entry.Key]=entry.Value;throw;}
        finally{_lock.Release();}
    }
}
