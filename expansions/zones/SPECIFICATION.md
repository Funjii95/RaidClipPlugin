# CODEX MASTERPROMPT – Tauchgebiete vollständig korrigieren und implementieren

## Projekt

Repository: https://github.com/Funjii95/RaidClipPlugin

Projekt: Funjii Otter RPG

**AUFTRAG: Bestehendes Tauchgebietssystem reparieren und vervollständigen. Kein zweites, unabhängiges Gebietssystem erstellen.**

## 1. Zuerst Ist-Zustand prüfen

Untersuche vor Änderungen folgende Komponenten:

- Bestehendes `!dive`-System
- Windows-Abenteueransicht
- Twitch-Extension-Abenteueransicht
- Cloud Run Backend
- Firestore-Spielstände
- Gebietsauswahl und gespeicherte Gebiets-IDs
- Gegner- und Kampfsystem
- Fisch- und Tauch-Lootpools
- Schatztruhen und Mimics
- Itemregistry und Seltenheitsdefinitionen
- Materialdrop-Logik
- Vorhandene Tests

Dokumentiere zunächst konkret, welche Teile bereits funktionieren, welche fehlen und welche fehlerhaft implementiert sind.

**Wichtig: Nicht einfach zehn hübsche Gebietskarten bauen, während im Hintergrund weiterhin überall dieselbe Tauchlogik läuft.**

## 2. Verbindliche Gebietsdefinitionen

Es müssen exakt zehn reguläre Tauchgebiete existieren.

| Nr. | ID | Name | Gegnerlevel |
|---|---|---|---|
| 1 | `sunny_lagoon` | Sonnenlagune | 10 |
| 2 | `sunken_temples` | Versunkene Tempel | 20 |
| 3 | `shipwreck_graveyard` | Wrackfriedhof | 30 |
| 4 | `glowing_coral_forest` | Leuchtender Korallenwald | 40 |
| 5 | `tidal_maelstrom` | Strudel der Gezeiten | 50 |
| 6 | `crystal_grotto` | Kristallgrotten | 60 |
| 7 | `cursed_depths` | Verfluchte Tiefen | 70 |
| 8 | `true_abyss` | Wahrer Abgrund | 80 |
| 9 | `abyss` | Abyss | 90 |
| 10 | `primordial_throne` | Thron der Urtiefe | 100 |

Diese IDs sind verbindlich und müssen in Windows, Twitch, Backend, Logs und Spielstand übereinstimmen.

Keine zusätzlichen erfundenen Gebiete.

Keine Umbenennung bestehender IDs ohne rückwärtskompatible Migration.

## 3. Keine Zugangsbeschränkungen

Spieler dürfen unabhängig von ihrem Level jedes Gebiet auswählen.

- Spielerlevelmaximum: 99
- Gegnerlevelmaximum: 100
- Kein Mindestlevel für die Gebietsauswahl
- Keine Perlenkosten für Tauchen
- Keine Energie
- Kein Sauerstoff
- Keine Tauchdauer
- Bestehende Tauch-Cooldowns beibehalten

Ein Level-1-Spieler darf den Thron der Urtiefe auswählen.

Das bedeutet aber nicht, dass er die dortigen Level-100-Gegner besiegen kann.

## 4. Gebiet bestimmt den tatsächlichen Gegner

Jedes Gebiet besitzt einen eigenen Gegnerpool.

