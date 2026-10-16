# RPG-Erweiterung – Implementierungsplan

## Bestandsaufnahme

- `AdventureGameService`: bestehende Dive-/Fish-Ergebnisse, Boss, Rennen; serialisiert Aktionen mit SemaphoreSlim. Aktions-Cooldowns bisher nur im Arbeitsspeicher.
- `ViewerPointStore`: gemeinsames Perlenkonto, Profil, Fischsammlung, Historie; JSON in LocalAppData/RaidClipPlugin/minigame. Twitch-User-ID ist Schlüssel. Punktedatei wird über temporäre Datei ersetzt.
- `ViewerPointEntry`: vorhandene Konten und Fischsammlung müssen vollständig erhalten bleiben.
- `MinigameService`/CommandRegistry: bestehende Chat-Routen und Berechtigungen weiterverwenden.
- `MainForm.AdventureGames`: vorhandene PropertyGrids und Konfigurationsspeicherung weiterverwenden.
- Tests: xUnit, bestehende Adventure-/Ökonomie-/UI-Tests.

## Reihenfolge und Abnahmekriterien

1. Datenfundament: additive RPG-Profildaten im vorhandenen Konto; kopierte Transaktionen mit Rollback, persistente Idempotenz und Cooldowns. Alte Daten sichern; defekte Dateien nicht als leere Konten weiterführen.
2. Loot: konfigurierbare Einsatzstufen, Seltenheits-Gating vor RNG, Caps und strukturierte Ergebnisse. Kostenloser Dive separat; bestehende bezahlte Ergebnisse kompatibel halten.
3. Charakter: zentrale Stats, acht Slots, Ring-Attribute, konfigurierbare permanente Perks.
4. Angeln/Crafting: bisherige Perlenauszahlung erhalten; stabile Fisch-IDs ergänzen, Rezepte und drei gültige Aktionsladungen atomar abwickeln.
5. Boss: vorhandene Attack-/Belohnungslogik erweitern; Crit tatsächlich anwenden, Auto-Start nicht verändern.
6. Integration: Chatbefehle, Registry/Hilfe, Verwaltungsoberfläche, einmalige besondere Meldungen.
7. Prüfung: Gating, Parallelität, Persistenz, Rollback, Migration, Buff-Verbrauch, Chat-Service-Konsistenz; kompletter Testlauf und Release-Build.

Kein öffentliches Backend, keine Extension und keine zweite Perlenökonomie. Neue Services müssen ohne Chatformatierung nutzbar sein. Kein Release vor abgeschlossener Integration und separatem Veröffentlichungsauftrag.

## Datenmigration

Neue Profile erhalten Schema 1 im bestehenden `viewer-points.json`. Fehlendes RPG-Feld ergibt ein leeres Profil; bestehende Fischsammlung und alle alten Kontofelder bleiben bestehen. Vor der ersten RPG-Schreibaktion wird die unveränderte Punktedatei einmalig als `.pre-rpg.bak` gesichert. RPG und Perlen werden in einer Datei gemeinsam ersetzt; Änderungen werden erst nach erfolgreichem Speichern im Speicher sichtbar.

## Arbeitsstand

Die Kernfunktionen sind integriert und automatisch geprüft. Zur Veröffentlichung als 2.1.10 (RPG-Teststand) vorgesehen. RPG bleibt standardmäßig deaktiviert. Die bestehende Glücksrad-Layoutkorrektur ist nicht Bestandteil dieses RPG-Auftrags und wurde noch nicht durchgeführt.

## Implementierte Mechaniken

