using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaidClipPlugin.Services;

public sealed record PrivacyDeletionRequest(string UserId,Guid OperationId,DateTimeOffset RequestedAt,
    bool LocalErased=false,bool CloudErased=false);

public sealed partial class ViewerPointStore
{
    internal async Task<PrivacyDeletionRequest?> ReadPrivacyRequestAsync(string id,CancellationToken ct=default)
    {
        if(!NumericId(id))throw new ArgumentException("Ungültige Twitch-ID.");
        var path=Path.Combine(PrivacyDirectory,id+".json");
        return File.Exists(path)?JsonSerializer.Deserialize<PrivacyDeletionRequest>(await File.ReadAllTextAsync(path,ct)):null;
    }
    public async Task<IReadOnlyList<PrivacyDeletionRequest>> PrivacyRequestsAsync(CancellationToken ct=default)
    {
        var result=new List<PrivacyDeletionRequest>();
        if(Directory.Exists(PrivacyDirectory))foreach(var path in Directory.GetFiles(PrivacyDirectory,"*.json")){
            var request=JsonSerializer.Deserialize<PrivacyDeletionRequest>(await File.ReadAllTextAsync(path,ct))??throw new InvalidDataException("Ungültiger Löschauftrag.");
            if(!NumericId(request.UserId)||Path.GetFileNameWithoutExtension(path)!=request.UserId||request.OperationId==Guid.Empty)throw new InvalidDataException("Ungültiger Löschauftrag.");result.Add(request);
        }
        return result;
    }
    public async Task<PrivacyDeletionRequest> RequestPrivacyDeletionAsync(string id,Guid operationId,CancellationToken ct)
    {
        if(!NumericId(id)||operationId==Guid.Empty)throw new ArgumentException("Ungültiger Löschauftrag.");
        using var maintenance=Admission?.EnterMaintenance();
        await _lock.WaitAsync(ct);try{
            await EnsureLoadedAsync(ct);
            var path=Path.Combine(PrivacyDirectory,id+".json");
            var request=await ReadPrivacyRequestAsync(id,ct);
            if(request?.LocalErased==true)return request;
            if(request is null){
                if(!_entries!.ContainsKey(id))throw new InvalidOperationException("Spieler nicht vorhanden.");
                request=new(id,operationId,DateTimeOffset.UtcNow);
                // Durable suppression first: a crash cannot recreate this player via chat/import.
                await WriteDurableAsync(path,request,ct);
            }
            _entries!.Remove(id);_history.RemoveAll(h=>h.UserId==id);
            await SaveAsync(ct);
            await WriteDurableAsync(_historyPath,_history,ct);
            // Deliberately no pre-deletion backup: it would retain the data being erased.
            var directory=Path.GetDirectoryName(_filePath)!;
            foreach(var file in Directory.GetFiles(directory).Where(p=>{
                var name=Path.GetFileName(p);return name.StartsWith("viewer-points.json.",StringComparison.Ordinal)&&name.EndsWith(".bak",StringComparison.Ordinal)||name.StartsWith("backup-",StringComparison.Ordinal)&&name.EndsWith(".json",StringComparison.Ordinal)||name is "rpg-boss-round.json" or "rpg-boss-round.json.bak";
            }))await RedactJsonFileAsync(file,id,ct);
            var receipts=Path.Combine(Path.GetDirectoryName(directory)!,"action-receipts");
            if(Directory.Exists(receipts))foreach(var file in Directory.GetFiles(receipts,"*.json*")){
                if(file.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)){
                    var retained=new List<string>();
                    foreach(var line in await File.ReadAllLinesAsync(file,ct)){
                        if(string.IsNullOrWhiteSpace(line))continue;
                        using var audit=JsonDocument.Parse(line);
                        if(audit.RootElement.EnumerateObject().Any(p=>(p.Name is "actor" or "target")&&p.Value.ValueKind==JsonValueKind.String&&p.Value.GetString()==id))continue;
                        retained.Add(line);
                    }
                    var temporary=file+".tmp";await File.WriteAllLinesAsync(temporary,retained,ct);File.Move(temporary,file,true);continue;
                }
                using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(file,ct));
                if(doc.RootElement.TryGetProperty("UserId",out var owner)&&owner.GetString()==id)File.Delete(file);
            }
            request=request with{LocalErased=true};await WriteDurableAsync(path,request,ct);return request;
        }finally{_lock.Release();}
    }
    private static async Task RedactJsonFileAsync(string file,string id,CancellationToken ct)
    {
        var node=JsonNode.Parse(await File.ReadAllTextAsync(file,ct))??throw new InvalidDataException("Leere Sicherung.");
        bool Matches(JsonNode? n)=>n is JsonObject obj && obj.Any(p=>p.Key.Equals("UserId",StringComparison.OrdinalIgnoreCase)&&p.Value is JsonValue v&&v.TryGetValue<string>(out var user)&&user==id);
        void Scrub(JsonNode? current){
            if(current is JsonObject obj)foreach(var pair in obj.ToArray()){
                if(pair.Key==id||Matches(pair.Value)){obj.Remove(pair.Key);continue;}Scrub(pair.Value);
            }
            else if(current is JsonArray array)for(var i=array.Count-1;i>=0;i--){if(Matches(array[i]))array.RemoveAt(i);else Scrub(array[i]);}
        }
        if(Matches(node))node=new JsonObject();else Scrub(node);
        await WriteDurableAsync(file,node,ct);
    }
    internal async Task MarkPrivacyCloudErasedAsync(string id,CancellationToken ct)
    {
        // Serialize with the administrator so phase updates cannot overwrite each other.
        await _lock.WaitAsync(ct);try{
            var request=await ReadPrivacyRequestAsync(id,ct)??throw new InvalidDataException("Löschauftrag fehlt.");
            if(!request.LocalErased)throw new InvalidOperationException("Lokale Bereinigung noch offen.");
            await WriteDurableAsync(Path.Combine(PrivacyDirectory,id+".json"),request with{CloudErased=true},ct);
        }finally{_lock.Release();}
    }
}