| Gebiet | Gegner 1 | Gegner 2 | Gegner 3 |
|---|---|---|---|
| Sonnenlagune | Kleine Riffkrabbe | Frecher Seeigel | Junger Lagunenräuber |
| Versunkene Tempel | Tempelwächterkrabbe | Runenqualle | Vergessener Muschelgolem |
| Wrackfriedhof | Verfluchter Piratenkrebs | Wrackräuber | Geisteraal |
| Korallenwald | Korallenbestie | Leuchtender Kappa | Kristallqualle |
| Strudel | Sturmrochen | Strömungsjäger | Gezeitenkoloss |
| Kristallgrotten | Kristallgolem | Frostpanzerkrabbe | Prismatischer Tiefenjäger |
| Verfluchte Tiefen | Fluchbestie | Schattenkrabbe | Verderbter Tiefenwächter |
| Wahrer Abgrund | Wahrer Korallenwächter | Uralter Riffkoloss | Wächter der Ewigen Strömung |
| Abyss | Abyss-Verschlinger | Leerenjäger | Schattenleviathan |
| Thron der Urtiefe | Urzeit-Verschlinger | Königlicher Abyss-Wächter | Avatar der Urtiefe |

Gegner müssen ausschließlich mit den Werten ihres Gebiets erzeugt werden.

Keine Skalierung mit Spielerlevel oder Gearscore.

### Gegnerwerte

Startwerte für gewöhnliche Gegner:

| Level | HP | Angriff | Rüstung |
|---|---:|---:|---:|
| 10 | 70 | 8 | 3 |
| 20 | 150 | 16 | 7 |
| 30 | 270 | 28 | 12 |
| 40 | 430 | 44 | 19 |
| 50 | 650 | 65 | 28 |
| 60 | 950 | 92 | 40 |
| 70 | 1.350 | 130 | 55 |
| 80 | 1.900 | 185 | 75 |
| 90 | 2.650 | 260 | 100 |
| 100 | 3.700 | 360 | 135 |

Elitegegner:

- 200 % der normalen HP
- 135 % des normalen Angriffs
- 125 % der normalen Rüstung

Vor endgültiger Übernahme prüfen, ob die Werte zu den vorhandenen Schadensformeln passen.

Das Gebiet muss auch bei wiederholtem Tauchen gleichbleibende Gegnerlevel erzeugen.

## 5. Jeder Tauchgang verwendet die ausgewählte Gebiets-ID

Die verbindliche Ablauflogik:

1. Spieler wählt ein Tauchgebiet.
2. Ausgewählte ID wird dauerhaft gespeichert.
3. Spieler startet `!dive` oder drückt „Tauchen“.
4. Backend lädt die gespeicherte Gebiets-ID.
5. Backend überprüft den vorhandenen Tauch-Cooldown.
6. Backend wählt ausschließlich Ereignisse des gewählten Gebiets.
7. Bei einem Gegnerereignis wird ein Gegner mit den festen Gebietswerten erzeugt.
8. Der Kampf wird serverseitig ausgewertet.
9. Nur besiegte Gegner können Ausrüstung vergeben.
10. Andere Tauchereignisse können Materialien, Perlen oder Sammelgegenstände vergeben.
11. Ergebnisse werden einmalig gespeichert.
12. Windows und Twitch zeigen denselben Spielstand.

Das Backend darf nicht stillschweigend auf die Sonnenlagune zurückfallen, wenn ein gültiges anderes Gebiet ausgewählt wurde.

Nur bei alten Spielständen ohne Auswahl gilt `sunny_lagoon` als Standard.

Ungültige Gebiets-IDs müssen eine verständliche Fehlermeldung auslösen.

## 6. Eigenständige Gebietsbeute

Die zehn Gebiete müssen unterschiedliche Lootpools besitzen.

| Gebiet | Spezielle Materialien | Reguläre maximale Ausrüstungsqualität |
|---|---|---|
| Sonnenlagune | Perlenstaub, Weiße Perle | Ungewöhnlich |
| Versunkene Tempel | Blaue Perle, Verstärkungskern | Selten |
| Wrackfriedhof | Verfluchter Seidenfaden | Episch |
| Korallenwald | Flossenschuppe, Blaue Perle | Episch |
| Strudel | Rote Perle, Verstärkungskern | Legendär |
| Kristallgrotten | Runenmuschel | Legendär |
| Verfluchte Tiefen | Verfluchter Seidenfaden | Cursed |
| Wahrer Abgrund | Wahrer Korallenkern | Wahr |
| Abyss | Abyss-Scherbe | Abyss |
| Thron der Urtiefe | Kern der Ewigen Tiefe, Abyss-Scherbe | Eternal |