- Optionales `!dive` ohne Einsatz, sonst bestehende Einsatzgrenzen; bestehende Solo-Auszahlungsfaktoren, Tageslimits und Perlenkonten bleiben maßgeblich.
- Zusätzlicher Loot mit Common (grau), Uncommon (grün), Rare (blau), Epic (lila), Legendary (gold). Farbzuordnung in `RarityPresentation` für zukünftige Oberflächen.
- Einsatzstufen 0/100/500/2000/5000 erlauben standardmäßig maximal Uncommon/Rare/Epic/Epic/Legendary. Der separate Legendary-Mindesteinsatz wird unabhängig von Luck vor der Auswahl geprüft. Hohe Einsätze garantieren keinen Gewinn; monetärer Verlust folgt den vorhandenen konfigurierten Solo-Stufen.
- Lootchance = Basis + Einsatzbonus + begrenzter Luckbonus + begrenzter Eventbonus, anschließend Gesamt-Cap. Perk-/Basisluck, Equipment und aktivitätsspezifisches Sushi werden im strukturierten Chance-Ergebnis getrennt ausgewiesen und teilen sich dasselbe Luck-Cap.
- Acht Ausrüstungsplätze; ein Item kann nicht beide Ringplätze gleichzeitig belegen. Standardkatalog mit Ausrüstung, Ringen, Schätzen und Sushi-Fund. Iteminstanzen haben eigene IDs und stabile Icon-IDs, keine lokalen Assetpfade.
- Permanente Perks mit Maximallevel, Basiskosten und Kostenwachstum. Kauf und Abbuchung erfolgen gemeinsam. Kein zusätzlicher Kontostand.
- Angeln bezahlt weiterhin die ursprüngliche Auszahlung. Die zusätzliche Fischarten-Auswahl ist separat gewichtet. Luck erhöht nur die zusätzlichen seltenen Fanggewichte (maximal verdoppelt), nicht unbemerkt die ursprüngliche Fang-/Auszahlungsstufe.
- Crafting verbraucht Fische und Perlen und erzeugt Sushi-Vorrat. Aktivierung ist separat, identische aktive Buffs werden abgelehnt. Unterschiedliche Buffs dürfen bis zur konfigurierten Grenze koexistieren. Standarddauer drei erfolgreiche relevante Aktionen; Boss zählt gültige Angriffe, nicht abgeschlossene ganze Bossrunden.
- Boss verwendet echte Crit-Würfe und Equipment-/Perk-/Sushi-Schaden. Bestehende Bossbelohnung und Auto-Start-Einstellung (standardmäßig 15 Minuten) bleiben unverändert.
- Legendäre Funde erzeugen eine konfigurierbare besondere Chatmeldung. Erneute Verarbeitung derselben erfolgreichen Chat-ID erzeugt keinen weiteren Fund und keine erneute Sondermeldung.

## Konfiguration

Unter **Punkte & Minigames → RPG** befindet sich ein Aktivierungshaken und ein JSON-Balancing-Editor. Der Haken bestimmt die Aktivierung, nicht der JSON-Wert `Enabled`. Speichern validiert die Konfiguration. Diese erste Verwaltungsoberfläche ist ein technischer Editor, noch kein komfortabler grafischer Item-/Rezepteditor.

