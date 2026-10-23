# CODEX MASTERPROMPT V4 – FUNJII OTTER RPG
# Synchronisierte Gebietsbosse + Boss-Galerie + eigene Bossbilder

## Projekt

**Repository:** https://github.com/Funjii95/RaidClipPlugin

**Anwendungen:**
- RaidClipPlugin – Windows-Version (C#/.NET)
- Funjii Otter RPG – Twitch Extension
- Bestehendes Cloud Run Backend
- Firestore-Spielstandspeicherung

## 1. Entwicklungsauftrag

Überarbeite das bestehende Boss-System zu einem globalen, prozentual synchronisierten Multiplayer-Boss-Event.

Jeder Spieler soll während desselben Events einen eigenen Gebietsboss auswählen und bekämpfen können.

**Alle Spieler teilen sich denselben globalen HP-Prozentwert, unabhängig davon, welchen Boss sie angreifen.**

Beispiel:

- Spieler A (Level 10) kämpft gegen Korallus (Level 10).
- Spieler B (Level 99) kämpft gegen Eonath (Level 100).
- Beide verursachen Schaden am gemeinsamen Event.
- Korallus verursacht bei Spieler A Level-10-Schaden.
- Eonath verursacht bei Spieler B Level-100-Schaden.
- Spieler A erhält bei erfolgreichem Event nur Korallus-Loot.
- Spieler B erhält bei erfolgreichem Event Eonath-Loot.

Zusätzlich:

1. Zehn einzigartige Bossdefinitionen implementieren.
2. Für jeden Boss eigene Grafiken erstellen.
3. Boss-Reiter vollständig überarbeiten.
4. Eine übersichtliche Bossliste integrieren.
5. Individuelle Bosse und Belohnungen anzeigen.
6. Die vorhandenen Boss-Events und Cooldowns erhalten.
7. Vollständige Adminsteuerung ermöglichen.
8. Alle Funktionen zwischen Windows und Twitch synchronisieren.

**Vor Beginn die tatsächlich vorhandene Boss-, Kampf-, Loot-, EventSub- und Cloud-Architektur analysieren. Bestehende Funktionen erweitern, keine zweite Boss-Engine entwickeln.**

---

# 2. Die zehn verbindlichen Gebietsbosse

Registriere exakt folgende zehn Bosse.

| ID | Name | Gebiet | Level |
|---|---|---|---:|
| boss_korallus | Korallus, der Riffkönig | Sonnenlagune | 10 |
| boss_thalruun | Thal'Ruun, der Tempelhüter | Versunkene Tempel | 20 |
| boss_moderklaue | Käpt'n Moderklaue | Wrackfriedhof | 30 |
| boss_luminara | Luminara, Herz des Riffs | Leuchtender Korallenwald | 40 |
| boss_vortexion | Vortexion, Herr der Strudel | Strudel der Gezeiten | 50 |
| boss_krysalith | Krysalith, der Splitterfürst | Kristallgrotten | 60 |
| boss_maledictus | Maledictus, der Fluchträger | Verfluchte Tiefen | 70 |
| boss_aethernos | Aethernos, Wächter der Wahrheit | Wahrer Abgrund | 80 |
| boss_nharzul | Nhar'Zul, Verschlinger der Leere | Abyss | 90 |
| boss_eonath | Eonath, der Ewige Herrscher | Thron der Urtiefe | 100 |

Diese IDs und Namen sind verbindlich.

Keine Umbenennung, keine abweichenden Schreibweisen und keine zusätzlichen Bosse erzeugen.

Die Bosse besitzen feste Level.

Keine Skalierung der individuellen Bossangriffe anhand des Spielerlevels.

---

# 3. Boss-HP und Grundschaden

Die folgenden Werte sind konfigurierbare Startwerte.

| Level | Boss-HP-Basis | Boss-Angriff |
|---|---:|---:|
| 10 | 15.000 | 25 |
| 20 | 35.000 | 50 |
| 30 | 70.000 | 90 |
| 40 | 120.000 | 145 |
| 50 | 200.000 | 220 |
| 60 | 320.000 | 320 |
| 70 | 500.000 | 450 |
| 80 | 850.000 | 650 |
| 90 | 1.500.000 | 950 |
| 100 | 3.000.000 | 1.400 |

Die HP-Basis dient als Normalisierung des verursachten Schadens.

Sie ist nicht als zehn voneinander unabhängige, dauerhaft gespeicherte HP-Pools zu verwenden.

Der globale Bossfortschritt wird pro Event nur einmal gespeichert.

---

# 4. Ein globaler Boss-HP-Pool

## Grundprinzip

Beim Start eines Boss-Events:

- Ein globales Event erzeugen.
- Globale Boss-HP auf 100 % setzen.
- Alle zehn Gebietsbosse für die Auswahl aktivieren.
- Jeden teilnehmenden Spieler mit seinem ausgewählten Boss verknüpfen.
- Alle Angriffe reduzieren denselben globalen HP-Wert.

Der HP-Wert wird intern bevorzugt als normierter Wert von 0,0 bis 1,0 beziehungsweise als skalierter Festkommawert gespeichert.

Keine zehn getrennten Boss-HP-Werte führen.

## Prozentuale Schadensberechnung

Formel:

`Contribution = ActualDamage / PersonalBossBaseHP`

Beispiel:

Spieler A:
- Korallus
- 15.000 Basis-HP
- 1.000 verursachter Schaden
- Beitrag: 6,6667 Prozentpunkte

Spieler B:
- Eonath
- 3.000.000 Basis-HP
- 60.000 verursachter Schaden
- Beitrag: 2 Prozentpunkte

Gemeinsamer Eventfortschritt:

`100 % - 6,6667 % - 2 % = 91,3333 % verbleibende HP`

Der globale HP-Wert darf niemals negativ werden.

## Wichtig: Tatsächlich verursachter Schaden

Die Schadensberechnung muss den vom vorhandenen Kampfsystem bestätigten Schaden verwenden.

Dabei beachten:

- Angriff
- Kritische Treffer
- Doppelangriffe
- Rüstung
- Schadensreduktion
- Waffeneffekte
- Setboni
- Perks
- Divine-Schaden
- Sonstige vorhandene Kampfmodifikatoren

Keinen Schaden aus bloßen Clientangaben übernehmen.

## Schutz vor Überlauf und Doppelangriffen

- Treffer serverseitig auswerten.
- Bei parallelen Treffern atomare Verarbeitung verwenden.
- Gleiche Attack-ID niemals doppelt verbuchen.
- Beiträge nur solange Event aktiv.
- Bei 0 % Event sofort abschließen.
- Keine Angriffe nach Eventabschluss zulassen.
- Kein erneuter Lootabschluss bei wiederholten Nachrichten.

Die Genauigkeit darf nicht durch frühzeitiges Runden kleiner Schadensbeiträge verloren gehen.

---

# 5. Persönliche Bossauswahl

Jeder Spieler darf beim aktiven Boss-Event einen der zehn Bosse auswählen.

Es existieren keine Levelanforderungen.

Beim ersten gültigen Angriff:

1. Ausgewählten Boss bestätigen.
2. Boss-ID im Teilnehmerdatensatz für dieses Event festhalten.
3. Teilnehmer an diesen Boss binden.
4. Weitere Angriffe gegen denselben Boss ausführen.

**Ein Wechsel des persönlichen Bosses während desselben Events ist nicht erlaubt.**

Damit kann kein Spieler kurz vor dem Eventabschluss auf Eonath wechseln, um Eternal-Loot zu erhalten.

Für das nächste Boss-Event darf erneut frei gewählt werden.

Ohne ausdrückliche Auswahl den zuletzt gewählten persönlichen Boss verwenden.

Bei Spielern ohne gespeicherte Bossauswahl Korallus als Standard verwenden.

---

# 6. Individueller Boss-Schaden

Alle zehn Bosse benutzen ihre eigenen Angriffs- und Fähigkeitswerte.

Nur der globale HP-Prozentwert ist geteilt.

## Fähigkeiten

### Korallus – Korallenpanzer

- 20 % weniger eingehender Schaden für drei eigene Angriffszyklen
- Niedriger Rückschlagschaden

### Thal'Ruun – Runenentladung

- Magischer Angriff
- 125 % Basisangriff

### Käpt'n Moderklaue – Geisterfluch

- 100 % direkter Angriffsschaden
- Zusätzlich 10 % des Basisangriffs über drei Schadensintervalle

### Luminara – Lichtblüte

- 135 % Basisangriff
- Temporärer Eigenschutz: 20 % Schadensreduktion

### Vortexion – Gezeitensturm

- Drei Treffer mit jeweils 55 % Basisangriff
- Bestehende Ausweich- und Rüstungsmechanik beachten

### Krysalith – Kristallbruch

- 160 % Basisangriff
- Ignoriert 20 % der Rüstung

### Maledictus – Seelenraub

- 140 % Basisangriff
- Boss erhält für den nächsten Angriffszyklus einen Schadensbonus von 10 %

### Aethernos – Wahrer Gezeitenschlag

- 185 % Basisangriff
- Ignoriert 25 % der Schadensreduktion

### Nhar'Zul – Leerenverschlingung

- 220 % Basisangriff
- Zusätzlicher Lebensentzug entsprechend 10 % des tatsächlich verursachten Schadens

### Eonath – Urteil der Ewigkeit

- Drei Treffer mit jeweils 110 % Basisangriff
- Letzter Treffer ignoriert 30 % gegnerische Rüstung

Diese Fähigkeiten sind Balancing-Startwerte.

Bossheilungen oder Schilde dürfen den globalen HP-Balken nicht unkontrolliert erhöhen. Entsprechende Effekte entweder als persönliche Schadensreduktion oder als explizit begrenzten globalen Effekt behandeln.

Kein Boss darf durch einen lokalen Heilungseffekt einen eigenen unabhängigen Event-HP-Pool erzeugen.

---

# 7. Individuelle Belohnungen

Die persönliche Bossauswahl bestimmt die Belohnungen jedes Spielers.

Ein Spieler erhält niemals automatisch die Belohnungen eines höherstufigen Bosses, nur weil ein anderer Teilnehmer gegen ihn kämpft.

## Boss-Lootpools

| Boss | Besondere Beute |
|---|---|
| Korallus | Perlenstaub, Weiße Perle, ungewöhnliche Ausrüstung |
| Thal'Ruun | Blaue Perle, Verstärkungskern, seltene Ausrüstung |
| Käpt'n Moderklaue | Verfluchter Seidenfaden, Piratenrelikte, epische Ausrüstung |
| Luminara | Leuchtende Flossenschuppe, epische Ausrüstung |
| Vortexion | Rote Perle, legendäre Ausrüstung, Sturmkrone der Gezeiten |
| Krysalith | Runenmuschel, legendäre Ausrüstung, Leviathans Ruf |
| Maledictus | Verfluchter Seidenfaden, Cursed-Ausrüstung |
| Aethernos | Wahrer Korallenkern, Wahre Sets, Wahre Waffen |
| Nhar'Zul | Abyss-Scherbe, Abyss-Sets, Abyss-Waffen |
| Eonath | Kern der Ewigen Tiefe, Eternal-Sets, Eternal-Waffen, Abyss-Ausrüstung |

Registrierte Itemdefinitionen verwenden.

Keine neuen Duplikatitems anlegen.

## Vorschlag für besondere Bossdrops

| Bosslevel | Besondere Itemchance |
|---|---:|
| 10 | 12 % |
| 20 | 10 % |
| 30 | 8 % |
| 40 | 6 % |
| 50 | 3 % Legendär |
| 60 | 3 % Legendär |
| 70 | 2 % Cursed |
| 80 | 1 % Wahr |
| 90 | 0,5 % Abyss |
| 100 | 0,15 % Eternal |

Die Werte gelten pro überlebendem, belohnungsberechtigtem Spieler.

Die Prozentwerte sind konfigurierbare Startwerte.

Keine Verdoppelung bereits vorhandener Itemdropraten.

Eternal-Luck-Regeln und vorhandene Lootbeschränkungen berücksichtigen.

Divine-Ausrüstung bleibt ausschließlich administrativ erhältlich.

---

# 8. EXP, Perlen und Teilnahme

Der gemeinsame Boss wird bei global 0 % besiegt.

Ein Teilnehmer wird nur belohnt, wenn:

- Er für das Event registriert ist.
- Er mindestens einen gültigen Angriff ausgeführt hat.
- Sein Charakter nach bestehenden Regeln überlebt hat.
- Er beim Abschluss als belohnungsberechtigt gilt.

Die EXP- und Perlenbelohnungen richten sich nach seinem persönlich gewählten Boss.

### EXP-Startwerte

| Level | EXP |
|---|---:|
| 10 | 500 |
| 20 | 1.000 |
| 30 | 2.000 |
| 40 | 3.500 |
| 50 | 6.000 |
| 60 | 10.000 |
| 70 | 16.000 |
| 80 | 25.000 |
| 90 | 40.000 |
| 100 | 65.000 |

Normale Perlenbelohnungen aus der bestehenden Boss- beziehungsweise Gebietslogik verwenden.

Keine Perlenkosten für den Beitritt zu einem Bosskampf einführen.

Verstorbene Spieler erhalten keine Bossbelohnung.

---

# 9. Boss-Reiter – Komplettes Redesign

Erstelle eine eigenständige, hochwertige Boss-Ansicht für die Windows-Version im Stil Deep Sea Codex 2.0.

**Die Bossliste muss der zentrale Bestandteil der Ansicht sein.**

## Desktop-Aufbau

### Linker Bereich – Bossliste

Zeige alle zehn Bosse als scrollbare Kartenliste.

Sortierung: Level 10 bis Level 100.

Jede Bosskarte besitzt:

- Eigenes Bossbild
- Bossname
- Zugehöriges Gebiet
- Bosslevel
- Besondere Loot-Seltenheit
- Auswahlzustand
- Hinweis auf die aktuelle persönliche Auswahl

Die Karten sollten einheitlich groß und gut lesbar sein.

Keine winzigen Dropdown-Einträge als alleinige Bossauswahl.

### Zentraler Bereich – Bossvorschau

Großes Bild des ausgewählten Bosses.

Darunter:

- Bossname
- Level
- Gebiet
- Beschreibung
- Besondere Fähigkeiten
- Basis-HP
- Angriff
- Zugehörige Lootkategorien

### Gemeinsamer Eventbalken

Über dem Kampfbereich:

**GLOBALER BOSSFORTSCHRITT**

Anzeigen:

- Verbleibende Event-HP in Prozent
- Aktuelle Teilnehmerzahl
- Eventstatus
- Nächster geplanter Eventstart
- Verbleibende Eventzeit, falls vorhanden

Alle Teilnehmer sehen denselben globalen HP-Prozentwert.

Optional den Balken zusätzlich mit den ursprünglichen Basis-HP des ausgewählten Bosses visualisieren. Diese Anzeige darf jedoch niemals einen unabhängigen HP-Zustand repräsentieren.

### Rechter Bereich – Persönliche Kampfinformationen

- Ausgewählter Boss
- Persönlicher Bossangriff
- Eigene HP
- Verursachter Schaden
- Persönlicher prozentualer Eventbeitrag
- Aktive Buffs
- Teilnahmeberechtigung
- Kampfbutton
- Persönliche Lootvorschau

Der Bossauswahlbutton wird nach dem ersten gültigen Angriff für das laufende Event gesperrt.

### Unterer Bereich – Eventprotokoll

- Angriffe
- Kritische Treffer
- Besiegte Spieler
- Bossfähigkeiten
- Eventfortschritt
- Besondere Drops
- Eventabschluss

Vorhandene Chat- und OBS-Ausgaben weiterverwenden.

---

# 10. Bossbilder automatisch erstellen

Generiere für alle zehn Bosse eigenständige Bilder.

Verwende eine tatsächlich verfügbare Bildgenerierungsfunktion oder den bereits bestehenden Asset-Workflow.

Falls Codex keinen Bildgenerator zur Verfügung hat, nicht behaupten, Bilder erzeugt zu haben. In diesem Fall eindeutige Platzhalter anlegen und die fehlenden Assets dokumentieren.

Keine fremden urheberrechtlich geschützten Bossgrafiken kopieren.

## Bildanforderungen

Stil:
- Hochwertige Fantasy-RPG-Bossillustration
- Unterwasserwelt
- Deep Sea Codex
- Dunkelblau, Cyan und Gold als Grundstil
- Je Boss individuelle Farbakzente
- Bedrohlich und mächtig
- Keine niedlichen oder knuddeligen Gegner
- Keine Texte im eigentlichen Bossbild

Auflösung:
- Boss-Porträt: mindestens 1024×1024
- Optionales Banner: 16:9

Speichere die Bilder mit stabilen Dateinamen.

### Bildbeschreibungen

**Korallus:** Gewaltige rote Riffkrabbe mit Korallenpanzer und mächtigen Scheren, sonnige Lagune.

**Thal'Ruun:** Uralter Meereswächter mit türkisfarbenen Runen, versunkener Tempel.

**Käpt'n Moderklaue:** Verfluchter Piratenkrebs mit beschädigter Rüstung, geisterhaftes Schiffswrack.

**Luminara:** Majestätische lebende Korallenkreatur mit grünen und türkisen Lichtadern.

**Vortexion:** Gewaltiger elektrischer Sturmrochen, umgeben von rotierenden Wasserstrudeln.

**Krysalith:** Riesiger Kristallgolem mit Eis- und Prismenkristallen.

**Maledictus:** Düsteres, verfluchtes Meeresmonster mit violetten Tentakeln und Schattenenergie.

**Aethernos:** Uralter grüner Korallenleviathan mit strahlender wahrer Meeresenergie.

**Nhar'Zul:** Gewaltiger schwarzer Tiefenleviathan mit dunkelroten Rissen und abyssaler Energie.

**Eonath:** Gigantischer goldweiß leuchtender Urmeer-Herrscher, göttlicher Leviathan vor dem Thron der Urtiefe.

### Dateibenennung

- boss_korallus.png
- boss_thalruun.png
- boss_moderklaue.png
- boss_luminara.png
- boss_vortexion.png
- boss_krysalith.png
- boss_maledictus.png
- boss_aethernos.png
- boss_nharzul.png
- boss_eonath.png

Die Bossbilder in Windows und Twitch wiederverwenden.

Keine identischen Bilder für verschiedene Bosse verwenden.

---

# 11. Twitch Extension

Die vorhandene Bossansicht erweitern.

Mobil und Panel müssen unterstützen:

- Liste aller zehn Bosse
- Persönlichen Boss auswählen
- Globalen Boss-HP-Balken anzeigen
- Persönlichen Kampfstatus anzeigen
- Eigene HP und verursachten Schaden anzeigen
- Bossangriff durchführen
- Eigene Lootvorschau anzeigen
- Eventergebnis anzeigen

Die Bossauswahl muss für Zuschauer dauerhaft gespeichert werden.

Eine laufende Eventbindung bleibt jedoch nach dem ersten Angriff unveränderlich.

Das gemeinsame Event verwendet dieselbe autoritative Backendlogik wie die Windows-Anwendung.

---

# 12. Admin-Control-Center

Erweitere Menüpunkt 15 „RPG-Verwaltung“ um eine vollwertige Bossadministration.

## Bossverwaltung

Alle zehn Bosse in einer editierbaren Tabelle darstellen.

Spalten:

- Bossbild
- Boss-ID
- Name
- Gebiet
- Level
- Basis-HP
- Angriff
- Besondere Fähigkeiten
- Lootpool
- Aktiviert

## Live-Steuerung

- Boss-Event sofort starten
- Event beenden
- Event abbrechen
- Globalen HP-Prozentwert anzeigen
- Teilnehmerliste
- Persönliche Bossauswahl jedes Teilnehmers
- Schaden und Beiträge
- Überlebensstatus
- Eventprotokoll

## Balancing

Einstellbar:

- Basis-HP je Boss
- Angriff je Boss
- Fähigkeiten
- EXP je Boss
- Perlenbelohnungen
- Materialdrops
- Itemdropchancen
- Setdropchancen
- Eventintervall
- Angriffscooldowns
- Mindestteilnahmebedingungen

## Debug

- Testspieler gegen beliebigen Boss kämpfen lassen
- Prozentbeitrag simulieren
- Globalen HP-Abzug prüfen
- Mehrere parallele Treffer simulieren
- Bossdrop simulieren
- Eventabschluss testen

Debugsimulationen nach Möglichkeit in einer isolierten Testumgebung ausführen.

Keine unabsichtlichen produktiven Belohnungen durch Simulationen erzeugen.

---

# 13. Event-Synchronisation

Alle aktiven Teilnehmer müssen zeitnah denselben globalen HP-Wert sehen.

Verwende die bestehende Synchronisations- beziehungsweise Eventstruktur.

Bei Änderungen:

- Globale HP aktualisieren.
- Teilnehmerstatus aktualisieren.
- UI aktualisieren.
- Eventabschluss nur einmal verarbeiten.

Keine Polling-Schleifen mit unnötig hoher Last einführen.

Den vorhandenen Wiederverbindungs- und Cloud-Sync-Schutz verwenden.

### Idempotenz

Jeder Angriff besitzt eine eindeutige ID.

Wiederholte identische Angriffsnachrichten dürfen den globalen HP-Wert nicht erneut reduzieren.

### Eventabschluss

Wenn der globale HP-Wert 0 erreicht:

1. Event atomar als abgeschlossen markieren.
2. Weitere Angriffe sperren.
3. Qualifizierte Teilnehmer ermitteln.
4. Deren persönliche Bossauswahl auslesen.
5. EXP und Loot je persönlichem Boss bestimmen.
6. Belohnungen einmalig vergeben.
7. Resultate dauerhaft speichern.
8. Windows, Twitch, Chat und OBS benachrichtigen.

---

# 14. Sicherheitsregeln

- Schadenswerte ausschließlich autoritativ berechnen.
- Keine Clientangaben ungeprüft übernehmen.
- Spieler dürfen ihren Boss nach dem ersten Angriff nicht wechseln.
- Keine Divine-Drops.
- Eternal ausschließlich aus berechtigten Level-100-Bossbelohnungen.
- Keine Belohnung für verstorbene Spieler.
- Keine doppelten Angriffe.
- Keine doppelten Belohnungen.
- Kein negativer globaler HP-Wert.
- Adminaktionen serverseitig absichern.

**Divine-Spieler dürfen weiterhin normale EXP und Loot erhalten.**

Divine-Sofortsiege müssen über den autoritativen globalen Eventabschluss verarbeitet werden.

---

# 15. Automatisierte Tests

Teste:

1. Alle zehn Bossdefinitionen.
2. Korrekte IDs und Namen.
3. Richtige Gebietszuordnung.
4. Richtige Gegnerlevel.
5. Richtige Basiswerte.
6. Eigenständige Bossfähigkeiten.
7. Gemeinsamen globalen HP-Pool.
8. Prozentuale Schadensumrechnung.
9. Parallele Angriffe verschiedener Bosslevel.
10. Spieler A gegen Korallus und Spieler B gegen Eonath.
11. Gleicher globaler HP-Stand bei allen Teilnehmern.
12. Persönlicher eingehender Boss-Schaden.
13. Bosswechsel nach erstem Angriff verhindert.
14. Eventabschluss bei 0 %.
15. Persönliche Lootpools.
16. Eternal-Loot nur bei Eonath.
17. Divine niemals als Loot.
18. Kein Loot bei Tod.
19. Keine mehrfachen Belohnungen.
20. Idempotenz von Angriffen.
21. Wiederverbindung während eines Events.
22. Windows-Bossliste.
23. Twitch-Bossauswahl.
24. Bossbilder korrekt zugeordnet.
25. Admin-Bossverwaltung.
26. Rückwärtskompatibilität bestehender Spielstände.
27. Bestehende Chat- und OBS-Integration.

---

# 16. Arbeitsreihenfolge

1. Vorhandene Bossarchitektur prüfen.
2. Zehn Bossdefinitionen registrieren.
3. Globalen Prozent-HP-Pool implementieren.
4. Persönliche Bossauswahl anbinden.
5. Individuelle Bossfähigkeiten integrieren.
6. Individuelle Lootpools integrieren.
7. Boss-Reiter neu gestalten.
8. Bossbilder erzeugen und integrieren.
9. Twitch-Bossansicht erweitern.
10. Admin-Control-Center erweitern.
11. Synchronisation absichern.
12. Tests ausführen.
13. Ergebnis dokumentieren.

---

# 17. Abschlussbericht

Nach der Implementierung dokumentieren:

- Geänderte Dateien
- Alle zehn Bossdefinitionen
- Verwendete HP-Formeln
- Eingesetzte Bossfähigkeiten
- Lootpools
- Neue Bossbilder samt Dateipfaden
- Windows-Bossansicht
- Twitch-Bossansicht
- Adminfunktionen
- Tatsächlich ausgeführte Tests
- Fehlende Funktionen oder technische Einschränkungen

Keine erfundenen Testergebnisse.

Kein automatisches Deployment oder produktives Update.

## ABSOLUT WICHTIG

Es gibt nur **EINEN GLOBALEN BOSS-EVENTFORTSCHRITT**.

Jeder Spieler besitzt eine persönliche Bossauswahl mit eigenem Gegnerlevel, Schaden und Lootpool.

Alle gültigen Angriffe reduzieren denselben gemeinsamen HP-Prozentwert.

Ein Level-10-Spieler darf damit zum Sieg über das gesamte Event beitragen, während ein anderer Spieler Eonath auf Level 100 bekämpft.

Die Schwierigkeit und die Belohnungen bleiben für jeden Spieler individuell.