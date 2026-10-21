using System.Security.Cryptography;
using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public enum RpgResetScope { Pearls, Items, PearlsAndItems }
public sealed record RpgResetPreview(int Players,long Items,long Fish,long Sushi,string Fingerprint);
public sealed record RpgResetResult(bool Success,string Message,int Players=0,bool Duplicate=false);
internal sealed record AdministrationReceipt(string Operation,string? Fingerprint,RpgResetResult? Result);

public sealed partial class ViewerPointStore
{
    private static bool NumericId(string id)=>id.Length is >=1 and <=25 && id.All(char.IsAsciiDigit);
    private string AdminDirectory=>Path.Combine(Path.GetDirectoryName(_filePath)!,"administration");
    internal string PrivacyDirectory=>Path.Combine(Path.GetDirectoryName(_filePath)!,"privacy");
    internal bool IsPrivacyBlocked(string id)=>NumericId(id) && File.Exists(Path.Combine(PrivacyDirectory,id+".json"));
    private void RequireNotPrivacyBlocked(string id){if(IsPrivacyBlocked(id))throw new InvalidOperationException("Spieler ist wegen einer Datenlöschung gesperrt.");}
    internal static async Task WriteDurableAsync<T>(string file,T value,CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temp=file+".tmp";
        await using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){
            await JsonSerializer.SerializeAsync(stream,value,cancellationToken:ct);await stream.FlushAsync(ct);stream.Flush(true);
        }
        File.Move(temp,file,true);
    }
    private string Fingerprint()=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(_entries,JsonOptions)));
    private static bool Selected(string id,string? user)=>user is null || user==id;
    public async Task<RpgResetPreview> PreviewResetAsync(string? user,CancellationToken ct)
    {
        if(user is not null && !NumericId(user))throw new ArgumentException("Ungültige Twitch-ID.");
        await _lock.WaitAsync(ct);try{
            await EnsureLoadedAsync(ct);var entries=_entries!.Where(p=>Selected(p.Key,user)).Select(p=>p.Value).ToArray();
            return new(entries.Length,entries.Sum(p=>(long)p.Rpg.Inventory.Count),entries.Sum(p=>p.Rpg.Fish.Sum(f=>(long)f.Value.Quantity)),entries.Sum(p=>p.Rpg.Sushi.Sum(s=>(long)s.Value)),Fingerprint());
        }finally{_lock.Release();}
    }
    public async Task<RpgResetResult> ResetAsync(string? user,RpgResetScope scope,Guid operationId,string expectedFingerprint,CancellationToken ct)
    {
        if(operationId==Guid.Empty || !Enum.IsDefined(scope) || user is not null && !NumericId(user))throw new ArgumentException("Ungültiger Verwaltungsauftrag.");
        using var maintenance=Admission?.EnterMaintenance();
        await _lock.WaitAsync(ct);try{
            await EnsureLoadedAsync(ct);var operation=$"{scope}:{user??"ALL"}";var receipt=Path.Combine(AdminDirectory,operationId.ToString("N")+".json");
            if(File.Exists(receipt)){
                var saved=JsonSerializer.Deserialize<AdministrationReceipt>(await File.ReadAllTextAsync(receipt,ct))!;
                if(saved.Operation!=operation)throw new InvalidOperationException("Verwaltungs-ID wurde anders verwendet.");
                return saved.Result is {} done?done with{Duplicate=true}:new(false,"Ergebnis unklar. Spielstand prüfen; nicht blind erneut zurücksetzen.");
            }
            if(Fingerprint()!=expectedFingerprint)return new(false,"Spielstand seit Vorschau geändert. Neu laden und erneut bestätigen.");
            var selected=_entries!.Where(p=>Selected(p.Key,user)).Select(p=>p.Value).ToArray();
            if(selected.Length==0)return new(false,"Keine passenden Spieler vorhanden.");
            var original=JsonSerializer.Serialize(_entries,JsonOptions);
            // Mark before any write. An interrupted reset can never be replayed blindly.
            await WriteDurableAsync(receipt,new AdministrationReceipt(operation,expectedFingerprint,null),ct);
            if(File.Exists(_filePath))File.Copy(_filePath,_filePath+".admin-reset-"+operationId.ToString("N")+".bak",false);
            _entries=JsonSerializer.Deserialize<Dictionary<string,ViewerPointEntry>>(original,JsonOptions)!;
            foreach(var (id,entry) in _entries.Where(p=>Selected(p.Key,user))){
                if(entry.Rpg is null || entry.Rpg.SchemaVersion!=1)throw new InvalidDataException("Unbekanntes RPG-Format; keine Zurücksetzung.");
                if(scope is RpgResetScope.Pearls or RpgResetScope.PearlsAndItems)entry.Points=0;
                if(scope is RpgResetScope.Items or RpgResetScope.PearlsAndItems){
                    entry.Rpg.Inventory.Clear();entry.Rpg.Equipment.Clear();entry.Rpg.Fish.Clear();entry.Rpg.Sushi.Clear();entry.Rpg.Buffs.Clear();entry.Rpg.Materials=new();
                    // Historical catch statistics remain; never reimport them as stock.
                    entry.Rpg.LegacyFishImported=true;
                }
                entry.UpdatedAt=DateTimeOffset.UtcNow;
            }
            try{await SaveAsync(ct);}catch{_entries=JsonSerializer.Deserialize<Dictionary<string,ViewerPointEntry>>(original,JsonOptions)!;throw;}
            var result=new RpgResetResult(true,"Zurückgesetzt. Cloud-Abgleich erfolgt über die bestehende Warteschlange.",selected.Length);
            await WriteDurableAsync(receipt,new AdministrationReceipt(operation,expectedFingerprint,result),CancellationToken.None);return result;
        }finally{_lock.Release();}
    }
}
