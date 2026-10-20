using System.Globalization;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed partial class ViewerPointStore
{
    private static string DuelReservation(string id,long amount)
    {
        if(!Guid.TryParseExact(id,"N",out _) || amount<=0)throw new ArgumentException("Ungültige Duell-Reservierung.");
        return "duel-reserve:"+id+":"+amount.ToString(CultureInfo.InvariantCulture);
    }
    private static string DuelFinished(string id)=>"duel-finish:"+id;
    private async Task SaveDuelCommitAsync(CancellationToken ct)
    {
        // Balances and durable reservation/finalization markers share the ORIGINAL atomic save.
        await SaveAsync(ct);
        try{await File.WriteAllTextAsync(_historyPath,JsonSerializer.Serialize(_history,JsonOptions),CancellationToken.None);}
        catch{Console.WriteLine("Duell gespeichert; separate Verlaufsdatei konnte nicht aktualisiert werden.");}
    }
    internal async Task RecoverDuelReservationsAsync(CancellationToken ct)
    {
        using var rollback=Admission?.EnterRollback();
        await _lock.WaitAsync(ct);
        try{
            await EnsureLoadedAsync(ct);
            var before=JsonSerializer.Serialize(_entries,JsonOptions);
            var changed=false;
            foreach(var entry in _entries!.Values){
                foreach(var marker in entry.Rpg.CompletedActions.Where(x=>x.StartsWith("duel-reserve:",StringComparison.Ordinal)).ToArray()){
                    var parts=marker.Split(':');
                    if(parts.Length!=3 || !Guid.TryParseExact(parts[1],"N",out _) || !long.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out var amount) || amount<=0)throw new InvalidDataException("Ungültiger Duell-Beleg; manuelle Prüfung erforderlich.");
                    var finish=DuelFinished(parts[1]);
                    if(entry.Rpg.CompletedActions.Contains(finish))continue;
                    entry.Points=checked(entry.Points+amount);entry.Rpg.CompletedActions.Add(finish);changed=true;
                }
            }
            if(changed)try{await SaveAsync(ct);}catch{_entries=JsonSerializer.Deserialize<Dictionary<string,RaidClipPlugin.Models.ViewerPointEntry>>(before,JsonOptions);throw;}
        }finally{_lock.Release();}
    }
}
