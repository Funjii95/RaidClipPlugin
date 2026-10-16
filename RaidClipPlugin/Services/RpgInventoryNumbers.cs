using System.Runtime.CompilerServices;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// A trusted, per-account view, never a client-provided index-to-ID mapping.
internal sealed class RpgInventoryNumbers
{
    private static readonly ConditionalWeakTable<ViewerPointStore, RpgInventoryNumbers> Stores = new();
    private readonly Dictionary<string, string[]> _views = new();
    internal static RpgInventoryNumbers For(ViewerPointStore store) => Stores.GetValue(store, _ => new());
    internal void Remember(string userId, RpgProfile profile)
    {
        lock (_views) _views[userId] = profile.Inventory.Select(i => i.InstanceId).ToArray();
    }
    internal string[]? Read(string userId)
    {
        lock (_views) return _views.TryGetValue(userId, out var ids) ? (string[])ids.Clone() : null;
    }
    internal static bool Matches(RpgProfile profile, string[] view) =>
        profile.Inventory.Select(i => i.InstanceId).SequenceEqual(view, StringComparer.Ordinal);
}