Die Tabelle definiert die maximale normale Ausrüstungsqualität; vorhandene niedrigere Seltenheiten dürfen weiterhin droppen.

Die tatsächliche Ausrüstung darf jedoch nur von erfolgreich besiegten Gegnern stammen.

**Keine Ausrüstung aus gewöhnlichen Truhen oder passiven Tauchereignissen.**

## 7. Strikte Kampfbindung von Ausrüstung

Diese Regel ist besonders wichtig.

Ausrüstungsitems entstehen nur nach:

- Besiegten normalen Gegnern
- Besiegten Elitegegnern
- Besiegten Mimics
- Besiegten Bossen, sofern die jeweilige Lootquelle dazu berechtigt ist

Keine Ausrüstung durch:

- Gebiet betreten
- Normales Tauchen ohne Kampf
- Angeln
- Gewöhnliche Schatztruhen
- Passiven Bonusloot
- Materialfunde

Wird der Spieler besiegt, gibt es keine Gegnerbeute.

Ein schwacher Spieler darf also im Abyss tauchen, kann aber nicht ohne erfolgreichen Kampf Abyss-Ausrüstung erhalten.

Die vorhandenen Material-, Sammel- und Nichtausrüstungsbelohnungen müssen trotzdem weiterhin funktionieren.

## 8. Wahre Setitems

Nur im Wahren Abgrund und ausdrücklich erlaubten höherstufigen Kampfquellen.

Die drei Wahren Sets verwenden:

- `true_eternal_reef`
- `true_deep_guardian`
- `true_ancient_current`

Die registrierten sechs Gegenstände pro Set verwenden.

Keine neuen Itemdefinitionen mit abweichenden Namen erzeugen.

## 9. Abyss-Setitems

Nur durch berechtigte Kämpfe im Abyss und Thron der Urtiefe.

Set-IDs:

- `abyss_devourer`
- `abyss_blood_dynasty`
- `abyss_sunken_world`

Keine Abyss-Ausrüstung aus kampflosen Truhen.

## 10. Eternal – Thron der Urtiefe

Eternal ist die höchste regulär droppbare Seltenheit.

Farbe: `#FFF2A8`

Ausschließlich im Gebiet:

`primordial_throne`

Drei Sets:

- `eternal_star_hunter`
- `eternal_tide_blessing`
- `eternal_primordial_wrath`

### Dropchancen

| Gegner | Eternal-Itemchance |
|---|---:|
| Normaler Gegner | 0,03 % |
| Elitegegner | 0,12 % |
| Mimic | 0,20 % |
| Besonderer Endgamegegner | 0,35 % |

Diese Chancen gelten nur nach erfolgreichem Sieg und müssen im Debugsystem einstellbar sein.

Luck, passive Bonusloots oder zusätzliche Tauchwürfe dürfen die Eternal-Dropchance nicht unkontrolliert vervielfachen.

Alle 18 Eternal-Itemdefinitionen aus der bestehenden Registry verwenden.

## 11. Divine – Admin Only

Divine-Ausrüstung ist unabhängig von Tauchgebieten.

- Keine regulären Drops
- Keine Truhendrops
- Keine Bossdrops
- Keine Mimic-Drops
- Keine passiven Drops

Nur durch autorisierte Adminvergabe.

Divine-Spieler erhalten bei berechtigten Kämpfen trotzdem normale EXP, Perlen und Lootdrops.

## 12. Truhen und Mimics

Jedes Gebiet besitzt eine eigene Truhen- und Mimic-Chance.

