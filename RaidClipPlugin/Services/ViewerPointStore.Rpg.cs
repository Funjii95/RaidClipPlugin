using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgMutation(bool Success, string Error = "");
public sealed record RpgTransactionResult(bool Success, string Error, long Balance, bool Duplicate = false);

public sealed partial class ViewerPointStore
{
    private static void PrepareRpg(ViewerPointEntry account)
    {
        account.Rpg ??= new();
        if (account.Rpg.SchemaVersion != 1) throw new InvalidOperationException("Unbekannte RPG-Datenversion.");
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
        string actionId, Func<ViewerPointEntry, RpgMutation> mutate, CancellationToken token, bool backupBeforeCommit = false)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(actionId) || actionId.Length > 200)
            throw new ArgumentException("Twitch-ID und gültige Aktions-ID sind erforderlich.");
        await _lock.WaitAsync(token);
        try
        {
            await EnsureLoadedAsync(token);
            var existed = _entries!.TryGetValue(userId, out var original);
            var draft = JsonSerializer.Deserialize<ViewerPointEntry>(
                JsonSerializer.Serialize(original ?? new ViewerPointEntry { UserId = userId }, JsonOptions), JsonOptions)!;
            PrepareRpg(draft);
            if (draft.Rpg.CompletedActions.Contains(actionId))
                return new(false, "Aktion wurde bereits verarbeitet.", draft.Points, true);
            var result = mutate(draft);
            if (!result.Success) return new(false, result.Error, original?.Points ?? 0);
            if (draft.Points < 0 || draft.UserId != userId)
                throw new InvalidOperationException("Ungültiger RPG-Kontozustand.");
            draft.DisplayName = displayName;
            draft.UpdatedAt = DateTimeOffset.UtcNow;
            draft.Rpg.CompletedActions.Add(actionId);
            // Capture an immutable pre-migration backup before the first RPG write.
            if (File.Exists(_filePath) && !File.Exists(_filePath + ".pre-rpg.bak"))
                File.Copy(_filePath, _filePath + ".pre-rpg.bak", overwrite: false);
            if (backupBeforeCommit && File.Exists(_filePath))
                File.Copy(_filePath, _filePath + ".rpg-delete-" + Guid.NewGuid().ToString("N") + ".bak");
            _entries[userId] = draft;
            try { await SaveAsync(token); }
            catch
            {
                if (existed) _entries[userId] = original!;
                else _entries.Remove(userId);
                throw;
            }
            return new(true, "", draft.Points);
        }
        finally { _lock.Release(); }
    }
}
