# Kraft und Ausdauer als garantierte Ausrüstungsbasis

Jedes ausrüstbare Item (auch Legacy-Items mit Slot, aber ohne `Kind=Equipment`) erhält zwei zusätzliche Grundattribute. Schätze, Fische und Sushi sind keine Ausrüstung. Die bisherigen Zufallsattribute und Effekte bleiben erhalten und werden nicht durch die Grundattribute ersetzt.

| Seltenheit | Kraft | Ausdauer | HP aus Ausdauer (Standard) |
|---|---:|---:|---:|
| Common | 1 | 1 | 5 |
| Uncommon | 2 | 2 | 10 |
| Rare | 3 | 4 | 20 |
| Epic | 5 | 7 | 35 |
| Legendary | 8 | 10 | 50 |

Es existiert derzeit kein separates Itemlevel; die Baseline richtet sich daher nach der tatsächlichen Seltenheit, ohne ein zweites Qualitätssystem zu erfinden.

## Wirkung

- Nur angelegte, eindeutige Instanzen wirken. Zwei verschiedene Ringe wirken beide; eine doppelt referenzierte Instanz zählt nur einmal.
- Kraft wird in `CharacterStatService` genau einmal zum allgemeinen Schaden addiert, standardmäßig 1 Prozentpunkt pro Punkt. `RpgConfig.PowerDamagePerPoint` steuert die Umrechnung; das bestehende Schadenscap gilt weiter. Der bisherige Boss-Schadensfaktor und Crit bleiben unverändert.
- Ausdauer erhöht MaxHP, standardmäßig um 5 HP je Punkt. `Health.HpPerStamina` ist im HP-Einstellungsbereich verfügbar. Direkte bestehende `MaximumHp`-Attribute bleiben als zusätzlicher Bonus bestehen. Die Summe unterliegt `Health.EquipmentHpCap` (Standard 1.000).
- Anlegen/Migration heilt verwundete Spieler nicht. Ablegen reduziert bei Bedarf aktuelle HP auf das neue Maximum; Wiederanlegen füllt nichts auf. Levelheilung wird separat aus dem Basis-MaxHP-Zuwachs berechnet. Todessperren werden nicht entfernt.
- Kraft, Ausdauer und direkte HP-Boni werden deutsch in Itemdetails, Ausrüstungsantworten und Charakterwerten dargestellt. Bei effektiven Charakterwerten enthält der angezeigte Schaden bereits den Kraftanteil.

## Erzeugung und alte Spielstände

`RpgCoreStatService` ergänzt Werte deterministisch pro Seltenheit. Dynamische Items erhalten die beiden Werte nach ihren regulären Zufallswürfen; sie zählen nicht zur bisherigen Anzahl zufälliger Attribute. Feste Items werden ebenfalls ergänzt. Es werden keine zusätzlichen Zufallsaufrufe benötigt.

`CoreStatsVersion=1` an der Iteminstanz verhindert erneute Ergänzung. Bereits positive manuell konfigurierte Kraft-/Ausdauerwerte bleiben erhalten. Der autoritative Store ergänzt alte Instanzen auf dem abgetrennten Transaktionsentwurf, auch wenn der frühere Fischimport bereits abgeschlossen war. Lesende Profilabfragen ergänzen nur ihre Rückgabekopie. Ein späterer erfolgreicher RPG-Commit persistiert die Ergänzung gemeinsam mit allen anderen Änderungen; Fehler ändern den originalen Kontozustand nicht.

Vor dem ersten RPG-Schreibvorgang wird eine zusätzliche `viewer-points.json.pre-core-stats.bak` angelegt, ohne bestehende Sicherungen zu überschreiben. Inventar-IDs, Sekundärwerte, Effekte, Setzugehörigkeiten, Ausrüstungsplätze, Perlen und EXP bleiben unverändert. Es gibt kein Neuwürfeln bestehender Instanzen.

Die zusätzliche bisherige HP-Basisausrüstung bleibt verfügbar; ihre direkten HP-Boni kommen zur Ausdauer hinzu. Legendary-Einsatzgrenzen und Dropchancen bleiben unverändert. Keine Veröffentlichung ohne Auftrag.
