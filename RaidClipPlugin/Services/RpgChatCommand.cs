using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class RpgChatCommand(ViewerPointStore points, RpgConfig config)
{
    public static bool Recognizes(string command) => RpgCommandCatalog.Find(command) is not null;
    public async Task<string> ExecuteAsync(ChatMessage user, string command, string[] parts, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(user.UserId)) return "🦦 Twitch-ID fehlt; RPG-Daten können nicht sicher zugeordnet werden.";
        if (user.CommandAuthorization == CommandAuthorization.Denied) return "🦦 Dieser RPG-Befehl ist für dich nicht erlaubt.";
        try
        {
            var definition = RpgCommandCatalog.Find(command);
            if (definition is null) return "🦦 Unbekannter RPG-Befehl. Hilfe: !rpg. Perk kaufen: !perk <ID> oder !buyperk <ID>.";
            command = definition.Command;
            if (!config.Enabled) return "🦦 RPG ist aktuell deaktiviert.";
            if (!ValidArguments(command, parts)) return $"🦦 Nutzung: {command} {definition.Usage}".Trim() +
                (command == "!perk" ? " – Beispiel: !perk damage (Alias: !buyperk damage). Übersicht: !perks." : command == "!equip" ? " – Slots: Weapon, Helmet, Chest, Gloves, Boots, Ring1, Ring2, Amulet." : "");
            if (command == "!rpgtext") return RpgReplyCache.For(points).Read(user.UserId, user.UserName,
                parts.Length == 2 && int.TryParse(parts[1], out var p) ? Math.Max(1, p) : 1);
            var reply = RpgChatPresentation.Clean(await ExecuteCoreAsync(user, command, parts, token));
            if (reply.Length == 0) throw new InvalidOperationException("Die RPG-Formatierung lieferte keinen Text.");
            return RpgReplyCache.For(points).Publish(user.UserId, user.UserName, reply);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Console.WriteLine("RPG-Chatdarstellung: " + ex);
            return $"🦦 @{user.UserName}, " + (ex is RpgChatDataException ? "Inventar-/RPG-Daten sind fehlerhaft oder unvollständig, nicht einfach leer. Bitte Plugin-Protokoll prüfen." :
                ex is RpgChatLoadException ? "RPG-Daten konnten nicht geladen werden. Bitte Plugin-Protokoll prüfen." :
                "RPG-Antwort konnte nicht erstellt werden. Bitte Status/Plugin-Protokoll prüfen, bevor du die Aktion erneut ausführst.");
        }
    }
    private async Task<string> ExecuteCoreAsync(ChatMessage user, string command, string[] parts, CancellationToken token)
    {
        if (!config.Enabled) return "🦦 RPG ist aktuell deaktiviert.";
        var progression = new RpgProgressionService(points, config);
        var id = string.IsNullOrWhiteSpace(user.Id) ? Guid.NewGuid().ToString("N") : user.Id;
        if (command == "!unequip")
        {
            var slot = Enum.Parse<EquipmentSlot>(parts[1], true);
            var removed = await progression.UnequipAsync(user.UserId, user.UserName, id, slot, token);
            return removed.Success ? $"💍 @{user.UserName}, {RpgChatPresentation.SlotName(slot)} abgelegt. Setboni wurden aktualisiert; Übersicht: !sets." : $"🦦 @{user.UserName}, {removed.Error}";
        }
        RpgTransactionResult? action = null;
        if (command == "!perk" && parts.Length == 2) action = await progression.BuyPerkAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!craft" && parts.Length == 2) action = await progression.CraftAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!sushi" && parts.Length == 2) action = await progression.ActivateSushiAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!equip")
            action = await progression.EquipSelectionAsync(user.UserId, user.UserName, id, parts[1],
                parts.Length == 3 ? Enum.Parse<EquipmentSlot>(parts[2], true) : null, token);
        if (action is not null)
            return action.Success && action.Details is not null ? RpgChatPresentation.Success(user.UserName, action.Details, action.Balance) :
                $"🦦 @{user.UserName}, " + (action.Success ? "Aktion gespeichert, aber Ergebnisdetails fehlen. Bitte Status/Protokoll prüfen; nicht erneut kaufen." : action.Error);
        if (command == "!rpg")
            return "🦦 " + (parts.Length > 1 && parts[1] is not ("hilfe" or "help") ? "Unbekannter RPG-Unterbefehl. " : "RPG-Hilfe: ") +
                "Perk kaufen: !perk <ID> (Alias !buyperk). IDs/Kosten: !perks. Inventar: !inv [Seite]. Details: !item <Nummer>. Ausrüsten: !equip <Nummer> [ring1|ring2]. Ablegen: !unequip <Slot>. Ausrüstung: !gear. Sets: !sets. Sushi: !craft <Rezept>, !sushi <Rezept>. Werte: !stats.";
        ViewerPointEntry account;
        try { account = (await points.GetProfileAsync(user.UserId, token)).Entry; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { throw new RpgChatLoadException(ex); }
        var profile = account.Rpg;
        ValidateView(profile);
        var page = parts.Length == 2 && int.TryParse(parts[1], out var requested) ? Math.Max(1, requested) : 1;
        if (command is "!inventory" or "!inventar")
        {
            RpgInventoryNumbers.For(points).Remember(user.UserId, profile);
            return ListPage(user.UserName, "🦦 Inventar", profile.Inventory.Select(i => ItemLine(i, profile)), page,
                "Noch keine Items gefunden. Nutze !dive.", "!inv");
        }
        if (command == "!item")
        {
            if (parts.Length is < 2 or > 3) return "💍 Nutzung: !item <vollständige Item-ID> [Seite]. IDs findest du mit !inventar.";
            string selection = parts[1];
            if (int.TryParse(selection, out var number))
            {
                var view = RpgInventoryNumbers.For(points).Read(user.UserId);
                if (view is null) return "💍 Bitte zuerst !inv anzeigen, dann !item <Nummer> verwenden.";
                if (!RpgInventoryNumbers.Matches(profile, view)) return "💍 Dein Inventar hat sich geändert. Bitte !inv erneut anzeigen.";
                if (number < 1 || number > view.Length) return $"💍 Gegenstand {number} existiert nicht. Übersicht: !inv.";
                selection = view[number - 1];
            }
            var item = profile.Inventory.SingleOrDefault(i => i.InstanceId.Equals(selection, StringComparison.OrdinalIgnoreCase));
            if (item is null) return $"💍 @{user.UserName}, dieses Item ist nicht in deinem Inventar. Übersicht: !inv; Details: !item <Nummer>.";
            var detailPage = parts.Length == 3 && int.TryParse(parts[2], out var requestedDetail) ? Math.Max(1, requestedDetail) : 1;
            return ListPage(user.UserName, "💍 Itemdetails", new[] { ItemLine(item, profile, true),
                "Vollständige Werte: " + RpgChatPresentation.Bonuses(item.Attributes),
            }.Concat(item.Effects.Select(e => "Spezialeffekt – " + RpgPassiveService.Describe(e)))
                .Concat(RpgSetService.Progress(profile, config).Where(p => p.Set.Id == item.SetId).SelectMany(RpgSetService.Describe))
                .Concat(new[] { "Beschreibung: " + (string.IsNullOrWhiteSpace(item.Description) ? "Keine Beschreibung hinterlegt." : item.Description),
                    item.Slot.HasValue ? $"Ausrüsten: !equip {parts[1]}" : "Dieser Gegenstand hat keinen Ausrüstungsplatz." }),
                detailPage, "Keine Itemdetails.", "!item " + parts[1]);
        }
        if (command == "!sets") return ListPage(user.UserName, "💍 Sets", RpgSetService.Progress(profile, config).SelectMany(p =>
            RpgSetService.Describe(p).Concat(p.Set.ItemIds.Select(itemId =>
                (config.DiveLoot.Items.FirstOrDefault(i => i.ItemId == itemId)?.DisplayName ?? itemId) + " – " +
                (profile.Inventory.Any(i => i.ItemId == itemId && i.SetId == p.Set.Id && profile.Equipment.Values.Contains(i.InstanceId)) ? "ausgerüstet" :
                    p.OwnedItemIds.Contains(itemId) ? "im Inventar" : "fehlt")))), page, "Keine Sets konfiguriert.", "!sets");
        if (command is "!equipment" or "!ausrüstung") return ListPage(user.UserName, "💍 Ausrüstung", profile.Equipment.Select(p =>
            RpgChatPresentation.SlotName(p.Key) + ": " + ItemLine(profile.Inventory.Single(i => i.InstanceId == p.Value), profile))
            .Concat(RpgSetService.Progress(profile, config).Where(p => p.Equipped > 0).SelectMany(RpgSetService.Describe)),
            page, "Noch nichts ausgerüstet. Nutze !inv und !equip <Nummer>.", "!gear");
        if (command == "!perks") return ListPage(user.UserName, "⭐ Perks", config.Perks.Select(p =>
            RpgChatPresentation.PerkName(p) + $" ({p.Id}) {profile.Perks.GetValueOrDefault(p.Id)}/{p.MaximumLevel}: " +
            RpgChatPresentation.Bonuses(RpgChatPresentation.Snapshot(p.BonusPerLevel, profile.Perks.GetValueOrDefault(p.Id))) +
            (profile.Perks.GetValueOrDefault(p.Id) >= p.MaximumLevel ? " MAX" : " → " + RpgChatPresentation.Number(p.Cost(profile.Perks.GetValueOrDefault(p.Id))) + " Perlen")),
            page, "Keine Perks konfiguriert.", "!perks");
        if (command == "!sushi")
        {
            string Name(string recipeId) => config.Recipes.SingleOrDefault(r => r.Id == recipeId) is { } recipe ? RpgChatPresentation.RecipeName(recipe) : recipeId;
            var stock = profile.Sushi.Where(p => p.Value > 0).Select(p => $"Vorrat: {Name(p.Key)} [{p.Key}] {p.Value}×");
            var buffs = profile.Buffs.Where(b => b.RemainingUses > 0).Select(b => $"Aktiv: {Name(b.RecipeId)}, {RpgChatPresentation.Bonuses(b.Attributes)}, noch {b.RemainingUses} gültige {RpgChatPresentation.Activity(b.Activity)}");
            var statusPage = parts.Length == 3 && parts[1] == "status" && int.TryParse(parts[2], out var selectedPage) ? Math.Max(1, selectedPage) : 1;
            return ListPage(user.UserName, "🍣 Sushi-Status", buffs.Concat(stock), statusPage,
                "Kein Sushi im Vorrat und keine aktiven Buffs. Herstellung: !craft <Rezept>.", "!sushi status");
        }
        if (command == "!stats")
        {
            var stats = CharacterStatService.Calculate(profile, config, RpgActivity.Boss);
            return $"🦦 @{user.UserName}, effektive Boss-Werte (mit Caps): {RpgChatPresentation.Bonuses(stats)}. " +
                $"Krit-Multiplikator: {RpgChatPresentation.Number(config.BaseCritMultiplier + stats.CritDamage / 100)}× nur bei kritischem Treffer. " +
                "Dive-/Angelwerte mit passenden Buffs: " +
                $"Tauchglück {RpgChatPresentation.Number(CharacterStatService.Calculate(profile, config, RpgActivity.Dive).DiveLuck)}, " +
                $"Angelglück {RpgChatPresentation.Number(CharacterStatService.Calculate(profile, config, RpgActivity.Fishing).FishingLuck)}.";
        }
        return "🦦 Nutzung: !perk <ID> | !equip <Item-ID> <Slot> | !craft <Rezept> | !sushi <Rezept>. Rezepte: " +
            string.Join(" | ", config.Recipes.Select(r => $"{RpgChatPresentation.RecipeName(r)} [{r.Id}]: {r.FishQuantity}× {config.FishSpecies.SingleOrDefault(f => f.Id == r.FishId)?.Name ?? r.FishId} + {RpgChatPresentation.Number(r.PearlCost)} Perlen"));
    }
    private static bool ValidArguments(string command, string[] parts)
    {
        static bool Page(string text) => int.TryParse(text, out var page) && page >= 1;
        return command switch
        {
            "!perk" or "!craft" => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]),
            "!equip" => parts.Length == 2 || parts.Length == 3 && Enum.TryParse<EquipmentSlot>(parts[2], true, out var slot) && Enum.IsDefined(slot),
            "!inventory" or "!equipment" or "!perks" or "!rpgtext" or "!sets" => parts.Length == 1 || parts.Length == 2 && Page(parts[1]),
            "!unequip" => parts.Length == 2 && Enum.TryParse<EquipmentSlot>(parts[1], true, out var unequipSlot) && Enum.IsDefined(unequipSlot),
            "!item" => parts.Length == 2 || parts.Length == 3 && Page(parts[2]),
            "!stats" => parts.Length == 1,
            "!sushi" => parts.Length is 1 or 2 || parts.Length == 3 && parts[1] == "status" && Page(parts[2]),
            "!rpg" => true,
            _ => false
        };
    }
    private string ItemLine(RpgItem item, RpgProfile profile, bool detail = false)
    {
        var slots = profile.Equipment.Where(p => p.Value == item.InstanceId).Select(p => RpgChatPresentation.SlotName(p.Key)).ToArray();
        var displayName = RpgChatPresentation.ItemName(item);
        var line = (profile.Inventory.IndexOf(item) + 1) + ". " + (item.Slot is null && item.Rarity == ItemRarity.Legendary ? "💎" : RpgChatPresentation.ItemIcon(item.Slot)) + " " + displayName + $" [{item.Rarity}]";
        if (!detail) return line + (slots.Length > 0 ? " ✓ ausgerüstet: " + string.Join(", ", slots) : "") +
            (item.Slot.HasValue ? "" : " (" + RpgChatPresentation.KindName(item) + ")");
        return line + " | " + RpgChatPresentation.KindName(item) + (item.Slot.HasValue ? " – " + RpgChatPresentation.SlotName(item.Slot.Value) : "; nicht ausrüstbar") +
            " | " + (slots.Length > 0 ? "ausgerüstet: " + string.Join(", ", slots) : "nicht ausgerüstet");
    }
    private static void ValidateView(RpgProfile? profile)
    {
        if (profile is null || profile.Inventory is null || profile.Equipment is null || profile.Perks is null || profile.Sushi is null || profile.Buffs is null)
            throw new RpgChatDataException("Eine RPG-Sammlung fehlt.");
        if (profile.Inventory.Any(i => i is null || string.IsNullOrWhiteSpace(i.InstanceId) || i.Attributes is null || !Enum.IsDefined(i.Rarity)) ||
            profile.Inventory.Select(i => i.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profile.Inventory.Count ||
            profile.Equipment.Any(p => !profile.Inventory.Any(i => i.InstanceId == p.Value)) || profile.Buffs.Any(b => b is null || b.Attributes is null))
            throw new RpgChatDataException("Inventar enthält unvollständige Items oder ungültige Ausrüstungsreferenzen.");
    }
    private static string ListPage(string name, string label, IEnumerable<string> entries, int page, string empty, string command)
    {
        return RpgChatPages.Page(name, label, entries, page, command, empty);
    }
}
