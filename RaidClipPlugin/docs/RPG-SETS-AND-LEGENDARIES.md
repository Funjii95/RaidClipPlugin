# Sets und legendäre Ausrüstung

## Aktivieren

RPG-Einstellungen → **Sets & Legenden vorbereiten** → **RPG-Einstellungen speichern**. Für bestehende Konfigurationen werden nur fehlende Set-/Itemdefinitionen ergänzt, keine eigenen Definitionen überschrieben. Neue Standardkonfigurationen enthalten den Katalog bereits. Kein Inventar wird verändert oder rückwirkend einem Set zugeordnet.

Der vorhandene dynamische Generator, Lootservice, Progressionsservice, Perlenberechnung und Proc-Service bleiben maßgeblich. Es gibt keinen zweiten Inventarspeicher und keine Neuwürfelung beim Laden.

## Sets

Beide Sets haben je vier epische Teile: Helm, Brust, Handschuhe und Schuhe. Pro Fund werden genau die drei konfigurierten Attribute unabhängig innerhalb ihrer Grenzen gewürfelt (`Generation.AttributeCount = 3`, `GenerateEffect = false`). Der Attributumfang ist bewusst eine benannte Katalogausnahme von den generischen Seltenheitsvorlagen; es werden keine unerwähnten Zufallseffekte hinzugefügt.

| Set-ID | Teile | Kumulative Boni |
|---|---|---|
| deepsea-guardian | Krone des Abgrunds, Panzer des Leviathans, Griff der Tiefsee, Schritte des Abgrunds | 2: +5 % Boss-Schaden; 3: +3 % Crit Chance; 4: +5 % Doppelangriff |
| ocean-treasure-hunter | Maske des Perlensuchers, Gewand des Schatzsuchers, Finger des Glücks, Flossen des Entdeckers | 2: +5 Dive Luck; 3: +5 Fishing Luck; 4: +5 % Perlenbonus |

Die Attributbereiche entsprechen dem Auftrag und stehen vollständig in `DiveLoot.Items[].Generation.Attributes`. Stabile Definition-IDs: `deepsea-crown`, `deepsea-chest`, `deepsea-grip`, `deepsea-boots`, `ocean-mask`, `ocean-garment`, `ocean-fingers`, `ocean-flippers`.

Setdefinitionen unter `Rpg.Sets[]`: `Id`, `Name`, `Description`, `ItemIds`, `Bonuses[]` mit `RequiredPieces` und `Attributes`. Iteminstanzen tragen optional `SetId`; alte Items standardmäßig eine leere Zeichenfolge. Nur Instanzen, die tatsächlich ausgerüstet sind, die passende Set-ID tragen und zur deklarierten Itemliste gehören, zählen. Mehrere Exemplare derselben Itemdefinition zählen einmal. Verschiedene Sets werden getrennt ausgewertet.

`CharacterStatService` addiert alle freigeschalteten Boni nach Perks/Ausrüstung/Sushi und vor den gemeinsamen Caps. Kein Bonus wird dauerhaft ins Profil kopiert: Ausrüsten, Ersetzen oder Ablegen ändert die Wirkung unmittelbar. Der Perlenbonus gilt weiterhin ausschließlich für positive Nettobelohnungen, niemals für Einsatzrückzahlung.

## Eigenständige Legendarys

Diese fünf Definitionen haben die festen Werte aus dem Auftrag und keine zufällige Generation:

| ID / Item | Werte | Effekt |
|---|---|---|
| leviathan-grip / Leviathans Griff | +8 % Schaden, +4 % Crit, +15 % Krit-Schaden, +5 % Boss-Schaden | 10 %: ein Zusatztreffer mit 50 % Schaden |
| tidal-seal / Siegel der Gezeiten | +5 % Crit, +10 % Krit-Schaden, +5 Luck | 3 %: nur bei regulärem kritischem Treffer ein Zusatztreffer mit 50 % Schaden |
| deepsea-heart / Herz der Tiefsee | +7 Luck, +5 Dive Luck, +5 Fishing Luck | 8 % Sushi-Erhaltung mit vorhandener Endlichkeitsregel |
| sunken-king-crown / Krone des versunkenen Königs | +8 Luck, +7 Dive Luck, +5 Fishing Luck | 5 %: zusätzlicher Dive-Itemwurf, gleiches Legendary-Gating |
| leviathan-trident / Dreizack des Leviathans | +10 % Schaden, +5 % Crit, +20 % Krit-Schaden, +8 % Boss-Schaden | 2 %: zwei Zusatztreffer mit je 35 % Schaden |

