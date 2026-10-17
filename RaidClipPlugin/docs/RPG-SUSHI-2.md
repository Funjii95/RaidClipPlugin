# Sushi 2.1 – Implementierung und Bedienung

## Bestand und Architektur

Der untersuchte Stand hatte aktives Angeln mit `!fish <Einsatz>`, fünf RPG-Fischarten, stapelbare Zutaten, Einzelzutaten-Crafting und Buff-Verbrauch pro Aktion. Die im Auftrag vorausgesetzten drei Boss-Teilnahmen waren dort noch nicht implementiert. Diese Erweiterung nutzt denselben `ViewerPointStore`, `RpgAdventureService`, `RpgProgressionService`, `AdventureGameService` und dieselbe Chatregistrierung; keine neue Datenbank oder Twitch-Integration.

`RpgFishingService` zentralisiert die bisherige gewichtete Auswahl. `RpgSushiService` ergänzt Rezepte, Slots, Bossverbrauch, Effekte und additive Migration. Typisierte Ergebnisobjekte tragen die tatsächlich verbrauchten Zutaten und Buffwerte in die Chatausgabe.

## Fangbare Zutaten

| ID | Name | Seltenheit | Gewicht | Neue Fangbelohnung | Gebietslabel |
|---|---|---|---:|---:|---|
| sea_cucumber | Seegurke | Common | 50 | 40 Perlen | Flachwasser |
| shrimp | Garnele | Uncommon | 25 | 100 Perlen | Korallenriff |
| giant_shrimp | Riesengarnele | Rare | 12 | 250 Perlen | Tiefsee |
| pufferfish | Kugelfisch | Rare | 10 | 350 Perlen | Tiefsee |
| golden_pufferfish | Goldener Kugelfisch | Epic | 3 | 800 Perlen | Abgrund |

Die bisherigen Gewichte 6500/2500/800/190/10 bleiben bestehen. Der Standardpool erfolgreicher Fänge hat ohne Luck nun Gewicht 10.100. Die neuen relativen Gewichte sind **keine zusätzlichen globalen Prozente**: beispielsweise gilt für Seegurke `50/10100` unter den erfolgreichen Fängen. Der normale Erfolg-/Fehlschlagwurf des Angelspiels bleibt unverändert. Rare+ werden weiter durch den bestehenden FishingLuck-/Luck-Gewichtsfaktor beeinflusst, niemals durch Addition absoluter Prozentpunkte.

Da Fischfang unabhängig von den Dive-Zonen ist, sind alle neuen Arten standardmäßig auch ohne Einsatz fangbar. `Zone` ist ein beschreibendes Gebietslabel; `MinimumStake` ist optional einstellbar (Standard 0). Es wird keine neue Einsatzpflicht erfunden. Ein Goldener Kugelfisch benötigt anders als legendäre Ausrüstung keinen Legendary-Einsatz.

Bei neuen Arten wird die bisherige Fang-Auszahlung durch **Einsatzrückgabe plus konfigurierte Fangbelohnung** ersetzt, nicht um eine zweite Belohnung ergänzt. Alte Arten behalten bei bezahlten Versuchen die vorhandenen Multiplikatoren. Bei kostenlosen erfolgreichen Fängen alter Arten gibt es die einstellbare `FreeFishingReward` (Standard 40). Bestehende Perlenbonus-/Hot-Time-Regeln wirken einmal auf positive Belohnungen, nicht auf den zurückgegebenen Einsatz. Die neuen Arten werden auch in der alten Fangsammlung unter ihrem tatsächlichen Namen erfasst; der RPG-Zutatenbestand wird genau einmal erhöht.

## Commands

- `!fish`: kostenloser Fangversuch, weiterhin mit vorhandener Fehlfangchance und persönlichem Angelcooldown.
- `!fish <Einsatz|all>`: bisheriges bezahltes Angeln bleibt verfügbar.
- `!fishbag [Seite]`: tatsächlicher, dauerhaft gespeicherter Zutatenvorrat mit stabilen IDs.
- `!sushi`: Status, Vorrat und Rezepte. Weitere Rezeptseiten über `!sushi recipes <Seite>`, reine Statusseiten über `!sushi status [Seite]`.
- `!cook <Rezept>` ist Alias für `!craft <Rezept>`.
- `!eat <Rezept>` ist Alias für das weiterhin gültige `!sushi <Rezept>`.
- `!stats` nennt beide Slots und verbleibende Boss-Teilnahmen; `!hp` zeigt wirksame passive und Chat-Heilungswerte. Lange Antworten verwenden die bestehende sichere Seitendarstellung.