| Level | Truhenchance | Mimic-Chance |
|---|---:|---:|
| 10 | 8 % | 1 % |
| 20 | 10 % | 2 % |
| 30 | 12 % | 4 % |
| 40 | 14 % | 3 % |
| 50 | 16 % | 4 % |
| 60 | 18 % | 5 % |
| 70 | 20 % | 7 % |
| 80 | 22 % | 8 % |
| 90 | 24 % | 10 % |
| 100 | 28 % | 12 % |

Die Werte sind konfigurierbare Startwerte.

Mimics gelten als Gegner.

Mimic-Loot darf nur nach erfolgreichem Kampf vergeben werden.

Die bestehende Regel bleibt erhalten:

**50 % Chance auf ein seltenes Aufwertungsmaterial nach besiegter Mimic.**

Diese Sonderchance nicht versehentlich verdoppeln.

## 13. Angeln mit denselben Gebieten

Auch Angeln muss alle zehn Gebiete unterstützen.

Getrennte Auswahl speichern:

- `selectedFishingZone`
- `selectedDivingZone`

Jedes Gebiet besitzt einen eigenen Fischpool.

Die vorhandenen Fischdefinitionen aus der Registry wiederverwenden.

Fehlende Fische bei Bedarf ergänzen.

Angeln kostet keine normalen Perlen.

Bestehende Fischbelohnungen und das Sushi-System dürfen nicht beschädigt werden.

## 14. Windows-Oberfläche

In der Windows-Anwendung:

### Gebietsauswahl

- Alle zehn Gebiete als auswählbare Karten darstellen.
- Gebietname und Gegnerlevel anzeigen.
- Gewähltes Gebiet hervorheben.
- Eigene Gebietsillustration beziehungsweise eindeutigen Platzhalter anzeigen.
- Mögliche Gegner anzeigen.
- Mögliche Materialien anzeigen.
- Besondere Setdrops anzeigen.
- Gefahrenstufe anzeigen.
- Aktuellen Cooldown anzeigen.
- Button „Hier tauchen“ anzeigen.

Die Auswahl muss tatsächlich mit dem Backend verbunden sein.

Nicht nur den Kartentitel oder ein lokales Frontend-Flag ändern.

### Ergebnisanzeige

Nach dem Tauchgang anzeigen:

- Gebiet
- Ereignistyp
- Gegnername und Level, falls vorhanden
- Kampfergebnis
- Erhaltene EXP
- Erhaltene normale Perlen
- Materialien
- Sammelgegenstände
- Ausrüstungsdrops, falls Gegner besiegt wurde

## 15. Twitch Extension

Dieselben zehn Gebiete in der Extension verfügbar machen.

Panel und Mobilansicht unterstützen.

- Gebiet auswählen
- Aktive Auswahl anzeigen
- Gebietsdaten anzeigen
- Tauchen starten
- Ergebnis anzeigen
- Cooldown anzeigen

Kein separates Client-Lootsystem.

Windows, Twitch und Chatbefehle müssen dieselbe Backendlogik verwenden.

## 16. Debug-Modus

Im vorhandenen Windows-Debugsystem hinzufügen:

- Gebiet auswählen
- Gegnerbegegnung erzwingen
- Bestimmten Gegner spawnen
- Gegnerlevel prüfen
- Truhe auslösen
- Mimic auslösen
- Lootwürfe simulieren
- Dropraten je Gebiet bearbeiten
- Setdropchancen prüfen

Debugaktionen dürfen ausschließlich für berechtigte Admins nutzbar sein.

Testbelohnungen dürfen niemals unbemerkt an normale Spieler vergeben werden.

## 17. Tests

Erstelle für jedes der zehn Gebiete mindestens folgende Tests:

