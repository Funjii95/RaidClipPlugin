# RPG Item-System 2.0

## Aktivierung und Erhalt vorhandener Daten

Unter Punkte & Minigames → RPG → Einstellungen: **Dynamische Items vorbereiten**, danach **RPG-Einstellungen speichern**. Der Knopf ergänzt fehlende Generierungsregeln für künftige Ausrüstungsfunde und fehlende Standard-Effektdefinitionen. Eigene vorhandene Regeln bleiben erhalten. Neue Standardkonfigurationen enthalten die Regeln bereits. Gespeicherte alte Konfigurationen ohne `Generation` erzeugen weiterhin feste Items, bis die Vorlage übernommen wird.

Bereits gefundene Items werden niemals neu gewürfelt. InstanceId, Inventarreihenfolge, Ausrüstung, Perlen, Fische, Sushi und Perks bleiben erhalten. Neue Modellfelder haben leere/Null-Standardwerte; die Profilversion bleibt 1, keine Datenmigration erforderlich. Vor der ersten RPG-Schreiboperation wird eine vorhandene Kontodatei einmalig als `viewer-points.json.pre-items-v2.bak` gesichert. Bestehende Sicherungen werden nicht überschrieben.

## Attribute und Berechnung

Die bestehenden internen Namen bleiben aus Kompatibilitätsgründen erhalten:

| Attribut | Wirkung | Gemeinsame Obergrenze |
|---|---|---|
| Damage (DamageBonus) | regulären Bosstreffer um Prozent erhöhen | DamageBonusCap, bisher 200 |
| BossDamage (BossDamageBonus) | weiterer Faktor auf Bossschaden | DamageBonusCap |
| CritChance (CriticalChance) | kritischer Treffer | CritChanceCap, bisher 50 % |
| CritDamage (CriticalDamage) | Zusatz zum Krit-Multiplikator | CritDamageCap, bisher 300 % |
| Luck, FishingLuck, DiveLuck | vorhandene gewichtete Fisch-/Lootberechnung | LuckCap, bisher je 100 |
| PearlBonus | nur positive Nettobelohnung erhöhen | PearlBonusCap, höchstens 15 % |
| DoubleAttackChance | ein Zusatztreffer | DoubleAttackChanceCap, höchstens 20 % |
| TripleAttackChance | zwei Zusatztreffer | TripleAttackChanceCap, höchstens 5 % |
| ExtraLootChance | ein zusätzlicher Dive-Itemwurf | ExtraLootChanceCap, höchstens 10 % |
| SushiPreservationChance | eine Aufladung schützen | SushiPreservationChanceCap, höchstens 20 % |

Alle Attribute werden aus einzigartig ausgerüsteten Instanzen, Perks und passenden aktiven Sushi-Buffs addiert und anschließend begrenzt. Passive Itemeffekte erhöhen die passende effektive Chance nur bei erfüllten Bedingungen und außerhalb ihres Cooldowns. `CharacterStatService.Calculate` akzeptiert dafür optional Zeitpunkt und tatsächlich gezahlten Einsatz. Ohne Einsatzkontext werden Mindesteinsatz-Effekte nicht als aktiv angezeigt. Effektchancen und direkte Attribute teilen denselben Cap; die Laufzeitberechnung zählt keinen Effekt doppelt.

LifeSteal, BlockChance und CooldownReduction wurden bewusst nicht ergänzt: Aktuell gibt es keine passenden Spieler-Lebenspunkte beziehungsweise Verteidigungsmechanik. Sie werden nicht als aktive Boni ausgegeben.

## Zufällige Ausrüstung konfigurieren

`DiveLoot.Items[].Generation` ist optional. Fehlt es, bleiben die festen `Attributes` der Definition maßgeblich. Mit Generation werden beim Fund die Attribute ersetzt, eindeutige Attributtypen ohne Zurücklegen ausgewählt und ganzzahlige Werte einschließlich beider Grenzen gewürfelt. Die vollständigen Ergebnisse werden in der Instanz gespeichert; `Generation` wird dort entfernt.

Beispiel für einen Common-Handschuh:

```json
"Generation": {
  "Attributes": [
    { "Attribute": "Damage", "Minimum": 1, "Maximum": 3 },
    { "Attribute": "CritChance", "Minimum": 1, "Maximum": 2 }
  ],
  "EffectIds": [],
  "DoubleAttackMinimumChance": 2,
  "DoubleAttackMaximumChance": 4
}
```

Common erhält 1 Attribut/0 Effekte, Uncommon 1–2/0, Rare 2–3/0–1, Epic 3–4/1, Legendary 4–5/1. Der Attributpool muss mindestens die jeweilige maximale Anzahl enthalten. Zulässig sind die tatsächlichen Eigenschaftsnamen aus der Tabelle, Werte 1–1000; Caps begrenzen die Wirkung. Diese Regeln gelten nicht rückwirkend für Legacy-Items. Verbrauchsgegenstände und Schätze ohne Slot werden nicht dynamisch ausgerüstet.

