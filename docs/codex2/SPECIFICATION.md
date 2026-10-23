# CODEX MASTERPROMPT – FUNJII OTTER RPG
# Deep Sea Codex 2.0 + vollständiges Admin-Control-Center

**Repository:** https://github.com/Funjii95/RaidClipPlugin

**Projekt:** Funjii Otter RPG / RaidClipPlugin

**Zielplattform:** Windows-Anwendung (C#/.NET)

**Designreferenz:** Das zuletzt erstellte Deep-Sea-Codex-2.0-Mockup mit 15 Menüeinträgen, zehn Gebietskarten und rechtem Adminpanel.

---

# 1. Zielsetzung

Überarbeite das vorhandene Windows-Interface zu einem vollwertigen Desktop-RPG-Client.

Das Ergebnis soll:

- den verfügbaren Bildschirmplatz sinnvoll nutzen,
- exakt 15 Hauptmenüpunkte besitzen,
- optisch einheitlich und hochwertig aussehen,
- sämtliche bestehenden RPG-Funktionen zugänglich machen,
- eine umfangreiche, eigenständige RPG-Verwaltung besitzen,
- alle wichtigen Spielwerte über ein Adminpanel konfigurierbar machen,
- bereits bestehende Windows-, Backend- und Cloud-Funktionen weiterverwenden.

**WICHTIG:**

Untersuche das Repository und die derzeitige Windows-Implementierung, bevor du Änderungen vornimmst.

Keine zweite RPG-Engine, kein paralleles Inventarsystem und keine alternative Spielstandspeicherung entwickeln.

Vorhandene Spielstände, Inventare, Equipment-Instanzen, Perlenbestände und Fortschritte dürfen nicht verloren gehen.

Die Twitch Extension wird nicht neu gestaltet.

---

# 2. Grundlayout – Deep Sea Codex 2.0

Erstelle ein modernes, responsives Desktop-Layout.

## 2.1 Kopfzeile

Höhe: ungefähr 75–85 Pixel.

Anzeigen:

- Funjii Otter RPG – Deep Sea Codex 2.0
- Spieleravatar
- Spielername
- Charakterlevel
- EXP-Fortschritt
- Normale Perlenwährung
- Backend-Verbindungsstatus
- Letzte Synchronisation
- Benachrichtigungen
- Einstellungen
- Windows-Fenstersteuerung

Keine erfundenen zusätzlichen Spielwährungen darstellen.

## 2.2 Linke Navigation

Breite: ungefähr 220–250 Pixel.

Exakt folgende 15 Hauptmenüpunkte:

| Nr. | Menüpunkt | Inhalt |
|---|---|---|
| 1 | Abenteuer | Aktivitätenübersicht und Gebiete |
| 2 | Charakter | Charakterwerte, Ausrüstungsslots |
| 3 | Waffen | Waffeninventar und Waffenverwaltung |
| 4 | Ausrüstung | Rüstung und Schmuck |
| 5 | Inventar | Allgemeines Inventar |
| 6 | Aufwertung | Equipment-Upgrades |
| 7 | Materialien | Upgrade-Materialien |
| 8 | Sammlungen | Sammelgegenstände und Fortschritt |
| 9 | Itemsets | Sets und Setboni |
| 10 | Boss | Bosskämpfe und Teilnehmer |
| 11 | Sushi-Küche | Fischverarbeitung und Sushi-Buffs |
| 12 | Talente & Perks | Talentbäume und Perkverwaltung |
| 13 | Minispiele | Heist, Rennen und Gamble |
| 14 | Werte & Statistiken | Spielerstatistiken und Fortschritt |
| 15 | RPG-Verwaltung | Vollständiges Admin-Control-Center |

„Einstellungen“ bleibt separat unterhalb der Navigation.

### Anforderungen

- Icons und ausgeschriebene Namen
- Aktive Hervorhebung
- Hovereffekte
- Tooltips
- Einheitliche Eintragshöhen
- Vertikal scrollbar
- Optional einklappbar
- Keine abgeschnittenen Texte
- Gute Bedienbarkeit bei 1366×768 bis 2560×1440

Die RPG-Verwaltung darf nur für autorisierte Administratoren zugänglich sein.

Wenn der Client selbst nur vom Streamer bedient wird, die bereits bestehende Berechtigungsarchitektur berücksichtigen.

## 2.3 Hauptinhalt

Der zentrale Inhalt nutzt die gesamte verfügbare Fläche.

Keine schmale, mittig platzierte Twitch-Panel-Ansicht mehr.

Pro Reiter passende Layoutkomponenten verwenden.

## 2.4 Rechtes Detailpanel

Ein kontextabhängiges Panel mit ungefähr 320–380 Pixel Breite.

Es zeigt beispielsweise:

- Ausgewähltes Tauchgebiet
- Itemdetails
- Upgrade-Kosten
- Rezeptdetails
- Bossinformationen
- Spielereinstellungen

Das Panel muss ein- und ausblendbar sein.

Im Adminbereich darf es für zusätzliche Verwaltungsfunktionen breiter werden.

---

# 3. Abenteuer – Zehn Reiche der Tiefe

Erstelle eine übersichtliche Gebietsauswahl.

## Darstellung

Zehn große Gebietskarten in einem responsiven Raster.

Bei 1920×1080 möglichst fünf Spalten und zwei Reihen.

Jede Karte enthält:

- Gebietsbild
- Gebietname
- Festes Gegnerlevel
- Besondere Lootkategorien
- Auswahlzustand

## Gebiete

| ID | Name | Gegnerlevel |
|---|---|---|
| sunny_lagoon | Sonnenlagune | 10 |
| sunken_temples | Versunkene Tempel | 20 |
| shipwreck_graveyard | Wrackfriedhof | 30 |
| glowing_coral_forest | Leuchtender Korallenwald | 40 |
| tidal_maelstrom | Strudel der Gezeiten | 50 |
| crystal_grotto | Kristallgrotten | 60 |
| cursed_depths | Verfluchte Tiefen | 70 |
| true_abyss | Wahrer Abgrund | 80 |
| abyss | Abyss | 90 |
| primordial_throne | Thron der Urtiefe | 100 |

Die vorher erstellten zehn Gebietsillustrationen verwenden.

Wenn Assets nur als vollständige Karten vorliegen, sie passend darstellen, ohne Texte unlesbar zu beschneiden.

## Aktivitätsmodi

Zwei Register:

- Tauchen
- Angeln

Beide besitzen eine getrennte gespeicherte Gebietsauswahl.

Die Karten selbst starten keine Aktivität.

Im rechten Detailpanel:

- Gebietsbeschreibung
- Gegner
- Materialien
- Fischarten
- Mögliche Ausrüstung
- Truhenchance
- Mimic-Chance
- Cooldown
- „Hier tauchen“ beziehungsweise „Hier angeln“

Keine Perlenkosten für Angeln oder Tauchen.

Keine Zugangssperren durch Spielerlevel.

Gegner behalten feste Level.

Ausrüstungsdrops nur nach gewonnenen Kämpfen.

---

# 4. Charakter

Große Charakterdarstellung mit folgenden acht Equipment-Slots:

- Helm
- Brust
- Handschuhe
- Stiefel
- Waffe
- Amulett
- Ring links
- Ring rechts

**Der Waffenslot muss direkt am Charakter sichtbar sein.**

Daneben:

- Angriff
- HP
- Rüstung
- Krit-Chance
- Krit-Schaden
- Doppelangriff
- Ausweichen
- Boss-Schaden
- Luck
- Heilung
- Aktive Buffs
- Aktive Itemsets

Die Charakterdarstellung darf bei fehlender dynamischer Charaktergrafik ein vorhandenes Otter-Motiv verwenden.

Keine neuen Equipment-Slots erfinden.

---

# 5. Waffen und Ausrüstung

Erstelle getrennte, aber konsistent gestaltete Ansichten.

## Waffen

- Waffeninventar
- Waffenfilter
- Itemvergleich
- Einzigartige Effekte
- Upgrade-Stufen
- Ausrüsten
- Ablegen

## Ausrüstung

- Helme
- Brustplatten
- Handschuhe
- Stiefel
- Ringe
- Amulette

Die bestehenden Seltenheiten unterstützen:

- Ungewöhnlich
- Selten
- Episch
- Legendär
- Wahr
- Cursed
- Abyss
- Eternal
- Divine

Seltenheitsfarben:

- Wahr: Hellgrün
- Cursed: Lila
- Abyss: Dunkelrot
- Eternal: `#FFF2A8`
- Divine: `#FFD700`

Farben aus zentralen Rarity-Definitionen beziehen.

---

# 6. Inventar

Großes Inventarraster mit:

- Itembildern
- Seltenheitsrahmen
- Stapelmengen
- Suche
- Sortierung
- Kategorien
- Itemdetails
- Vorhandenen Inventaraktionen

Alle Items anhand ihrer tatsächlichen Iteminstanz-ID verwalten.

---

# 7. Aufwertung

Das bestehende garantierte Upgrade-System +0 bis +9 verwenden.

Anzeigen:

- Aktuelles Item
- Nächste Stufe
- Wertevergleich
- Normale Perlenkosten
- Benötigte Materialien
- Vorhandene Mengen
- Fehlende Ressourcen
- Upgrade-Button
- Endveredelung

Jede Aufwertung hat 100 % Erfolgschance.

Keine Fehlschläge.

Keine Itemzerstörung.

Divine-Items dürfen nicht aufgewertet werden.

---

# 8. Materialien

Eigenständige Übersicht sämtlicher registrierter Upgrade-Materialien.

- Icons
- Seltenheit
- Bestand
- Beschreibung
- Fundorte
- Verwendungszweck

Materialbestände aus dem vorhandenen Spielerinventar laden.

Keine separate Währung für Upgrade-Materialien erzeugen.

---

# 9. Sammlungen

Eigenständige Schatzsammlung.

Pro Artefakt:

- Bild
- Name
- Seltenheit
- Fundort
- Fortschritt 0/5 bis 5/5

Gesamtfortschritt dynamisch berechnen.

Neue Sammelgegenstände automatisch aus der Registry übernehmen.

---

# 10. Itemsets

Vollständige Setübersicht.

- Bestehende Sets
- Legendäre Sets
- Wahre Sets
- Abyss-Sets
- Eternal-Sets
- Divine-Set

Setboni und Fortschritt dynamisch aus den registrierten Setdefinitionen laden.

Eternal und Divine besitzen sechs kumulative Boni bei 1–6 Teilen.

Ältere Sets behalten ihre vorhandenen Schwellen.

---

# 11. Boss

Erstelle ein übersichtliches Bossfenster.

- Aktueller Boss
- Bossbild
- Bosslevel
- HP und HP-Balken
- Teilnehmer
- Schadensrangliste
- Bossfähigkeiten, soweit vorhanden
- Belohnungen
- Nächster Spawn
- Kampfprotokoll

Keine fiktiven Teilnehmer oder Bosswerte anzeigen.

---

# 12. Sushi-Küche

Implementiere eine vollwertige Kochoberfläche für das bestehende Sushi-System.

## Linker Bereich

- Verfügbare Fische
- Fischinventar
- Suche
- Filter

## Mittlerer Bereich

- Sushi-Rezepte
- Rezeptbilder
- Benötigte Fischarten
- Zutatenmengen
- Herstellbare Menge

## Rechter Bereich

- Rezeptdetails
- Buffbeschreibung
- Buffstärke
- Buffdauer, sofern die vorhandene Mechanik eine Dauer verwendet
- Aktiver Sushi-Buff
- Herstellen-Button
- Benutzen-Button

Die zuvor definierten neuen Sushi-Spezialitäten aus den zehn Angelgebieten integrieren, sofern diese bereits im Rezeptkatalog existieren.

Keine vorhandenen Fische oder Sushi-Items duplizieren.

Herstellung muss serverseitig validiert und gegen doppelte Materialabbuchung geschützt sein.

---

# 13. Talente & Perks

Erstelle eine zentrale Talentverwaltung.

Die vorhandenen Talentbäume unterstützen:

- Kämpfen
- Angeln
- Tauchen

Jeder Talentbaum besitzt maximal 33 verteilbare Punkte nach dem bestehenden Regelwerk.

Keine Sauerstoff-, Tauchdauer- oder Zeitmechaniken ergänzen.

Anzeigen:

- Talentbaum
- Verteilte Punkte
- Freie Punkte
- Voraussetzungen
- Talentbeschreibung
- Aktive Boni
- Bestehende Rücksetzfunktion, sofern vorhanden

Zusätzlich die bestehende Perk-Verwaltung integrieren.

---

# 14. Minispiele

Eigenständiges Fenster mit drei Unterbereichen:

## Heist

- Aktueller Status
- Teilnehmer
- Einsatz
- Belohnungen
- Ergebnis
- Cooldown

## Rennen

- Teilnehmer
- Start
- Rennfortschritt
- Gewinner
- Belohnungen

Es handelt sich um ein Otter-Wettrennen, nicht um Motorsport.

## Gamble

- Einsatz
- Vorhandene Spielregeln
- Ergebnis
- Gewinn oder Verlust
- Historie, sofern vorhanden

Alle Minispiele verwenden weiterhin die bestehenden Backend-Services.

Keine doppelte Ausführung durch wiederholte UI-Anfragen.

---

# 15. Werte & Statistiken

Erstelle ein Statistikdashboard.

Mögliche Kennzahlen, soweit tatsächlich erfasst:

- Aktuelles Level
- Gesamt-EXP
- Besiegte Gegner
- Besiegte Bosse
- Gesamtschaden
- Erfolgreiche Tauchgänge
- Erfolgreiche Angelvorgänge
- Gefangene Fische
- Gefundene Materialien
- Seltene Itemdrops
- Sammelfortschritt
- Gewonnene Heists
- Gewonnene Rennen
- Gamble-Ergebnisse

Bestehende Statistiken verwenden.

Nicht vorhandene Werte als „Noch nicht erfasst“ kennzeichnen.

---

# 16. RPG-Verwaltung – Admin-Control-Center

**Dieser Bereich hat neben der Gebietsauswahl höchste Priorität.**

Ziel: Der Administrator soll das gesamte Funjii Otter RPG zentral über die Windows-Anwendung steuern können.

Keine manuelle JSON-Bearbeitung für gewöhnliche Konfigurationsänderungen erforderlich.

Verwende die bereits bestehenden Konfigurations- und Administrationsservices.

Die Verwaltung erhält folgende Unterreiter:

## 16.1 Dashboard & Systemstatus

Anzeigen:

- Backend-Status
- Cloud-Sync-Status
- Plugin-Version
- Aktive Spieler
- Aktuelle Spielereignisse
- Bossstatus
- Minispielstatus
- Fehlerprotokoll
- Letzte Synchronisation

Funktionen:

- Einstellungen neu laden
- Verbindung prüfen
- Diagnosedaten anzeigen
- Vorhandene sichere Neustart- oder Reconnect-Funktionen verwenden

Keine destruktiven Neustarts ohne Bestätigung.

## 16.2 Spielerverwaltung

- Spieler suchen
- Spielerprofil öffnen
- Level anzeigen
- EXP anzeigen
- Perlenbestand anzeigen
- Inventar anzeigen
- Ausrüstung anzeigen
- Materialien anzeigen
- Sammlungen anzeigen

Autorisierte Adminaktionen:

- EXP vergeben oder abziehen
- Perlen vergeben oder abziehen
- Materialmengen ändern
- Items vergeben oder entfernen
- Sammelgegenstände verwalten
- Talentpunkte bearbeiten, sofern das bestehende System dies unterstützt
- Testspielstände zurücksetzen

Alle Aktionen benötigen eine serverseitige Berechtigungsprüfung.

Jede Änderung protokollieren.

## 16.3 Item- & Lootverwaltung

Konfigurierbar:

- Globale Itemdropchance
- Gebietsspezifische Itemdropchancen
- Seltenheitsgewichte
- Gegnerloot
- Bossloot
- Mimic-Loot
- Materialdrops
- Sammelgegenstands-Drops
- Eternal-Dropraten

Divine darf niemals als regulärer Loot aktiviert werden.

Ausrüstung darf weiterhin nur von besiegten Gegnern droppen.

## 16.4 Gebietsverwaltung

Alle zehn Tauchgebiete als bearbeitbare Datensätze anzeigen.

Je Gebiet:

- Name und stabile ID
- Gegnerlevel
- Gegnerpool
- Gegnerwerte
- Truhenchance
- Mimic-Chance
- Materialpool
- Fischpool
- Ausrüstungs-Lootpool
- EXP-Multiplikator
- Perlen-Multiplikator

IDs nicht ohne Migration ändern.

Bestehende Spielerlevel- und Gegnerlevelregeln erhalten.

## 16.5 Gegnerverwaltung

- Gegner suchen
- Gegnerwerte bearbeiten
- Gegnerlevel anzeigen
- HP bearbeiten
- Angriff bearbeiten
- Rüstung bearbeiten
- Elite-Multiplikatoren anpassen
- Gebietszuordnung prüfen
- Testkampf auslösen

Keine Änderung der Gegnerwerte durch eine bloße UI-Vorschau.

## 16.6 Bossverwaltung

- Bossauswahl
- Bosswerte
- HP
- Angriff
- Rüstung
- Spawnintervall
- Teilnehmerregeln
- Belohnungen
- Boss sofort starten
- Bosskampf abbrechen
- Boss zurücksetzen

Laufende Kämpfe nicht ohne Warnung verändern.

## 16.7 Minispiel-Steuerung

Implementiere ein zentrales Live-Control-Panel.

### Heist

- Aktivieren/deaktivieren
- Einsatzgrenzen
- Teilnahmefenster
- Cooldown
- Gewinnwahrscheinlichkeiten
- Belohnungsmultiplikatoren
- Heist starten
- Heist beenden
- Heist abbrechen

### Rennen

- Aktivieren/deaktivieren
- Teilnehmergrenzen
- Teilnahmefenster
- Cooldown
- Einsatzoptionen, soweit vorhanden
- Rennen starten
- Rennen beenden
- Rennen abbrechen
- Rennergebnis anzeigen

### Gamble

- Aktivieren/deaktivieren
- Mindesteinsatz
- Höchsteinsatz
- Gewinnmultiplikatoren
- Vorhandene Gewinnregeln
- Cooldown
- Ergebnisprotokoll

**Adminänderungen an Wahrscheinlichkeiten betreffen ausschließlich die Minispiele, niemals die garantierte Ausrüstungsaufwertung.**

## 16.8 Aufwertungsverwaltung

Konfigurierbar:

- Perlenkosten +1 bis +9
- Verstärkungskernkosten
- Farbige Perlen
- Seltenheitsmaterialien
- Upgrade-Statboni
- Endveredelungskosten

Erfolgswahrscheinlichkeit bleibt immer 100 %.

Keine Einstellung für Upgrade-Fehlschläge hinzufügen.

Divine bleibt nicht aufwertbar.

## 16.9 Fisch- & Sushi-Verwaltung

- Fischarten verwalten
- Fischpools nach Gebiet
- Fangchancen
- Sushi-Rezepte
- Zutatenkosten
- Buffwerte
- Buffdauer
- Aktivierte Rezepte
- Testherstellung

Keine neuen Fischdefinitionen duplizieren.

## 16.10 Talente & Perks

- Talentdefinitionen anzeigen
- Talentkosten konfigurieren
- Punkteobergrenzen anzeigen
- Voraussetzungen bearbeiten
- Perkpreise konfigurieren
- Buffwerte bearbeiten

Vorhandene Validierungsregeln erhalten.

## 16.11 Events & Hot Time

- Hot Time aktivieren/deaktivieren
- Bonusdauer
- Perlenbonus
- Materialbonus
- Dropbonus
- Event sofort starten
- Event beenden
- Eventstatus anzeigen

Den vorhandenen Hot-Time-Ablauf wiederverwenden.

## 16.12 Debug & Testwerkzeuge

- Testgegner spawnen
- Truhe auslösen
- Mimic auslösen
- Lootroll simulieren
- Materialien vergeben
- Itemdrops testen
- Bosskampf testen
- Cloud-Sync prüfen
- Spielerzustand überprüfen

Debugsimulationen klar von produktiven Spielaktionen unterscheiden.

Für Simulationen nach Möglichkeit einen isolierten Testzustand verwenden.

---

# 17. Adminpanel – Bedienung und Sicherheit

Das Admin-Control-Center muss wie ein richtiges Verwaltungswerkzeug funktionieren.

## Konfiguration

- Änderungen zunächst lokal im Formular bearbeiten.
- Änderungen validieren.
- Unterschiede vor dem Speichern anzeigen.
- Änderungen erst durch „Übernehmen“ speichern.
- Erfolgreiche Speicherung bestätigen.
- Fehler verständlich anzeigen.
- Vorhandene Rücksetz- und Standardwertfunktionen integrieren.

## Liveaktionen

Aktionen wie „Boss starten“, „Mimic auslösen“ oder „EXP vergeben“ besitzen separate Bestätigungen.

Liveaktionen nicht automatisch beim Ändern eines Dropdowns ausführen.

## Berechtigungen

- Serverseitige Adminprüfung.
- Normale Zuschauer können Admin-Endpunkte nicht aufrufen.
- Keine Freigabe durch manipulierbare Frontend-Flags.
- Adminaktionen protokollieren.
- Keine Secrets, Twitch-Tokens oder API-Schlüssel im UI anzeigen.

## Audit-Protokoll

Für administrative Änderungen nach Möglichkeit speichern:

- Zeitpunkt
- Autorisierte Adminidentität
- Aktion
- Betroffener Spieler oder Konfigurationsbereich
- Alter Wert
- Neuer Wert
- Ergebnis

---

# 18. Designsystem

Farben:

- Hintergrund: `#061527`
- Panels: `#0C2239`
- Sekundärflächen: `#102B46`
- Aktive Auswahl: `#15517B`
- Cyan-Akzent: `#43C8F5`
- Goldakzent: `#D9AE63`
- Primärtext: `#F2F6FA`
- Sekundärtext: `#A9C6D8`

Typografie:

- Haupttitel: 26 px
- Abschnittstitel: 20 px
- Kartentitel: 16 px
- Fließtext: 14–15 px
- Zusatztext: mindestens 12–13 px

Verwende wiederverwendbare Komponenten:

- Navigationspunkt
- Gebietskarte
- Gegenstandskarte
- Gegnerkarte
- Statistikfeld
- Detailpanel
- Admin-Einstellungsfeld
- Dropchance-Eingabe
- Bestätigungsdialog
- Validierungsfehler
- Statusanzeige
- Erfolgs- und Fehlermeldung

Dekorationen dürfen keine Texte oder Bedienelemente verdecken.

---

# 19. Responsive Windows-Darstellung

Unterstütze:

- 1366×768
- 1600×900
- 1920×1080
- 2560×1440

Wichtig:

- Sidebar scrollbar
- Hauptbereich flexibel
- Detailpanel optional einklappbar
- Keine abgeschnittenen Bedienelemente
- Keine überlappenden Fenster
- DPI-Skalierung beachten
- Maus und Tastatur unterstützen

---

# 20. Technische Architektur

Die Windows-Anwendung ist die Benutzeroberfläche, nicht eine zweite autoritative Spielinstanz.

Verwende bestehende Services für:

- Spielerdaten
- Equipment
- Inventar
- Materialverwaltung
- Tauchgänge
- Angeln
- Bosse
- Minispiele
- Sushi
- Talente
- Konfiguration
- Cloud-Synchronisation

Falls für Adminfunktionen noch keine autorisierten Backend-Operationen existieren, diese gezielt ergänzen.

Keine administrativen Aktionen allein clientseitig durchführen, wenn dadurch der zentrale Spielstand umgangen würde.

Keine parallelen Datenspeicher aufbauen.

---

# 21. Implementierungsreihenfolge

## Phase 1 – Bestandsaufnahme
- Repository analysieren
- Vorhandene Funktionen erfassen
- Fehlende Adminoperationen auflisten
- UI-Framework identifizieren

## Phase 2 – Designsystem
- Farben
- Schriftgrößen
- Komponenten
- Navigation
- Layout

## Phase 3 – Hauptfenster
- Kopfzeile
- 15 Menüpunkte
- Dynamischer Hauptbereich
- Kontextpanel
- Statusleiste

## Phase 4 – Spielansichten
- Abenteuer und Gebiete
- Charakter
- Waffen und Ausrüstung
- Inventar
- Aufwertung
- Materialien
- Sammlungen
- Itemsets
- Boss
- Sushi-Küche
- Talente
- Minispiele
- Statistiken

## Phase 5 – Admin-Control-Center
- Systemstatus
- Spielerverwaltung
- Items und Loot
- Gebiete und Gegner
- Bosse
- Minispiele
- Upgrades
- Fische und Sushi
- Talente
- Events
- Debugging

## Phase 6 – Tests
- Funktionstests
- Berechtigungsprüfungen
- Synchronisation
- Regressionstests
- Fenstergrößen
- Screenshots

---

# 22. Verbindliche Abnahmekriterien

Die Implementierung ist erst fertig, wenn:

1. Alle 15 Hauptmenüpunkte erreichbar sind.
2. Jeder Menüpunkt eine funktionierende Ansicht besitzt.
3. Sushi-Küche vollständig integriert ist.
4. Die zehn Gebiete korrekt angezeigt werden.
5. Angeln und Tauchen getrennte Gebietsauswahlen besitzen.
6. Der Waffenslot am Charakter sichtbar ist.
7. Alle Itemseltenheiten korrekt dargestellt werden.
8. Eternal- und Divine-Sets korrekt angezeigt werden.
9. Divine nicht aufwertbar ist.
10. Das Adminpanel alle vorhandenen RPG-Systeme verwalten kann.
11. Minispiele über das Adminpanel gesteuert werden können.
12. Dropchancen und Cooldowns nur über autorisierte Funktionen verändert werden.
13. Spielerverwaltung funktioniert.
14. Boss- und Mimic-Testaktionen funktionieren.
15. Konfigurationen persistent gespeichert werden.
16. Adminänderungen nachvollziehbar protokolliert werden.
17. Keine Spielerstände verloren gehen.
18. Keine doppelte Lootvergabe entsteht.
19. Die Twitch Extension unverändert funktionsfähig bleibt.
20. Die UI auch bei kleineren Fenstergrößen sinnvoll bedienbar ist.

---

# 23. Abschlussbericht

Nach Implementierung ausgeben:

- Liste geänderter Dateien
- Neue UI-Komponenten
- Alle 15 implementierten Hauptansichten
- Admin-Unterbereiche
- Neu ergänzte Backendoperationen
- Noch fehlende Verwaltungsfunktionen
- Sicherheitsprüfungen
- Tatsächlich ausgeführte Tests
- Screenshots der Windows-Oberfläche, sofern möglich

Keine Testergebnisse erfinden.

Keine automatische produktive Veröffentlichung oder Deployment.

## Oberste Priorität

Das Ergebnis muss eine vollständig integrierte, hochwertig gestaltete Windows-Anwendung sein, mit der ich mein Funjii Otter RPG sowohl spielen als auch administrieren kann.

Die 15 Hauptmenüpunkte und das Admin-Control-Center sind verbindlich.

Das Redesign darf keine bestehenden RPG-Funktionen beschädigen.