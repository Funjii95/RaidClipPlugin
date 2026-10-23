# Tiefen-Erweiterung · Desktop 0.3.0 / Plugin 2.3.0

## Inhalt

54 neue Ausrüstungsteile: 18 Wahre Setteile, 18 Abyss-Setteile, zwölf einzelne Abyss-Schmuckstücke und sechs verfluchte Gegenstände. Die sechs neuen Sets verwenden ausschließlich die vorhandenen Ausrüstungsplätze. Zwei gleiche Ringdefinitionen zählen weiterhin nur als ein Setteil.

Elf stapelbare Materialien liegen im bestehenden `RpgMaterials`-Vorrat. Farbige Perlen sind Materialien; normale Perlen bleiben Währung. Alte Vorräte behalten ihre Mengen, ergänzte Felder beginnen bei null. Bestehende Iteminstanzen und ihre ursprünglichen Attribute werden nicht ersetzt oder neu ausgewürfelt.

Die 81 WebP-Einzelbilder wurden aus den acht bereitgestellten Bildtafeln ausgeschnitten: 54 neue Ausrüstungsteile, elf Materialien und 16 vorhandene Setteile. `expansions/depths/sources` enthält die Originale; `depths-art.json` dokumentiert Quellenhash, Ausschnitt und Dateihash. `extract_art.py` erstellt die Ausschnitte reproduzierbar. Es fehlen keine Bilder für diese Erweiterung. Die früheren Gezeiten-Items behalten ihre bisherigen Platzhalter, sofern keine passende Einzelillustration vorlag.

Der vollständige Katalog steht in `expansions/depths/ITEMS.md` und `catalog.json`. Die vorhandenen vier alten Sets behalten ihre bestehenden Namen, Seltenheiten und konfigurierten Effekte; ihre Bilder werden zugeordnet, ohne alte Spielstände umzuschreiben.

## Aufwertungen

| Ziel | Normale Perlen | Verstärkungskerne | Farbige Perle | Bonus auf ursprüngliche positive Werte |
|---|---:|---:|---|---:|
| +1 | 100 | 0 | — | 3 % |
| +2 | 250 | 0 | — | 6 % |
| +3 | 500 | 0 | — | 9 % |
| +4 | 1.000 | 0 | — | 12 % |
| +5 | 2.000 | 1 | — | 17 % |
| +6 | 3.500 | 2 | — | 22 % |
| +7 | 5.000 | 3 | 1 weiße | 28 % |
| +8 | 8.000 | 4 | 1 blaue | 35 % |
| +9 | 12.000 | 5 | 1 rote | 45 % |

Ab +7 kommen 2/3/5 Perlenstaub für Common/Uncommon/Rare hinzu, beziehungsweise 1/2/3 Flossenschuppen für Epic, Runenmuscheln für Legendary, Korallenkerne für True, Seidenfäden für Cursed oder Abyss-Scherben für Abyss. Common verwendet mangels abweichender Vorgabe ebenfalls Perlenstaub.

Die einmalige Veredelung eines +9-Items kostet 15.000 normale Perlen und einen `eternal_depth_core`. Der Gesamtbonus steigt von 45 auf 50 %, die Stufe bleibt +9. Jede Aufwertung und Veredelung ist garantiert erfolgreich. Es gibt keinen Erfolgswurf, keine Zerstörung, Rückstufung oder neuen Zufallsattribute.

Berechnet wird immer aus den unveränderten ursprünglichen Attributen. Erhöhte Werte werden nicht erneut als Basis verwendet. Positive aufgewertete Werte werden deterministisch auf vier Dezimalstellen gerundet; +0-Werte und negative Fluchwerte bleiben exakt erhalten. Vorhandene Grenzen gelten bei der Zusammenführung der Kampfwerte. Setboni und einzigartige Effekte werden nicht verstärkt.

Besitz, erwartete aktuelle Stufe, Materialien und Perlen prüft das Plugin in derselben gespeicherten Transaktion. Aktionsbelege und `CompletedActions` verhindern Mehrfachbuchungen nach Netzwerkfehlern. Ein fehlgeschlagener Schreibvorgang setzt den gesamten Entwurf zurück.

## Materialfunde und Balancing

Die vollständigen Standardchancen und Mengen stehen unter `upgrades` in `expansions/depths/catalog.json`; sie entsprechen dem gelieferten Prompt. Quellen: Tauchen, Angeln, besiegte kleine Tauchgegner, Boss-Siegbelohnungen und Tauchtruhen. Das vorhandene Tauchergebnis unterscheidet kleine Truhe, Schatzkammer und Königsschatz; diese verwenden normale, seltene und legendäre Truhenwerte. Muscheln bleiben das bestehende separate System. Es wurden keine zusätzlichen Truhen- oder Mimic-Spawns eingeführt.

Jede berechtigte besiegte Mimic hat genau einen 50-%-Wurf. Bei Erfolg folgt ein gewichteter Materialwurf: Rot 30, Runenmuschel 25, Seidenfaden 18, Korallenkern 15, Abyss-Scherbe 10, Tiefenkern 2. Hot Time verändert weder die 50-%-Chance noch die Gewichte. Sonstige Materialchancen erhalten den vorhandenen Dropbonus einmalig und höchstens bis 100 %.