1. Gebiet existiert.
2. Richtige ID.
3. Richtiger Name.
4. Richtiges Gegnerlevel.
5. Keine Levelanforderung.
6. Keine Perlenkosten.
7. Richtiger Gegnerpool.
8. Richtiger Materialpool.
9. Richtige Ausrüstungsqualität.
10. Keine Ausrüstung ohne gewonnenen Kampf.
11. Mimic-Drops nur nach Sieg.
12. Gültige Gebietsauswahl wird gespeichert.
13. Gebietsauswahl wird beim nächsten Tauchgang tatsächlich verwendet.
14. Windows und Twitch erhalten denselben Gebietszustand.

Zusätzlich:

- True-Sets nur aus berechtigten Kämpfen.
- Abyss-Sets nur aus berechtigten Kämpfen.
- Eternal ausschließlich aus Level-100-Kämpfen im Thron der Urtiefe.
- Divine niemals aus normalen Lootquellen.
- Vorhandene Cooldowns funktionieren.
- Bestehende Spielstände bleiben kompatibel.
- Keine doppelten Lootbelohnungen.
- `!dive` verwendet das korrekte Gebiet.
- `!fish` verwendet sein separat gespeichertes Angelgebiet.

## 18. Abnahmekriterien

Die Umsetzung ist erst abgeschlossen, wenn folgende Punkte nachweisbar funktionieren:

**Test A: Level-1-Spieler im Abyss**

- Gebiet kann ausgewählt werden.
- Gegner hat Level 90.
- Spielerlevel beeinflusst Gegnerlevel nicht.
- Niederlage gibt keine Gegnerausrüstung.

**Test B: Level-99-Spieler im Thron der Urtiefe**

- Gegner hat Level 100.
- Erfolgreicher Sieg kann Eternal-Loot auslösen.
- Eternal-Drops verwenden ausschließlich registrierte Itemdefinitionen.

**Test C: Gebiet wechseln**

- Sonnenlagune auswählen und tauchen.
- Danach Abyss auswählen und tauchen.
- Loot- und Gegnerpools unterscheiden sich.
- Die Auswahl bleibt über Neustart und Cloud-Sync erhalten.

**Test D: Normale Schatztruhe**

- Materialien können gefunden werden.
- Sammelgegenstände können gefunden werden.
- Keine Ausrüstung entsteht.

**Test E: Mimic**

- Mimic-Kampf wird gestartet.
- Nur bei Sieg wird Gegnerloot vergeben.
- 50-%-Sonderchance auf seltene Upgrade-Materialien bleibt bestehen.

**Test F: Plattformgleichheit**

- Windows-Auswahl funktioniert.
- Twitch-Auswahl funktioniert.
- Chatbefehle funktionieren.
- Alle drei verwenden denselben autoritativen Spielzustand.

## 19. Arbeitsweise und Abschluss

Arbeite in dieser Reihenfolge:

1. Bestehende Implementierung überprüfen.
2. Fehlerursachen dokumentieren.
3. Gebietsdefinitionen korrigieren.
4. Backend-Gebietsauswahl korrigieren.
5. Gegner- und Kampfverknüpfung herstellen.
6. Gebietslootpools anbinden.
7. Kampfgebundene Ausrüstungsdrops absichern.
8. Windows-UI korrigieren.
9. Twitch-UI korrigieren.
10. Chatbefehle anbinden.
11. Tests ausführen.
12. Ergebnis dokumentieren.

Nach Abschluss für alle zehn Gebiete eine Übersicht der tatsächlich implementierten Gegner, Level, Materialien und Ausrüstungsdrops ausgeben.

Nicht vorhandene Implementierungen ausdrücklich benennen.

Keine Tests als bestanden ausgeben, die nicht ausgeführt wurden.

Kein automatisches Deployment oder produktives Update ohne separaten Auftrag.

**WICHTIGSTE ANWEISUNG:**

Die zehn Tauchgebiete müssen zehn tatsächlich unterschiedliche Spielbereiche sein. Die Gebietsauswahl darf nicht nur kosmetisch sein.

Die ausgewählte Zone muss verbindlich Gegnerlevel, Gegnerpool, Materialpool, Ereignisse und mögliche Kampfbeute bestimmen.