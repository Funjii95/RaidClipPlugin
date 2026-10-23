using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgMutation(bool Success, string Error = "", RpgActionDetails? Details = null);
public sealed record RpgTransactionResult(bool Success, string Error, long Balance, bool Duplicate = false,
    RpgActionDetails? Details = null);

public sealed partial class ViewerPointStore
{
    internal string BossRoundPath => Path.Combine(Path.GetDirectoryName(_filePath)!,"rpg-boss-round.json");
    internal async Task<IReadOnlyList<(string Id,string Name,int BossHp)>> BossParticipantsAsync(string bossId,CancellationToken token)
    {
        using var operationAdmission = Admission?.EnterOperation();
        await _lock.WaitAsync(token);
        try
        {
            await EnsureLoadedAsync(token);
            return _entries!.Values.Where(a=>a.Rpg?.Health?.BossId==bossId)
                .Select(a=>(a.UserId,a.DisplayName,a.Rpg.Health.BossRemainingHp)).ToArray();
        }
        finally {_lock.Release();}
    }
    private void EnsureItemSystemBackup()
    {
        if(File.Exists(_filePath) && !File.Exists(_filePath+".pre-talents-v1.bak"))
            File.Copy(_filePath,_filePath+".pre-talents-v1.bak",overwrite:false);
        if(File.Exists(_filePath) && !File.Exists(_filePath+".pre-materials-v1.bak"))
            File.Copy(_filePath,_filePath+".pre-materials-v1.bak",overwrite:false);
        if(File.Exists(_filePath) && !File.Exists(_filePath+".pre-sushi-2.bak"))
            File.Copy(_filePath,_filePath+".pre-sushi-2.bak",overwrite:false);
        if(File.Exists(_filePath) && !File.Exists(_filePath+".pre-core-stats.bak"))
            File.Copy(_filePath,_filePath+".pre-core-stats.bak",overwrite:false);
        if (File.Exists(_filePath) && !File.Exists(_filePath + ".pre-items-v2.bak"))
            File.Copy(_filePath, _filePath + ".pre-items-v2.bak", overwrite: false);
    }
    private static void ValidatePersistedInventories(JsonElement root)
    {
        static bool Property(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
            value = default; return false;
        }
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Kontodatei ist kein Objekt.");
        foreach (var account in root.EnumerateObject())
        {
            // Accounts predating RPG may legitimately have no RPG property.
            if (!Property(account.Value, "Rpg", out var rpg)) continue;
            if (Property(rpg,"TalentRanks",out var talentRanks) && talentRanks.ValueKind!=JsonValueKind.Object)
                throw new InvalidDataException("Gespeicherte Talente sind ungültig.");
            if (Property(rpg, "Materials", out var materials))
            {
                string[] keys = ["Shells","WhitePearls","BluePearls","RedPearls","StoneChips"];
                if (materials.ValueKind != JsonValueKind.Object ||
                    materials.EnumerateObject().Any(p=>!typeof(RpgMaterials).GetProperties().Any(k=>k.Name.Equals(p.Name,StringComparison.OrdinalIgnoreCase))) ||
                    materials.EnumerateObject().Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=materials.EnumerateObject().Count())
                    throw new InvalidDataException("Gespeicherter Materialvorrat ist unvollständig.");
                foreach (var key in keys)
                    if (!Property(materials,key,out var value) || !value.TryGetInt32(out var count) || count<0)
                        throw new InvalidDataException("Gespeicherter Materialvorrat ist ungültig.");
                foreach(var field in materials.EnumerateObject())
                    if(!field.Value.TryGetInt32(out var balance)||balance<0)throw new InvalidDataException("Ungültiger Materialbestand.");
            }
            if (!Property(rpg, "Inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Gespeichertes RPG-Inventar fehlt oder ist keine Liste.");
            foreach (var item in inventory.EnumerateArray())
            {
                if (!Property(item, "InstanceId", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                    throw new InvalidDataException("Gespeichertes Item ohne gültige InstanceId; automatische ID-Erzeugung beim Laden wird verhindert.");
                if(Property(item,"UpgradeLevel",out var level)&&(!level.TryGetInt32(out var n)||n is <0 or >9))throw new InvalidDataException("Ungültige Aufwertungsstufe.");
                if(Property(item,"IsRefined",out var refined)&&(refined.ValueKind is not (JsonValueKind.True or JsonValueKind.False)||refined.ValueKind==JsonValueKind.True&&(!Property(item,"UpgradeLevel",out var l)||l.GetInt32()!=9)))throw new InvalidDataException("Ungültige Veredelung.");
            }
        }
    }
    private static void PrepareRpg(ViewerPointEntry account)
    {
        account.Rpg ??= new();
        if (account.Rpg.SchemaVersion != 1) throw new InvalidOperationException("Unbekannte RPG-Datenversion.");
        SnapshotProtocol.TalentRules.Validate(account.Rpg.PlayerLevel,account.Rpg.TalentRanks);
        account.Rpg.Materials ??= new();
        account.Rpg.Materials.Validate();
        RpgCoreStatService.Upgrade(account.Rpg);
        RpgSushiService.Upgrade(account.Rpg);
        if (account.Rpg.LegacyFishImported) return;
        foreach (var (oldName, id) in new[] { ("Rotfeder", "sardine"), ("Hecht", "perch"), ("Goldener Koi", "crystal-carp"), ("Mondaal", "eel"), ("Kraken", "leviathan") })
        {
            var count = account.FishCollection?.GetValueOrDefault(oldName) ?? 0;
            if (count <= 0) continue;
            if (!account.Rpg.Fish.TryGetValue(id, out var fish)) account.Rpg.Fish[id] = fish = new() { FishId = id };
            fish.Quantity = checked(fish.Quantity + count);
        }
        account.Rpg.LegacyFishImported = true;
    }
    /// <summary>Authoritative local gameplay transaction. The callback only receives a detached
    /// account; it must have no external side effects. Currency and inventory commit together.</summary>
    internal async Task<RpgTransactionResult> ApplyRpgAsync(string userId, string displayName,
        string actionId, Func<ViewerPointEntry, RpgMutation> mutate, CancellationToken token, bool backupBeforeCommit = false,
        Func<RpgGlobalBossState?,RpgGlobalBossState?>? mutateGlobalBoss = null)
    {
        using var operationAdmission = Admission?.EnterOperation();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actionId) || actionId.Length > 200)
            throw new ArgumentException("Twitch-ID und gültige Aktions-ID sind erforderlich.");
        GroqStoryEvent? levelStory=null;
        await _lock.WaitAsync(token);
        try
        {
            await EnsureLoadedAsync(token);
            var existed = _entries!.TryGetValue(userId, out var original);
            RequireNotPrivacyBlocked(userId);
            var draft = JsonSerializer.Deserialize<ViewerPointEntry>(
                JsonSerializer.Serialize(original ?? new ViewerPointEntry { UserId = userId }, JsonOptions), JsonOptions)!;
            PrepareRpg(draft);
            if (draft.Rpg.CompletedActions.Contains(actionId))
                return new(false, "Aktion wurde bereits verarbeitet.", draft.Points, true);
            var result = mutate(draft);
            if (!result.Success) return new(false, result.Error, original?.Points ?? 0);
            RpgCollectionInventory.SalvageOverflow(draft, _collectionSalvage, _collectionMaximumBalance);
            if (draft.Points < 0 || draft.UserId != userId)
                throw new InvalidOperationException("Ungültiger RPG-Kontozustand.");
            var previousBoss=_globalBoss;
            if(mutateGlobalBoss is not null){var nextBoss=mutateGlobalBoss(_globalBoss is null?null:JsonSerializer.Deserialize<RpgGlobalBossState>(JsonSerializer.Serialize(_globalBoss))!);_globalBoss=nextBoss is null?null:JsonSerializer.Deserialize<RpgGlobalBossState>(JsonSerializer.Serialize(nextBoss))!;}
            draft.DisplayName = displayName;
            draft.UpdatedAt = DateTimeOffset.UtcNow;
            draft.Rpg.CompletedActions.Add(actionId);
            // Capture an immutable pre-migration backup before the first RPG write.
            EnsureItemSystemBackup();
            if (File.Exists(_filePath) && !File.Exists(_filePath + ".pre-rpg.bak"))
                File.Copy(_filePath, _filePath + ".pre-rpg.bak", overwrite: false);
            if (backupBeforeCommit && File.Exists(_filePath))
                File.Copy(_filePath, _filePath + ".rpg-delete-" + Guid.NewGuid().ToString("N") + ".bak");
            _entries[userId] = draft;
            try { await SaveAsync(token); }
            catch
            {
                _globalBoss=previousBoss;
                if (existed) _entries[userId] = original!;
                else _entries.Remove(userId);
                throw;
            }
            if(draft.Rpg.PlayerLevel>(original?.Rpg?.PlayerLevel??1))
                levelStory=new(GroqScene.LevelUp,"level:"+userId+":"+actionId,"Neuer Level erreicht",displayName,draft.Rpg.PlayerLevel);
            return new(true, "", draft.Points, Details: result.Details);
        }
        finally { _lock.Release(); if(levelStory is not null)PublishStory(levelStory); }
    }
}