- `CritChanceCap` 50 %, `BaseCritMultiplier` 1,5, `CritDamageCap` 300 Prozentpunkte, `LuckCap` 100, `DamageBonusCap` 200 %.
- `BuffUses` 3, `MaximumDifferentBuffs` 3; `LegendaryMessage` mit `{user}` und `{item}`.
- `DiveLoot`: `BaseDropChance` 20 %, `MaximumDropChance` 60 %, `LuckChancePerPoint` 0,1, `MaximumLuckBonus` 10, `EventBonus` 0, `MaximumEventBonus` 10, `LegendaryMinimumStake` 5000.
- `RiskTiers`: Mindestbetrag, maximale Rarity und Drop-Bonus; `RarityWeights`: 6500/2500/800/190/10. Gewichte werden nur unter den tatsächlich erlaubten und bestückten Seltenheiten normalisiert.
- `Items`: stabile Definition-ID, Name, Beschreibung, Icon-ID, Rarity, Typ, optional Slot/Verbrauchsrezept und Attribute. Attribute: Damage, CritChance, CritDamage, Luck, BossDamage, PearlBonus, FishingLuck, DiveLuck. Prozentwerte sind Prozentpunkte.
- `Perks`: ID, MaximumLevel (5), BaseCost (250), CostGrowth (2), BonusPerLevel. Standard-IDs: damage, crit, critdamage, luck, boss, dive, fishing.
- `FishSpecies`: ID, Name, Beschreibung, Kategorie, Icon-ID, Rarity und Gewicht; unabhängig vom Dive-Loot.
- `Recipes`: ID, FishId, FishQuantity, PearlCost, Activity (Dive=0, Fishing=1, Boss=2), Attributes. Standard: fishing, fighter, critical, dive.
- Einsatzgrenzen, monetäre Chancen/Faktoren, Cooldowns und Boss-Parameter bleiben in den vorhandenen Spiel-Reitern. Für legendäre Dive-Funde muss der erlaubte Maximaleinsatz mindestens 5000 betragen; der alte Maximalwert wird nicht stillschweigend erhöht.

## Migration und Sicherheit

Vor der ersten erfolgreichen RPG-Schreibaktion wird die bisherige Kontodatei einmalig nach `viewer-points.json.pre-rpg.bak` kopiert. Die zusätzlichen Felder benötigen keine Konten-Neuanlage. Historische Rotfeder/Hecht/Goldener Koi/Mondaal/Kraken-Zahlen werden einmalig als sardine/perch/crystal-carp/eel/leviathan in den zusätzlichen Vorrat übernommen. Historische Fangzahlen selbst werden beim Crafting nicht abgezogen.

Neue Aktionen arbeiten auf einem abgetrennten Kontomodell unter der vorhandenen Store-Sperre. Ablehnungen, Ausnahmen und Fehler beim Schreiben übernehmen keine Perlen-/Item-/Buffänderung. Eine defekte oder unbekannte Kontoversion wird nicht überschrieben. Erfolgreiche Aktions-IDs und Cooldowns überleben Neustarts. Der bestehende Store ist weiterhin für eine Plugin-Instanz ausgelegt; keine Mehrprozess-Datenbank.

## Erweiterungsschnittstellen

- `RpgAdventureService.ExecuteAsync`: strukturierte Solo-Ergebnisse inklusive Spieler-ID, Einsatz, Auszahlung, Kontostand, Loot, angewendeten Buffs und Rest-Cooldown; verwendet den bestehenden Store und dessen Casino-Abrechnung.
- `RpgProgressionService`: BuyPerkAsync, EquipAsync, CraftAsync, ActivateSushiAsync.
- `CharacterStatService`: gemeinsame, begrenzte Werte und tatsächliche Boss-Crit-Schadensberechnung.
- `RpgLootService`: reine Loot-/Chance-Berechnung mit injizierbarem RNG für Tests.
- `AdventureGameService.ExecuteBossAttackAsync`: zentraler Boss-Angriffsaufruf mit strukturiertem Ergebnis (Erfolg, Spieler-ID, Schaden, Crit, Boss-LP, Sieg, Rest-Cooldown, Fehler). Verwendet dieselbe Logik wie der Chatbefehl.

Die zukünftige Extension benötigt weiterhin serverseitig geprüfte Twitch-Identität und Online-/Teilnahmebedingungen. Es wurde weder ein Authentifizierungsbackend noch ein öffentlicher Endpunkt implementiert. Ein Benutzername berechtigt nicht zum Ändern fremder Konten.

## Tests und Build