Tote oder nach bestehenden Regeln nicht berechtigte Boss-/Mimic-Teilnehmer erhalten keine Materialien. Die Funde werden innerhalb des bestehenden idempotenten Belohnungsvorgangs gespeichert. Zusätzliche Ausrüstung verwendet einen getrennten Pool, damit die bisherigen Itemwahrscheinlichkeiten erhalten bleiben. Startchancen pro berechtigter Aktivität: True 0,25 %, Cursed 0,4 %, Abyss 0,1 %. Abyss-Ausrüstung setzt einen besiegten Boss/Mimic mit maximalen HP mindestens in Höhe der vorhandenen `Health.LegendaryThreshold` voraus. Diese Startwerte sind im Adminbereich anpassbar.

## Seteffekte und bestehende Systeme

Neue Seteffekte verwenden die vorhandenen autoritativen Kämpfe, Gesundheitswerte und gespeicherten Abklingzeiten. Procs lösen keine weiteren Procs aus. Heilung basiert auf tatsächlichem Schaden beziehungsweise maximalen HP, Schilde werden nicht addiert. Die Blutdynastie-Rettung kann auch nach Aus-/Wiederanlegen nur einmal pro Boss auslösen. Schattenstapel sind auf fünf begrenzt und laufen nach acht Sekunden ab.

„Angriff“ auf den Schmuck-/Fluchtafeln wird auf die vorhandene Kraft (`Power`) abgebildet. Bewegungstempo verändert die vorhandenen Angel-/Tauch-Aktionspausen; es existiert keine frei begehbare Spielwelt. Boss-Schadensreduktion bleibt standardmäßig 0; gegen konfigurierte Reduktion ignorieren kritische Treffer mit sechs Verschlinger-Teilen 15 % dieser Reduktion.

## Windows, Twitch und Cloud

Beide Oberflächen verwenden dieselbe Materialübersicht, Bilder, Gegenstandsauswahl, Kostenanzeige, Vorschau und Veredelung. Verbindliche Kostenberechnung und Buchung bleiben im Plugin. Die Windows-Adminansicht erlaubt dem authentifizierten Kanalinhaber Fundchancen, Mengen, Mimic-Gewichte und Perlen-/Kernkosten zu bearbeiten, Standards wiederherzustellen und Testmaterialien zu vergeben. Rollen werden in Backend und Plugin geprüft. Konfiguration wird vor der Übernahme geprüft und dauerhaft gespeichert.

Die Cloud-Projektion ist Version 6. Der Publisher akzeptiert weiterhin die früheren Projektionen 2–5. Erweiterte Materialfelder, Seltenheiten, negative Fluchwerte, Upgrade-Stufe und Veredelungskennzeichen werden bis zum Backend übertragen. Bestehende Konten benötigen keine destruktive Migration.

## Prüfung

Vollständiger lokaler Release-Build: 1.010 Plugin-Tests, 268 Cloud-Tests, 116 Oberflächentests und sieben Updater-Tests bestanden. Zusätzlich bestand ein Browserlauf mit dem echten Desktop-Bundle und einer ausschließlich synthetischen lokalen Brücke: elf geladene Materialbilder, +8 auf +9, einmalige Veredelung und Admin-Balancing. Der Firestore-Test verwendet das tatsächliche SDK mit isolierter lokaler Gegenstelle. Keine Testmaterialien, Items oder Bossaktionen wurden dadurch produktiven Spielern zugewiesen.

Eine erste Prüfrunde deckte fehlende Zerlegewerte der neuen Seltenheiten und eine Rundung unveränderter +0-Bruchteile auf. Beide Fehler wurden vor dem erfolgreichen vollständigen Lauf behoben. Alte Tests für die vorbereiteten Upgrade-Kosten wurden an die neuen verbindlichen Vorgaben angepasst; Tests des bisherigen Lootpools schalten den zusätzlichen Ausrüstungspool ausdrücklich aus und prüfen weiter ihre ursprünglichen Regeln.

Geänderte Plugin-Dateien: `expansions/depths/plugin-files.json`; ihre vollständigen Quellen liegen im gleichnamigen `plugin-overlay`. Weitere Änderungen betreffen `Sync/src/{SnapshotProtocol,SnapshotPublisher,Backend}`, den zusätzlichen Cloud-Test, `ItemExpansion.Tools`, `Build-DepthsUpdate.ps1`, das Desktop-Projekt sowie die gemeinsamen React-Komponenten, Darstellung, Datenübertragung und Asset-Zuordnung. Das Windows-Update und die Twitch-ZIP müssen mit Plugin 2.3.0 und dem Publisher/Backend für Projektion 6 kombiniert werden.

Die Veröffentlichung wurde nachträglich ausdrücklich beauftragt. Eine automatische lokale Installation oder die Aktivierung einer neuen Twitch-Extension-Version im Entwicklerportal gehört nicht zu diesem Veröffentlichungsauftrag.

Heal all: Der Adminbutton setzt alle vorhandenen, nicht zur Löschung gesperrten RPG-Spieler in einer gespeicherten Transaktion auf ihre jeweiligen maximalen HP. Wiederholte Aufträge heilen nicht doppelt; Schreibfehler nehmen die gesamte Änderung zurück. Perlen, Inventar und bestehende Boss-Niederlagen bleiben erhalten.
