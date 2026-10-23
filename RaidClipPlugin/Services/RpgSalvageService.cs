using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record RpgSalvageItem(int Number, string InstanceId, string Name, ItemRarity Rarity, long Value, string Fingerprint, bool RequiresWarning, string Details);
public sealed record RpgSalvageQuote(string Token, string UserId, string Selection, DateTimeOffset ExpiresAt, IReadOnlyList<RpgSalvageItem> Items, long Total);
public sealed record RpgSalvagePreview(bool Success, string Error, RpgSalvageQuote? Quote = null);
public sealed record RpgSalvageResult(bool Success, string Error, long Award = 0, long Balance = 0, int Count = 0);

// Reusable local interface: inputs are verified account IDs and server-resolved instances.
// Preview is read-only; confirmation commits item removal and currency together.
public sealed class RpgSalvageService(ViewerPointStore points, RpgConfig config, Func<DateTimeOffset>? now = null, long maximumBalance = long.MaxValue)
{
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);
    private sealed record Pending(RpgSalvageQuote Quote, string[] Inventory);
    private sealed class Tickets { public readonly Dictionary<string, Pending> Users = new(); }
    private static readonly ConditionalWeakTable<ViewerPointStore, Tickets> Stores = new();
    private Tickets Cache => Stores.GetValue(points, _ => new());

    public static long Value(RpgItem item, RpgSalvageConfig config)
    {
        config.Validate();
        if (item.SalvageValue.HasValue)
            return item.SalvageValue.Value >= 0 ? item.SalvageValue.Value : throw new InvalidDataException("Negativer Zerlegewert.");
        var range = config.Values[item.Rarity];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"salvage-v1:{item.InstanceId}:{(int)item.Rarity}"));
        return range.Minimum + (long)(BinaryPrimitives.ReadUInt64LittleEndian(hash) % (ulong)(range.Maximum-range.Minimum+1));
    }
    // Definition equality is NOT roll equality. Ignore only instance identity/time and salvage price.
    public static string RollSignature(RpgItem item) => JsonSerializer.Serialize(new
    {
        item.ItemId, item.DisplayName, item.Description, item.IconId, item.Rarity, item.Kind, item.Slot, item.SetId,
        item.ConsumableRecipeId, item.Attributes, item.Collector,
        Effects = item.Effects.Select(e => JsonSerializer.Serialize(e)).OrderBy(e => e, StringComparer.Ordinal).ToArray()
    });
    private static string? Blocked(RpgItem item, RpgProfile profile) =>
        profile.Equipment.Values.Contains(item.InstanceId) ? "ist ausgerüstet; zuerst ablegen" :
        item.IsLocked || item.IsFavorite ? "ist gesperrt oder favorisiert" : null;
    private static bool Sensitive(RpgItem item) => item.Rarity >= ItemRarity.Legendary || !string.IsNullOrEmpty(item.SetId);
    private static string Fingerprint(RpgItem item) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(item))));

    public async Task<RpgSalvagePreview> PreviewAsync(string userId, string selection, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new(false,"Twitch-ID fehlt.");
        if (!config.Enabled || !config.Salvage.Enabled) return new(false,"Zerlegen ist deaktiviert.");
        config.Salvage.Validate();
        var profile = (await points.GetProfileAsync(userId,token)).Entry.Rpg;
        var view = RpgInventoryNumbers.For(points).Read(userId);
        if (view is null) return new(false,"Bitte zuerst !inv anzeigen.");
        if (!RpgInventoryNumbers.Matches(profile,view)) return new(false,"Dein Inventar hat sich geändert. Bitte !inv erneut anzeigen.");
        IEnumerable<RpgItem> chosen;
        if (int.TryParse(selection,out var number))
        {
            if (number < 1 || number > profile.Inventory.Count) return new(false,$"Gegenstand {number} existiert nicht. Übersicht: !inv.");
            chosen = new[] { profile.Inventory[number-1] };
        }
        else if (selection == "duplicates")
            chosen = profile.Inventory.Where(i=>!string.IsNullOrWhiteSpace(i.ItemId)).GroupBy(RollSignature).SelectMany(g => g.OrderByDescending(i => Blocked(i,profile) is not null).Skip(1))
                .Where(i => Blocked(i,profile) is null && !Sensitive(i));
        else if (selection == "all common") chosen = profile.Inventory.Where(i => i.Rarity == ItemRarity.Common && Blocked(i,profile) is null && !Sensitive(i));
        else return new(false,"Nutzung: !salvage <Nummer> | duplicates | all common.");
        return CreateQuote(userId,selection,profile,chosen.ToArray());
    }
    // Future Extension may select full IDs; all ownership/protection checks still happen here.
    public async Task<RpgSalvagePreview> PreviewInstancesAsync(string userId, IReadOnlyList<string> instanceIds, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userId) || !config.Enabled || !config.Salvage.Enabled) return new(false,"Zerlegen ist deaktiviert oder Twitch-ID fehlt.");
        var profile = (await points.GetProfileAsync(userId,token)).Entry.Rpg;
        var ids = instanceIds.Distinct(StringComparer.Ordinal).ToArray();
        var items = profile.Inventory.Where(i => ids.Contains(i.InstanceId,StringComparer.Ordinal)).ToArray();
        if (items.Length != ids.Length) return new(false,"Mindestens ein Item gehört nicht zu deinem Inventar.");
        return CreateQuote(userId,"extension",profile,items);
    }
    private RpgSalvagePreview CreateQuote(string userId,string selection,RpgProfile profile,RpgItem[] items)
    {
        config.Salvage.Validate();
        if (items.Length == 0) return new(false,"Keine passenden ungeschützten Items. Duplikate benötigen exakt gleiche Werte/Effekte; unterschiedliche Rolls, Sets und Legendarys werden nie automatisch zerlegt.");
        if (items.Length > config.Salvage.MaximumBatchSize) return new(false,$"Maximal {config.Salvage.MaximumBatchSize} Items pro Vorschau. Bitte einzeln auswählen.");
        foreach (var item in items) if (Blocked(item,profile) is { } reason) return new(false,$"{RpgChatPresentation.ItemName(item)} {reason}. Nichts wurde zerlegt.");
        var rows = items.Select(i => new RpgSalvageItem(profile.Inventory.IndexOf(i)+1,i.InstanceId,RpgChatPresentation.ItemName(i),i.Rarity,Value(i,config.Salvage),Fingerprint(i),Sensitive(i),
            RpgChatPresentation.Bonuses(RpgEquipmentUpgradeRules.Effective(i)) + string.Concat(i.Effects.Select(e=>"; "+RpgPassiveService.Describe(e))))).ToArray();
        var quote = new RpgSalvageQuote(Guid.NewGuid().ToString("N"),userId,selection,_now().AddSeconds(config.Salvage.ConfirmationSeconds),Array.AsReadOnly(rows),checked(rows.Sum(i=>i.Value)));
        lock (Cache.Users)
        {
            foreach(var key in Cache.Users.Where(p=>p.Value.Quote.ExpiresAt<=_now()).Select(p=>p.Key).ToArray()) Cache.Users.Remove(key);
            if(Cache.Users.Count>=512) Cache.Users.Remove(Cache.Users.MinBy(p=>p.Value.Quote.ExpiresAt).Key);
            Cache.Users[userId] = new(quote,profile.Inventory.Select(i=>i.InstanceId).ToArray());
        }
        return new(true,"",quote);
    }
    public RpgSalvageQuote? PendingQuote(string userId,string selection)
    {
        lock(Cache.Users) return Cache.Users.TryGetValue(userId,out var pending) && pending.Quote.Selection==selection && pending.Quote.ExpiresAt>_now() ? pending.Quote : null;
    }
    public Task<RpgTransactionResult> SetProtectionAsync(string userId,string name,string requestId,int number,bool favorite,bool enabled,CancellationToken token)
    {
        var view=RpgInventoryNumbers.For(points).Read(userId);
        return points.ApplyRpgAsync(userId,name,requestId,account=>
        {
            if(!config.Enabled) return new(false,"RPG ist deaktiviert.");
            if(view is null || !RpgInventoryNumbers.Matches(account.Rpg,view)) return new(false,"Bitte !inv erneut anzeigen.");
            if(number<1 || number>view.Length) return new(false,"Unbekannte Inventarnummer.");
            var item=account.Rpg.Inventory.Single(i=>i.InstanceId==view[number-1]);
            if(favorite) item.IsFavorite=enabled; else item.IsLocked=enabled;
            return new(true);
        },token);
    }
    // Stable IDs and explicit desired state prevent stale UI positions and toggle retries.
    public Task<RpgTransactionResult> SetProtectionInstanceAsync(string userId,string name,string requestId,string instanceId,bool favorite,bool enabled,CancellationToken token) =>
        points.ApplyRpgAsync(userId,name,requestId,account=>
        {
            if(!config.Enabled)return new(false,"RPG ist deaktiviert.");
            var item=account.Rpg.Inventory.SingleOrDefault(i=>i.InstanceId==instanceId);
            if(item is null)return new(false,"Gegenstand gehört nicht zu deinem Inventar.");
            if(favorite)item.IsFavorite=enabled;else item.IsLocked=enabled;
            return new(true);
        },token);

    public async Task<RpgSalvagePreview> PreviewBatchAsync(string userId,string selection,CancellationToken token)
    {
        var profile=(await points.GetProfileAsync(userId,token)).Entry.Rpg;
        IEnumerable<RpgItem> chosen=selection switch {
            "common"=>profile.Inventory.Where(i=>i.Rarity==ItemRarity.Common && Blocked(i,profile) is null && !Sensitive(i)),
            "duplicates"=>profile.Inventory.Where(i=>!string.IsNullOrWhiteSpace(i.ItemId)).GroupBy(RollSignature)
                .SelectMany(g=>g.OrderByDescending(i=>Blocked(i,profile) is not null).Skip(1))
                .Where(i=>Blocked(i,profile) is null && !Sensitive(i)),
            _=>[]};
        return await PreviewInstancesAsync(userId,chosen.Select(i=>i.InstanceId).ToArray(),token);
    }

    public bool CancelQuote(string userId,string quoteToken)
    {
        lock(Cache.Users){
            if(!Cache.Users.TryGetValue(userId,out var pending) || pending.Quote.Token!=quoteToken)return false;
            return Cache.Users.Remove(userId);
        }
    }
    public async Task<RpgSalvageResult> ConfirmAsync(string userId,string name,string requestId,string quoteToken,CancellationToken token)
    {
        Pending? pending;
        lock(Cache.Users) Cache.Users.TryGetValue(userId,out pending);
        if (pending is null || pending.Quote.Token!=quoteToken || pending.Quote.ExpiresAt<=_now()) return new(false,"Bestätigung fehlt oder ist abgelaufen. Bitte neue Vorschau anfordern.");
        var quote = pending.Quote;
        var result = await points.ApplyRpgAsync(userId,name,requestId,account =>
        {
            if(!config.Enabled || !config.Salvage.Enabled) return new(false,"Zerlegen ist deaktiviert.");
            if(quote.ExpiresAt<=_now()) return new(false,"Bestätigung ist abgelaufen. Neue Vorschau erforderlich.");
            if(!RpgInventoryNumbers.Matches(account.Rpg,pending.Inventory)) return new(false,"Inventar wurde verändert. Bitte !inv und eine neue Zerlegevorschau anfordern.");
            foreach(var row in quote.Items)
            {
                var item = account.Rpg.Inventory.SingleOrDefault(i=>i.InstanceId==row.InstanceId);
                if(item is null || Blocked(item,account.Rpg) is not null || Fingerprint(item)!=row.Fingerprint || Value(item,config.Salvage)!=row.Value)
                    return new(false,"Item, Schutz oder Zerlegewert wurde verändert. Nichts wurde zerlegt; neue Vorschau erforderlich.");
            }
            if(quote.Total>long.MaxValue-account.Points || account.Points>maximumBalance || quote.Total>maximumBalance-account.Points)
                return new(false,"Perlengutschrift würde das Kontolimit überschreiten. Nichts wurde zerlegt.");
            var ids=quote.Items.Select(i=>i.InstanceId).ToHashSet(StringComparer.Ordinal);
            account.Rpg.Inventory.RemoveAll(i=>ids.Contains(i.InstanceId));
            account.Points=checked(account.Points+quote.Total);
            return new(true);
        },token,backupBeforeCommit:true);
        if(!result.Success) return new(false,result.Error,Balance:result.Balance);
        lock(Cache.Users) if(Cache.Users.TryGetValue(userId,out var current) && current.Quote.Token==quoteToken) Cache.Users.Remove(userId);
        return new(true,"",quote.Total,result.Balance,quote.Items.Count);
    }
}