## Neue Rezepte

| Rezept-ID | Rezept | Verbrauch | Effekt |
|---|---|---|---|
| kappa | Kappa-Maki | 2 Seegurken +300 Perlen | +3 HP pro Minute passiv |
| tempura | Garnelen-Tempura-Rolle | 2 Garnelen +1 Riesengarnele +450 Perlen | +5 HP je gültiger Chat-Heilung |
| kugelfisch | Kugelfisch-Sashimi | 2 Kugelfische +700 Perlen | 15 % weniger großer Boss-Gegenschaden |

Alle drei sind defensiv und haben standardmäßig drei Boss-Teilnahmen. Der Goldene Kugelfisch ist ein lagerbarer künftiger Rezeptbestandteil, erhält vorerst kein zusätzliches Rezept. Bestehende Rezeptkosten und Attribute werden nicht geändert. Multi-Zutaten stehen in `Ingredients`; leere Listen verwenden rückwärtskompatibel `FishId/FishQuantity`.

## Slots und Bossverbrauch

Ein offensiver und ein defensiver Slot können gleichzeitig belegt werden. Ein belegter Slot kann nicht überschrieben werden; die abgelehnte Aktivierung verbraucht kein Sushi. Attribute und defensive Effekte werden bei Aktivierung als Snapshot gespeichert. Neue Nutzungsdauer steht pro Rezept in `BossUses`; 0 übernimmt für alte Rezepte die bestehende globale BuffUses-Einstellung, standardmäßig 3.

Der erste gültige Angriff auf einen Boss verbraucht einmalig eine Nutzung **beider** aktiven Kategorien. Weitere Angriffe auf dieselbe Boss-ID verbrauchen nichts. Buffs gelten vor der ersten Teilnahme und zwischen den Teilnahmen. Beim dritten Boss sinkt die Anzeige auf 0, der Buff bleibt aber bis zum gespeicherten Rundenende bzw. zum vorzeitigen Sieg/Abbruch vollständig aktiv. Auch Tod und Timeout zählen als Teilnahme. Nichtteilnahme zählt nicht. Erhaltungseffekte verlängern die encounter-basierten Sushi nicht über die festgelegte Anzahl hinaus.

Alle echten Buff-Abschlusswege rechnen Regeneration vor dem Entfernen ab. Der gespeicherte Endzeitpunkt begrenzt den letzten Buff auch nach einem Neustart; vergangene Rundenintervalle werden nicht nachträglich mit einem neuen Buff aufgefüllt. Offensivbuffs wirken weiterhin in ihrer jeweiligen Aktivität (Boss/Fishing/Dive), ihre neue Dauer zählt Boss-Teilnahmen.

## Heilung und Schaden

- Kappa-Maki: normale 2 HP/Minute plus 3, insgesamt 5. Aktivierung rechnet zuvor verstrichene volle Intervalle zuerst ohne neuen Bonus ab. Zusätzliche Regeneration wird nur für die Überschneidung des abgerechneten Intervalls mit Aktivierungs-/Endzeit berechnet. Ganze HP werden abgerundet, Bruchteile gespeichert; damit geht bei kurzen Intervallen oder häufigen Abfragen keine Heilung verloren. Kein Bonus für Zeit vor der Aktivierung oder nach dem Ende. Die vorhandene passive Intervall-/Offline-Nachholgrenze und MaxHP gelten weiter.
- Tempura: normale 10 Chat-HP plus 5, insgesamt 15. Derselbe persönliche 30-Sekunden-Cooldown, Command-/Bot-Ausschluss und persistenter Event-Wiederholungsschutz bleiben erhalten. Keine zusätzliche Chatnachricht pro Heilung.
- Kugelfisch: großer Boss-Gegenschaden `ceil(Rohschaden * (1 - Reduktion/100))`, Reduktion sicher auf 80 % begrenzt. Beispiel 40 →34; 10 →9. Kleine Unterwassergegner erhalten keine Reduktion. Der letzte tödliche Spielertreffer gegen den großen Boss erzeugt weiterhin keinen Gegenangriff.