`Effects[].RequiresCritical` ist die neue strukturierte Bedingung für das Siegel. Reguläre Charakterwerte zählen diese bedingte Chance nicht als immer aktiv. Die Trefferberechnung prüft sie anhand des tatsächlich gewürfelten regulären Crits. Triple hat weiterhin Vorrang vor Double; maximal zwei Zusatztreffer, keine rekursiven Auslösungen, keine zusätzlichen Bossaktionen, Buff-Verbräuche oder Perlenbelohnungen. Die vorhandene Ringautomatik unterstützt beide Ringplätze.

## Dropquellen und Konfiguration

`DiveLoot.Items[].DropActivities` enthält die erlaubten Quellen (Standard alter Items: nur Dive).

- **Dive:** Beide Sets und alle fünf Legendarys. Set-Epic frühestens gemäß vorhandener Epic-Einsatzstufe; Legendarys weiterhin ausschließlich oberhalb der unveränderten `LegendaryMinimumStake` und entsprechender RiskTier-Freigabe. Auch Extra-Loot filtert vor der Auswahl nach diesen Grenzen. Vorhandene Gesamt-Dropchancen und Rarity-Gewichte bleiben unverändert; innerhalb einer Seltenheit werden passende Definitionen gleichmäßig gewählt.
- **Boss:** Wächterteile und die fünf Legendarys. `BossLootChance` standardmäßig **2 % pro Teilnehmer und besiegtem Boss**. Maximal ein Gegenstand je Teilnehmer. Legendarys sind standardmäßig gesperrt (`BossLegendaryEnabled = false`). Eine explizite Freigabe braucht zusätzlich mindestens `BossLegendaryMinimumHp = 1000` anfängliche Boss-LP und `BossLegendaryMinimumPlayers = 3` unterschiedliche Teilnehmer. Ohne alle Bedingungen maximal Epic. Es wird kein fiktiver Dive-Einsatz verwendet. Boss-Perlenbelohnungen bleiben unverändert.
- **Fishing:** Schatzjägerteile mit `FishingEquipmentChance = 0.5 %`, nur zusätzlich zu einem erfolgreichen Fischfang, maximal ein Ausrüstungsteil und höchstens Epic. Vorhandene Fisch- und Perlenbelohnungen bleiben unverändert. Extra-Dive-Loot löst hier nicht aus.

Boss- und Angel-Loot benutzen dieselbe Auswahl-/Instanzgenerierung wie Dive, aber explizite separate Quellen und Seltenheitsgrenzen. Boss-Loot ist innerhalb der vorhandenen Kontotransaktion mit `bossloot:<Begegnungs-ID>` idempotent, auch bei einer wiederholten Belohnungsanforderung. Kontolimit und Basis-Bossgewinn werden nicht durch Itemloot verändert.

Konfigurierbare Chancen: 0–100 %. Boss-Legendary-Mindest-LP und Mindestspielerzahl müssen positiv sein. Der bestehende Button für generische dynamische Items überspringt feste Items mit Spezialeffekten, damit diese fünf Legendarys nicht versehentlich neu definiert werden.

## Chat und Persistenz

- `!inv` und `!gear`: Setname und Verweis auf die Setübersicht; Ausrüstung enthält zusätzlich aktuellen Setfortschritt und Bonuszustände auf Folgeseiten.
- `!item <Nummer> [Seite]`: vollständige Instanzattribute, Effekte, Setname sowie jeden tatsächlichen Bonus mit **Aktiv** oder **Noch nicht freigeschaltet**.
- `!sets [Seite]`: alle Sets, Fortschritt, aktive/gesperrte Boni und jedes Teil mit **ausgerüstet**, **im Inventar** oder **fehlt**.
- `!unequip <Slot>`: Ausrüstungsplatz leeren, z. B. `!unequip Helmet` oder `!unequip Ring2`. Das Item bleibt im Inventar; Setboni werden automatisch neu berechnet.

IDs, Instanzattribute und Set-Zugehörigkeit werden über die vorhandene JSON-Persistenz gespeichert. Bestehende Backups und Schutzmechanismen bleiben aktiv. Die zukünftige Extension kann Sets, Instanz-SetId, Attribute, Effekte und Fortschritt über dieselben strukturierten Modelle und Services nutzen.

Keine automatische Veröffentlichung. Kein Zurücksetzen produktiver Daten.
