# Vampir-Maki

## Fangmöglichkeiten und Rezept

- `vampire_squid`: Vampirtintenfisch, Rare, Gebietslabel Tiefsee, relatives Gewicht 8, Standard-Fangbelohnung 300 Perlen.
- `tuna`: Thunfisch, Uncommon, Gebietslabel Offenes Meer, relatives Gewicht 25, Standard-Fangbelohnung 150 Perlen. Thunfisch war im bisherigen RPG-Fangpool nicht vorhanden und wurde ergänzt, damit das gewünschte Rezept tatsächlich herstellbar ist.
- Beide sind über kostenloses `!fish` und bezahltes `!fish <Einsatz>` fangbar, ohne neue Einsatzpflicht. IDs, Gewichte, Belohnungen und optionale Einsatzgrenzen sind im bestehenden Fangpool-Editor einstellbar. Relative Auswahl und FishingLuck funktionieren unverändert. Der vollständige Standardpool hat nun Gesamtgewicht 10.133.
- `!fishbag` zeigt die Zutaten. `!cook vampir` benötigt exakt 2× Vampirtintenfisch +1× Thunfisch und 750 Perlen. Der vorhandene atomare Crafting-Service bucht alle Zutaten und Perlen gemeinsam ab und schreibt einmalig ein Vampir-Maki gut. `!eat vampir` aktiviert es.

Vampir-Maki belegt den **defensiven** Slot. Ein offensives Sushi kann gleichzeitig aktiv sein; eine andere defensive Aktivierung überschreibt Vampir-Maki nicht. Einmaliger Verbrauch je Boss-ID und vollständige Wirkung bis zum Ende der dritten Teilnahme verwenden unverändert das Sushi-2.1-System.

## Lifeleech ohne feste HP-Obergrenze

1. Der bestehende Bossresolver berechnet den vollständigen Angriff einschließlich Ausrüstung, Kraft, Sushi, Perks, kritischem Treffer und möglichen Zusatztreffern.
2. `ActualDamage = min(Angriffsschaden, verbleibende Boss-HP)` schließt Overkill aus.
3. `RequestedHealing = floor(ActualDamage × BossLifeleech / 100)`, standardmäßig 5 %.
4. `NewHP = min(MaxHP, CurrentHP + RequestedHealing)`.
5. Erst anschließend erfolgt ein Gegenangriff, sofern der Boss noch lebt.

| Tatsächlich verursachter Schaden | Heilung bei 5 %, sofern genügend HP fehlen |
|---:|---:|
| 500 | 25 |
| 1.000 | 50 |
| 5.000 | 250 |
| 10.000 | 500 |

Es gibt **kein 25-HP-Cap und kein anderes festes Heilungscap pro Angriff**. Die einzige HP-Begrenzung ist die vorhandene MaxHP-Grenze. Ein kritischer Treffer mit doppelt so viel tatsächlichem Schaden heilt entsprechend mehr. Verbleiben dem Boss beispielsweise nur 500 HP, heilt selbst ein 10.000-Schaden-Angriff nur 25 HP: Das ist der Overkill-Ausschluss, kein Heilungscap.

Die Prozentkonfiguration ist auf gültige Werte von 0–100 % begrenzt; sie begrenzt nicht die absolute Heilungsmenge. Alte Buffs ohne dieses additive Feld haben 0 % Lifeleech. Ein aktivierter Buff speichert einen Snapshot; spätere Änderungen am Rezept verändern diesen nicht rückwirkend.

Spieler mit 0 HP oder mit Todessperre für denselben Boss erhalten keine Lifeleech-Heilung. Die bestehenden Teilnahmeprüfungen laufen außerdem vor der Schadensberechnung. Normale Regeneration darf weiterhin nach den bisherigen Regeln heilen, entfernt aber keine Boss-Todessperre. Der Lifeleech-Helper selbst regeneriert nicht und belebt niemanden wieder. Kleine Unterwassergegner rufen ihn nicht auf.

## Transaktionen und Anzeige

Lifeleech läuft innerhalb derselben vorhandenen Profiltransaktion wie Bossbeitrag, Rest-HP des Bosses, Gegenangriff, Cooldown und Sushi-Nutzung. Wiederholte Twitch-Events werden vor der Mutation abgewiesen. Speicherfehler rollen Heilung und Schaden gemeinsam zurück; es wird keine zweite Heilungstransaktion und kein zusätzlicher Timer eingeführt.

Kampfmeldungen nennen tatsächlich erhaltene Lifeleech-HP, wenn Heilung stattfand. Auch beim tödlichen Treffer gegen den Boss wird die tatsächliche Heilung in der Siegmeldung genannt. `!sushi`, Rezeptseiten und `!sushi status` zeigen Effekt beziehungsweise gespeicherten Prozentwert. HP werden über die vorhandenen `!hp`/`!stats` angezeigt.

RPG → Fischfang & Sushi → Rezepte & Buffs enthält **Boss-Lifeleech %**. Das vorhandene isolierte RPG Debug Center bietet unter HP & Überleben die Aktion **Boss-Lifeleech**: Der Zahlenwert ist der tatsächliche Boss-Schaden. Zuvor Vampir-Maki im Testkonto aktivieren und fehlende HP einstellen. Die Aktion ist nicht für Twitch-Zuschauer erreichbar.

## Verifikation

Vollständige Testsuite: **610 bestanden**, darunter 12 neue Fälle. Geprüft wurden die vier expliziten Schadens-/Heilungswerte, Crit, Overkill, MaxHP, Todessperre, fehlender Lifeleech gegen kleine Gegner, dritte Boss-Teilnahme, beide fangbaren Zutaten, Craftingkosten, defensive/offensive Slotkompatibilität, Effektsnapshot und Neustart sowie echte Bossangriffe mit Heilung vor dem Gegenangriff. Doppelte Angriffe und Speicherfehler können nicht mehrfach heilen.

Bestehende Fische, Sushi, Items, Guthaben und Spielstände werden nicht gelöscht oder zurückgesetzt. Neue Definitionen werden über den vorhandenen AddMissing-Pfad ergänzt, ohne bereits konfigurierte IDs zu überschreiben. Keine zusätzliche Datenbank und keine Streamer.bot-Abhängigkeit. Kein Twitch-Livetest oder visueller UI-Test durchgeführt; keine automatische Veröffentlichung.
