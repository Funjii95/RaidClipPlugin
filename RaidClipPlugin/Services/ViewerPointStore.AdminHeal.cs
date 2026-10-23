using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;
namespace RaidClipPlugin.Services;

public sealed record RpgHealAllResult(bool Success,string Message,int Healed=0,int Players=0,bool Duplicate=false);
public sealed partial class ViewerPointStore
{
    /// <summary>One durable commit for the whole existing roster. No player IDs come from the viewer.</summary>
    internal async Task<RpgHealAllResult> HealAllAsync(string owner,string actionId,RpgHealthConfig health,DateTimeOffset now,CancellationToken ct)
    {
        using var operationAdmission=Admission?.EnterOperation();
        await _lock.WaitAsync(ct);
        var originals=new Dictionary<string,ViewerPointEntry>();
        try
        {
            await EnsureLoadedAsync(ct);
            health.Validate();
            if(IsPrivacyBlocked(owner)||!_entries!.TryGetValue(owner,out var broadcaster)||broadcaster.Rpg is not {SchemaVersion:1})return new(false,"Spielstand des Kanalinhabers fehlt oder ist gesperrt.");
            var marker="admin-heal-all:"+actionId;
            if(broadcaster.Rpg.CompletedActions.Contains(marker))return new(true,"Diese Heilung wurde bereits gespeichert.",Duplicate:true);
            int healed=0,players=0;
            foreach(var entry in _entries.Values.ToArray())
            {
                if(IsPrivacyBlocked(entry.UserId)||entry.Rpg is not {SchemaVersion:1})continue;
                var draft=JsonSerializer.Deserialize<ViewerPointEntry>(JsonSerializer.Serialize(entry,JsonOptions),JsonOptions)!;
                var maximum=RpgHealthService.Maximum(draft.Rpg,health);
                if(draft.Rpg.Health.CurrentHp!=maximum)healed++;
                RpgHealthService.Refresh(draft.Rpg,health,now);
                draft.Rpg.Health.CurrentHp=RpgHealthService.Maximum(draft.Rpg,health);
                // Healing never erases the existing defeat/participation record for an active boss.
                if(entry.UserId==owner)draft.Rpg.CompletedActions.Add(marker);
                draft.UpdatedAt=now;originals[entry.UserId]=entry;_entries[entry.UserId]=draft;players++;
            }
            EnsureItemSystemBackup();await SaveAsync(ct);
            return new(true,$"Alle {players} Spieler auf volle HP geheilt ({healed} mit fehlenden HP).",healed,players);
        }
        catch {foreach(var entry in originals)_entries![entry.Key]=entry.Value;throw;}
        finally {_lock.Release();}
    }
}