Heilung kann HP erhöhen, entfernt aber niemals eine bestehende Todessperre. Besiegte erhalten weiter keine Bossperlen, EXP oder Items; lebende Teilnehmer müssen nicht dauerhaft weiter angreifen.

## Oberfläche und Debugging

RPG → **Fischfang & Sushi** enthält Tabellen für Fangpool und Rezepte. Bestehende IDs sind schreibgeschützt. Gewicht 0 deaktiviert eine Art; Anzeigename, Seltenheit, Gewicht, optionale eigene Perlenbelohnung, Mindesteinsatz und Gebiet sind editierbar. Ein leeres Perlenfeld verwendet die alte Auszahlung. Rezeptfelder enthalten Kategorie, Einzel-/Mehrfachzutaten (`shrimp:2;giant_shrimp:1`), Perlenkosten, drei defensive Effektwerte und Boss-Teilnahmen. Neue Zeilen sind möglich; ungültige Daten werden vor dem Speichern abgewiesen. Die bisherigen offensiven Attribute bleiben im vorhandenen vollständigen RPG-Einstellungseditor editierbar.

Das isolierte, nur lokal für Administratoren verfügbare RPG Debug Center kann die neuen Arten über „Fische erzeugen“ vergeben, Vorräte anzeigen und alle neuen Rezepte herstellen/aktivieren. HP-Tests berücksichtigen die defensiven Effekte. „Boss-Teilnahme“ verwendet den Zahlenwert als Test-Boss-ID: derselbe Wert zählt nicht mehrfach, verschiedene Werte zählen verschiedene Testbosse.

## Persistenz und Migration

Bestehende Fischbestände, Sushi-Vorräte, IDs, Perlen und EXP bleiben erhalten. Fehlende Definitions-IDs werden beim Konfigurationsladen ergänzt; vorhandene Definitionen werden nicht überschrieben. Aktive Altbuffs übernehmen ihre verbleibenden Aufladungen und werden einmalig auf Boss-Teilnahmen umgestellt. Bereits vorhandene mehrere Offensivbuffs werden nicht gelöscht; neue offensive Aktivierungen warten, bis deren Slot frei ist.

Kategorie, Nutzungen, Aktivierungszeit, letzte Boss-ID, Ablaufzeit sowie tatsächliche defensive Effekte werden in der bestehenden Buffliste gespeichert (`SushiVersion=1`). Kein paralleler Buffspeicher. Vor dem ersten RPG-Schreibvorgang entsteht zusätzlich `.pre-sushi-2.bak`; Fischfang und Crafting bleiben in den bestehenden atomaren Transaktionen. Speicherfehler und doppelte Requests erzeugen weder zusätzliche Zutaten noch zusätzliche Perlen.

## Verifikation und Grenzen

Vollständige Testsuite: **598 bestanden**, davon 19 neue Sushi-2.1-Fälle. Abgedeckt sind alle fünf Fänge und Belohnungen, relative Gewichte/Perks, drei Rezepte, unvollständige Zutaten/fehlende Perlen, parallele Crafting-Requests, zwei Slots und Neustarts, dritter Boss samt Mehrfachangriffen, zeitlich getrennte und bruchteilgenaue Regeneration, Chat-Heilung, Rundung und Ausschluss kleiner Gegner, Altbuff-Migration, echte Bossangriffe und kostenlose Chat-Fänge sowie Speicher-Rollback.

Keine Live-Verbindung zu Twitch oder visuelle Bedienprüfung der Windows-Oberfläche wurde für diesen Auftrag ausgeführt. Die Gebietslabels erzwingen ohne optionalen Mindesteinsatz keine Dive-Zone. Bestehende Sonderwerte für Buffdauer werden respektiert statt zurückgesetzt. Keine automatische Veröffentlichung.