393 automatisierte Tests bestanden, darunter 26 neue RPG-Testfälle. Geprüft: Rarity-Grenzen bei extremem Luck, Legendary-Gating, getrennte Chance-Bonusquellen, parallele Aktionen, persistente Cooldowns, unveränderte Angel-Grundauszahlung, echte Ring-Crits, Boss-Buffverbrauch, genau drei gültige Solo-Teilnahmen, Kaufkostenprogression, Neustart-Persistenz, doppelte Aktions-IDs, einmalige Legacy-Übernahme, abgelehnte Aktionen und simulierte Schreibfehler. Der Testlauf baut Anwendung und Tests im Release-Modus erfolgreich.

Bekannte bestehende Buildwarnungen: NU1510 und WindowsBase/WebView2-Versionskonflikt. Kein Live-Twitch-Test und kein visueller Test der neuen Einstellungsseite durchgeführt.

## Verbleibende Grenzen

- Keine separate UI-Vorschau der effektiven Dropverteilung; die Daten sind über die zentrale Chance-Berechnung verfügbar.
- Chatversand besitzt keine persistente Outbox. Ein Verbindungsabbruch nach dem gespeicherten Fund kann eine Sondermeldung verhindern. Exakt-einmal-Zustellung an Twitch ist damit nicht garantiert; Fund und Konto bleiben trotzdem gespeichert.
- Aktions-ID-Liste ist derzeit unbeschränkt und muss für sehr lange Laufzeiten noch mit einer wohldefinierten Replay-/Archivierungsstrategie versehen werden.
- Kein Live-Balancing/Langzeit-Wirtschaftstest. Monetäre Hochrisiko-Stufen verwenden vorerst weiterhin dieselben vorhandenen Auszahlungschancen je Einsatz; höhere Beträge erhöhen absolutes Risiko und Loot-Ceiling, nicht die prozentuale Verlustchance.

## Chat-Testanleitung

1. Lokalen neuen Build verwenden, Punkte und Minigames aktivieren. RPG-Haken setzen und speichern. Für ungefährliche Tests einen Testkanal/ein Testkonto nutzen; nicht auf produktiven Zuschauerständen experimentieren.
2. `!dive`, danach sofort erneut `!dive`: zweiter Aufruf muss Cooldown melden. Für Legendary-Gating in einem Testprofil hohen Luck konfigurieren und Einsätze unter/über 5000 vergleichen; keine Garantie auf einen Drop.
3. `!inventory 1`: Item-ID kopieren. `!equip <ID> Ring1`, danach `!equipment` und `!stats` prüfen. Andere Slots entsprechend dem Itemtyp nutzen.
4. `!perks`, `!perk crit`, erneut `!perks`: Level steigt, Kosten der nächsten Stufe steigen und Perlen sinken. Nach Neustart erneut prüfen.
5. `!fish 100`, `!sammlung`, `!craft fishing`, `!sushi fishing`: benötigt Sardine und 100 Perlen. `!sushi` zeigt Aufladungen; nur drei akzeptierte Fish-Aktionen verbrauchen sie. Cooldown-Versuche dürfen keine Ladung abziehen.
6. Als Mod `!boss`, dann `!attack`; Boss-Cooldown und Buffverbrauch prüfen. Auto-Start nicht für den Test unbeabsichtigt verändern.

## Geänderte Dateien

- Bestehend: Config/AdventureGamesConfig.cs; Models/ViewerPointEntry.cs; Services/ViewerPointStore.cs, AdventureGameService.cs, CommandRegistry.cs; MainForm.cs, MainForm.AdventureGames.cs; docs/CHANGELOG.md.
- Neu: Config/RpgConfig.cs, RpgLootConfig.cs; Models/RpgProfile.cs; Services/ViewerPointStore.Rpg.cs, CharacterStatService.cs, RpgLootService.cs, RpgProgressionService.cs, RpgAdventureService.cs, RpgChatCommand.cs.
- Neue Tests: RpgPersistenceTests.cs, RpgProgressionTests.cs, RpgLootTests.cs, RpgAdventureIntegrationTests.cs; dieses Dokument enthält Plan, Konfiguration, Migration und Testanleitung.
