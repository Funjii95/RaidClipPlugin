using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class RpgChatCommand(ViewerPointStore points, RpgConfig config, long maximumBalance = long.MaxValue)
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
        if(command is "!talent" or "!talente")
        {
            var p=(await points.GetProfileAsync(user.UserId,token)).Entry.Rpg;
            if(command=="!talent"){
                var result=await RpgTalentService.LearnAsync(points,config,user.UserId,user.UserName,id,parts[1],p.TalentRanks.GetValueOrDefault(parts[1]),SnapshotProtocol.TalentRules.CatalogVersion,token);
                if(!result.Success)return result.Error;
                p=(await points.GetProfileAsync(user.UserId,token)).Entry.Rpg;
                return $"Talent gelernt. Verbleibende Talentpunkte: {SnapshotProtocol.TalentRules.Available(p.PlayerLevel,p.TalentRanks)}.";
            }
            var branch=parts.Length==1?null:parts[1].ToLowerInvariant() switch {"kämpfen"=>"Boss","angeln"=>"Fishing",_=>"Dive"};
            return $"Talentpunkte: {SnapshotProtocol.TalentRules.Available(p.PlayerLevel,p.TalentRanks)} verbleibend / {SnapshotProtocol.TalentRules.Earned(p.PlayerLevel)} verdient. Lernen: !talent <ID>. "+string.Join(" | ",SnapshotProtocol.TalentRules.Catalog.Where(t=>branch==null||t.Branch==branch).Select(t=>$"{t.Name} ({t.Id}) {p.TalentRanks.GetValueOrDefault(t.Id)}/{t.Max}"));
        }
        if (command == "!muscheln")
        {
            if(parts.Length==1){
                var stock=(await points.GetProfileAsync(user.UserId,token)).Entry.Rpg.Materials ?? new();
                stock.Validate();
                return $"Muscheln: {stock.Shells}. Upgrade-Perlen: {stock.WhitePearls} weiße, {stock.BluePearls} blaue, {stock.RedPearls} rote. Öffnen: !muscheln <1–100>.";
            }
            var opened=await new RpgShellService(points,config,maximumBalance:maximumBalance).OpenAsync(user.UserId,user.UserName,id,int.Parse(parts[1]),token);
            return opened.Message;
        }
        if (command == "!hp")
        {
            if (!config.Health.Enabled) return "❤️ Das HP-System ist deaktiviert.";
            var healthProfile = await RpgHealthService.ReadAsync(points,user.UserId,user.UserName,config.Health,DateTimeOffset.UtcNow,token);
            return $"@{user.UserName} | " + RpgHealthService.Status(healthProfile,config.Health);
        }
        if(command is "!salvage" or "!itemlock" or "!favorite")
        {
            var salvage=new RpgSalvageService(points,config,maximumBalance:maximumBalance);
            if(command != "!salvage")
            {
                var enabled=parts.Length<3 || parts[2].Equals("on",StringComparison.OrdinalIgnoreCase);
                var changed=await salvage.SetProtectionAsync(user.UserId,user.UserName,id,int.Parse(parts[1]),command=="!favorite",enabled,token);
                return changed.Success ? $"🦦 @{user.UserName}, Gegenstand {parts[1]}: " + (command=="!favorite" ? "Favorit" : "Zerlegesperre") + (enabled ? " aktiviert." : " aufgehoben.") : $"🦦 @{user.UserName}, {changed.Error}";
            }
            var confirm=parts[^1].Equals("confirm",StringComparison.OrdinalIgnoreCase);
            var selection=string.Join(" ",parts.Skip(1).Take(parts.Length-1-(confirm?1:0))).ToLowerInvariant();
            if(confirm)
            {
                var pending=salvage.PendingQuote(user.UserId,selection);
                if(pending is null) return $"🦦 @{user.UserName}, keine passende gültige Vorschau. Zuerst !salvage {selection} verwenden.";
                var result=await salvage.ConfirmAsync(user.UserId,user.UserName,id,pending.Token,token);
                return result.Success ? $"🦦 @{user.UserName}, {result.Count} Gegenstand/Gegenstände zerlegt: +{RpgChatPresentation.Number(result.Award)} Perlen. Stand: {RpgChatPresentation.Number(result.Balance)}. Neue Nummern: !inv." : $"🦦 @{user.UserName}, {result.Error}";
            }
            var preview=await salvage.PreviewAsync(user.UserId,selection,token);
            if(!preview.Success) return $"🦦 @{user.UserName}, {preview.Error}";
            var quote=preview.Quote!;
            return $"🦦 @{user.UserName}, Zerlegevorschau: {quote.Items.Count} Item(s), insgesamt {RpgChatPresentation.Number(quote.Total)} Perlen. " +
                $"Bestätigen binnen {config.Salvage.ConfirmationSeconds}s: !salvage {selection} confirm. " +
                string.Join(" | ",quote.Items.Select(i=>$"{i.Number}. {i.Name} [{i.Rarity}]: {RpgChatPresentation.Number(i.Value)} Perlen – {i.Details}"+(i.RequiresWarning?" ⚠ Legendary/Setteil geht unwiderruflich verloren!":"")));
        }
        if (command == "!unequip")
        {
            var slot = Enum.Parse<EquipmentSlot>(parts[1], true);
            var removed = await progression.UnequipAsync(user.UserId, user.UserName, id, slot, token);
            return removed.Success ? $"💍 @{user.UserName}, {RpgChatPresentation.SlotName(slot)} abgelegt. Setboni wurden aktualisiert; Übersicht: !sets." : $"🦦 @{user.UserName}, {removed.Error}";
        }
        RpgTransactionResult? action = null;
        if (command == "!perk" && parts.Length == 2) action = await progression.BuyPerkAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!craft" && parts.Length == 2) action = await progression.CraftAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!sushi" && parts.Length == 2 && parts[1] is not ("status" or "recipes")) action = await progression.ActivateSushiAsync(user.UserId, user.UserName, id, parts[1], token);
        if (command == "!equip")
            action = await progression.EquipSelectionAsync(user.UserId, user.UserName, id, parts[1],
                parts.Length == 3 ? Enum.Parse<EquipmentSlot>(parts[2], true) : null, token);
        if (action is not null)
            return action.Success && action.Details is not null ? RpgChatPresentation.Success(user.UserName, action.Details, action.Balance) :
                $"🦦 @{user.UserName}, " + (action.Success ? "Aktion gespeichert, aber Ergebnisdetails fehlen. Bitte Status/Protokoll prüfen; nicht erneut kaufen." : action.Error);
        if (command == "!rpg")
            return "🦦 " + (parts.Length > 1 && parts[1] is not ("hilfe" or "help") ? "Unbekannter RPG-Unterbefehl. " : "RPG-Hilfe: ") +
                "Talente: !talente [kämpfen|angeln|tauchen], lernen: !talent <ID>. Perk kaufen: !perk <ID> (Alias !buyperk). IDs/Kosten: !perks. Inventar: !inv [Seite]. Details: !item <Nummer>. Ausrüsten: !equip <Nummer> [ring1|ring2]. Ablegen: !unequip <Slot>. Ausrüstung: !gear. Sets: !sets. Sushi: !craft <Rezept>, !sushi <Rezept>. Werte: !stats.";
        ViewerPointEntry account;
        try { account = (await points.GetProfileAsync(user.UserId, token)).Entry; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { throw new RpgChatLoadException(ex); }
        var profile = account.Rpg;
        ValidateView(profile);
        var page = parts.Length == 2 && int.TryParse(parts[1], out var requested) ? Math.Max(1, requested) : 1;
        if(command=="!treasures") return ListPage(user.UserName,"🦦 Sammlerschätze",
            profile.Inventory.Where(i=>i.Kind==RpgItemKind.Treasure && i.Collector is not null).GroupBy(i=>i.ItemId).Select(g=>
                $"{RpgChatPresentation.ItemName(g.First())} [{g.First().Rarity}] {g.Count()}× (wirkt einmal): "+string.Join(", ",RpgCollectorService.Describe(g.First().Collector!)))
                .Concat(profile.Inventory.Any(i=>i.Collector is not null)?new[]{"Aktive Summen (nur Sammlung/Wirtschaft): "+string.Join(", ",RpgCollectorService.Describe(RpgCollectorService.Calculate(profile,config.DiveLoot)))}:Array.Empty<string>()),
            page,"Noch keine Sammlerschätze gefunden.","!treasures");
        if(command=="!fishbag") return ListPage(user.UserName,"🎣 Fischvorrat",profile.Fish.Where(f=>f.Value.Quantity>0).Select(f=>
            $"{config.FishSpecies.FirstOrDefault(s=>s.Id==f.Key)?.Name??f.Key} [{f.Key}]: {f.Value.Quantity}×"),page,"Noch keine Zutaten. Fang: !fish oder !fish <Einsatz>.","!fishbag");
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
                "Vollständige Werte: " + RpgChatPresentation.Bonuses(RpgEquipmentUpgradeRules.Effective(item)),
                $"Zerlegewert: {RpgChatPresentation.Number(RpgSalvageService.Value(item,config.Salvage))} Perlen." +
                    (item.IsLocked || item.IsFavorite ? " Gesperrt/favorisiert: nicht zerlegbar." : "") +
                    (item.Rarity==ItemRarity.Legendary || !string.IsNullOrEmpty(item.SetId) ? " ⚠ Legendary/Setteil: ausdrückliche Bestätigung erforderlich; Sammelzerlegen ausgeschlossen." : "")
            }.Concat(item.Collector is null ? Array.Empty<string>() : RpgCollectorService.Describe(item.Collector).Select(s=>"Passiver Sammlereffekt (einmal je Schatz): "+s))
                .Concat(item.Effects.Select(e => "Spezialeffekt – " + RpgPassiveService.Describe(e)))
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
            (RpgPerkCatalog.Find(p.Id) is {Value:>0} entry ? $"{entry.Value*Math.Clamp(profile.Perks.GetValueOrDefault(p.Id),0,5)} {entry.Label}" : RpgChatPresentation.Bonuses(RpgChatPresentation.Snapshot(p.BonusPerLevel, Math.Clamp(profile.Perks.GetValueOrDefault(p.Id),0,5)))) +
            (profile.Perks.GetValueOrDefault(p.Id) >= p.MaximumLevel ? " MAX" : " → " + RpgChatPresentation.Number(p.Cost(profile.Perks.GetValueOrDefault(p.Id))) + " Perlen")),
            page, "Keine Perks konfiguriert.", "!perks");
        if (command == "!sushi")
        {
            string Name(string recipeId) => config.Recipes.SingleOrDefault(r => r.Id == recipeId) is { } recipe ? RpgChatPresentation.RecipeName(recipe) : recipeId;
            var stock = profile.Sushi.Where(p => p.Value > 0).Select(p => $"Vorrat: {Name(p.Key)} [{p.Key}] {p.Value}×");
            var buffs = profile.Buffs.Where(b => RpgSushiService.Active(b,DateTimeOffset.UtcNow)).Select(b => $"{b.Category}: {Name(b.RecipeId)}, {RpgChatPresentation.Bonuses(b.Attributes)}, noch {(b.ExpiresAt is {} expiry?Math.Max(0,(int)Math.Ceiling((expiry-DateTimeOffset.UtcNow).TotalMinutes)):b.RemainingUses)} " +
                (b.ExpiresAt is not null?"Minuten":b.EncounterBased?"Bosskampfteilnahmen":"gültige "+RpgChatPresentation.Activity(b.Activity)) + (b.RemainingUses==0?" – letzter Boss läuft":"")+
                (b.PassiveHealing>0?$", +{b.PassiveHealing} HP/Minute":"")+(b.ChatHealing>0?$", +{b.ChatHealing} Chat-HP":"")+(b.BossDamageReduction>0?$", -{b.BossDamageReduction} % Boss-Gegenschaden":"")+(b.BossLifeleech>0?$", {b.BossLifeleech} % Boss-Lifeleech":"")).DefaultIfEmpty("keine aktiven Buffs");
            var recipes=config.Recipes.Select(r=>$"{RpgChatPresentation.RecipeName(r)} [{r.Id}, {r.Category}]: {RpgSushiService.IngredientsText(r,config)} + {r.PearlCost} Perlen; {RpgSushiService.Effects(r)} 1 Stunde. !cook {r.Id} / !eat {r.Id}");
            var statusPage = parts.Length == 3 && parts[1] is "status" or "recipes" && int.TryParse(parts[2], out var selectedPage) ? Math.Max(1, selectedPage) : 1;
            return ListPage(user.UserName, "🍣 Sushi-Status", buffs.Concat(stock).Concat(parts.Length==1||parts[1]=="recipes"?recipes:Array.Empty<string>()), statusPage,
                "Kein Sushi im Vorrat und keine aktiven Buffs. Herstellung: !craft <Rezept>.", parts.Length==1||parts[1]=="recipes"?"!sushi recipes":"!sushi status");
        }
        if (command == "!stats")
        {
            if(config.Health.Enabled) profile = await RpgHealthService.ReadAsync(points,user.UserId,user.UserName,config.Health,DateTimeOffset.UtcNow,token);
            var stats = CharacterStatService.Calculate(profile, config, RpgActivity.Boss);
            return $"Talentpunkte: {SnapshotProtocol.TalentRules.Available(profile.PlayerLevel,profile.TalentRanks)} verbleibend. " + (config.Health.Enabled ? RpgHealthService.Status(profile,config.Health)+" " : "") + "Sushi: " + RpgSushiService.Status(profile,DateTimeOffset.UtcNow) + " | " + $"🦦 @{user.UserName}, effektive Boss-Werte (mit Caps): {RpgChatPresentation.Bonuses(stats)}. " +
                $"Krit-Multiplikator: {RpgChatPresentation.Number(config.BaseCritMultiplier + stats.CritDamage / 100)}× nur bei kritischem Treffer. " +
                "Dive-/Angelwerte mit passenden Buffs: " +
                $"Tauchglück {RpgChatPresentation.Number(CharacterStatService.Calculate(profile, config, RpgActivity.Dive).DiveLuck)}, " +
                $"Angelglück {RpgChatPresentation.Number(CharacterStatService.Calculate(profile, config, RpgActivity.Fishing).FishingLuck)}. " +
                "Boss-Lifeleech (Sushi): " + RpgChatPresentation.Number(RpgSushiService.Defensive(profile,DateTimeOffset.UtcNow).Select(b=>b.BossLifeleech).DefaultIfEmpty().Max()) + " %. " +
                "Aktive Sets: " + string.Join(", ",RpgSetService.Progress(profile,config).Where(p=>p.Set.Bonuses.Any(b=>p.Equipped>=b.RequiredPieces)).Select(p=>$"{p.Set.Name} ({p.Equipped})"));
        }
        return "🦦 Nutzung: !perk <ID> | !equip <Item-ID> <Slot> | !craft <Rezept> | !sushi <Rezept>. Rezepte: " +
            string.Join(" | ", config.Recipes.Select(r => $"{RpgChatPresentation.RecipeName(r)} [{r.Id}]: {r.FishQuantity}× {config.FishSpecies.SingleOrDefault(f => f.Id == r.FishId)?.Name ?? r.FishId} + {RpgChatPresentation.Number(r.PearlCost)} Perlen"));
    }
    private static bool ValidArguments(string command, string[] parts)
    {
        static bool Page(string text) => int.TryParse(text, out var page) && page >= 1;
        return command switch
        {
            "!talente" => parts.Length==1 || parts.Length==2 && new[]{"kämpfen","angeln","tauchen"}.Contains(parts[1].ToLowerInvariant()),
            "!talent" or "!perk" or "!craft" => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]),
            "!equip" => parts.Length == 2 || parts.Length == 3 && Enum.TryParse<EquipmentSlot>(parts[2], true, out var slot) && Enum.IsDefined(slot),
            "!inventory" or "!equipment" or "!perks" or "!rpgtext" or "!sets" or "!treasures" or "!fishbag" => parts.Length == 1 || parts.Length == 2 && Page(parts[1]),
            "!unequip" => parts.Length == 2 && Enum.TryParse<EquipmentSlot>(parts[1], true, out var unequipSlot) && Enum.IsDefined(unequipSlot),
            "!salvage" => parts.Length>=2 && parts.Length<=4,
            "!itemlock" or "!favorite" => parts.Length is 2 or 3 && Page(parts[1]) && (parts.Length==2 || parts[2].Equals("on",StringComparison.OrdinalIgnoreCase) || parts[2].Equals("off",StringComparison.OrdinalIgnoreCase)),
            "!item" => parts.Length == 2 || parts.Length == 3 && Page(parts[2]),
            "!muscheln" => parts.Length==1 || parts.Length==2 && int.TryParse(parts[1],out var shells) && shells is >=1 and <=100,
            "!stats" or "!hp" => parts.Length == 1,
            "!sushi" => parts.Length is 1 or 2 || parts.Length == 3 && parts[1] is "status" or "recipes" && Page(parts[2]),
            "!rpg" => true,
            _ => false
        };
    }
    private string ItemLine(RpgItem item, RpgProfile profile, bool detail = false)
    {
        var slots = profile.Equipment.Where(p => p.Value == item.InstanceId).Select(p => RpgChatPresentation.SlotName(p.Key)).ToArray();
        var displayName = RpgChatPresentation.ItemName(item);
        var line = (profile.Inventory.IndexOf(item) + 1) + ". " + (item.Slot is null && item.Rarity == ItemRarity.Legendary ? "💎" : RpgChatPresentation.ItemIcon(item.Slot)) + " " + displayName + $" [{item.Rarity}]";
        if(item.IsLocked) line+=" 🔒";
        if(item.IsFavorite) line+=" ⭐";
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