Die Standardpools sind nach Slot spezialisiert: Waffen auf Schaden/Krit, Helme auf Glück/Abenteuer, Brust auf Kampf, Handschuhe auf Krit/Schaden, Schuhe auf Abenteuer/Perlen, Ringe auf Krit/Glück und Amulette auf Glück/Perlen. Für höhere Seltenheiten ergänzen sie weitere aktive Attribute. Standardwerte reichen von 1 bis `2 + 2 × Seltenheitsindex` (Common = 0). Die acht vorhandenen Slots einschließlich zweier Ringplätze bleiben unverändert.

## Zentrale Effektdefinitionen

`DiveLoot.Effects[]` enthält strukturierte Definitionen mit `Id`, `Name`, `Trigger`, `Chance` (Prozent), `Strength` (0 < Wert ≤ 1), `MinimumStake`, `CooldownSeconds` (0–86400) und optional `Activity`. Standard-IDs: `double`, `triple`, `extra-loot`, `sushi-save`. `Generation.EffectIds` enthält die möglichen Definition-IDs. Eine Instanz speichert einen unabhängigen Snapshot; Änderungen an der Registry gelten nur für spätere Funde.

- **DoubleAttack:** Standard-Zusatzschaden 50 % des vollständig berechneten regulären Treffers einschließlich Krit. Bei Generierung kommt die Chance aus `DoubleAttackMinimumChance`/`DoubleAttackMaximumChance`: Standard Rare 2–4, Epic 4–7, Legendary 7–12. Nur Bossaktivität, kein Mindesteinsatz.
- **TripleAttack:** nur für legendäre Generierung, konfigurierte Chance höchstens 3 %, standardmäßig 2 %. Zwei Zusatztreffer mit je `Strength` des regulären Treffers. Hat Vorrang; nur wenn Triple nicht auslöst, wird Double geprüft. Kein rekursiver Proc, keine zusätzliche Bossaktion oder Belohnung und nur eine reguläre Buff-Verarbeitung.
- **ExtraLoot:** Standard 3 %, nur Dive. Ein zusätzlicher vollständiger Itemwurf mit derselben Lootchance und demselben tatsächlich gezahlten Einsatz; dieser Wurf kann leer ausgehen. Legendary-Mindestgrenze und Rarity-Gating gelten vor der Auswahl für beide Würfe. Kein weiterer Extra-Loot-Proc und keine erneute Dive-Auszahlung.
- **SushiPreservation:** Standard 5 %. Je aktivem, zur Aktion passendem Buff eine Schutzprüfung in gespeicherter Reihenfolge. Ein Buff kann höchstens einmal hintereinander geschützt werden; auf eine geschützte Aufladung folgt zwingend ein Verbrauch. Damit hält ein Buff selbst bei ausschließlich erfolgreichen Zufallswürfen höchstens doppelt so viele Aktionen. Der Schutzmarker wird persistent gespeichert. Effekte mit Cooldown können nach einer Auslösung für weitere Buffs derselben Aktion gesperrt sein.

Pro Auslösung wird genau ein beitragender Effekt proportional zu seiner Chance gewählt; dessen Stärke und eigener Instanz-/Effekt-Cooldown gelten. Gesamtchancen sind gedeckelt. Schutz und Extra-Loot nutzen `Strength` nicht als Mengenmultiplikator: immer höchstens eine Aufladung beziehungsweise ein Itemwurf. Keine Endlosschleifen.

Perlenformel: `Auszahlung + floor(max(0, Auszahlung − Einsatz) × begrenzter PearlBonus / 100)`. Verlust, Einsatzrückzahlung und der Einsatz selbst erhalten keinen Bonus. Keine Änderung an Einsatzstufen, Lootgewichten oder eigentlichen Basisauszahlungen. Der Perlenbonus bleibt ein begrenzter Faktor auf den vorgesehenen Gewinn, nicht eine zusätzliche Einsatz-/Loot-Rückkopplung.

## Chat und spätere Extension

`!inv`, `!item <Nummer>`, `!equip <Nummer>` und `!gear` bleiben erhalten. Übersichten zeigen Effektname und tatsächliche Attribute, Itemdetails zusätzlich vollständige automatisch erzeugte Effektbeschreibung, Wahrscheinlichkeit, Stärke, Bedingungen und Cooldown. Texte werden aus Instanzdaten erzeugt, nicht aus manuell gepflegten Zahlen. Lange Details sind weiterhin über Seiten abrufbar. Zusätzliche Dive-Funde werden in derselben Ergebnisantwort genannt; keine Extra-Nachrichtenserie.

Die späteren Extension-Adapter können dieselben serialisierbaren Modelle und Services verwenden: `RpgItem.Attributes`, `Effects`, InstanceId sowie `RpgAdventureResult.AdditionalItems`. Es wurde kein alternativer Inventar- oder Belohnungsspeicher eingeführt.

## Prüfung

480 Tests erfolgreich, einschließlich neuer reproduzierbarer Tests mit injiziertem RNG für Attributgrenzen, Seltenheitsanzahl, Persistenz, Legacy-Erhalt, Crit/Double/Triple/Priorität, Cooldowns/Bedingungen, gemeinsame Caps, finite Sushi-Erhaltung, Perlenrückzahlung und Extra-Loot mit Legendary-Gating. Tests ausschließlich mit temporären Kontodateien, kein Live-Spielstand verändert. Keine automatische Veröffentlichung.
