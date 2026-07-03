# Changelog

## 1.5.9

- Neues Twitch-Minispiel Duel mit `!duel <user> <punkte|all>`, `!accept` und `!deny`.
- Thread-sichere Einsatzreservierung, Rückerstattung bei Ablehnung, Timeout und Bot-Stopp sowie atomare Pot-Auszahlung.
- Fairer 50/50-Modus oder konfigurierbare Gewinnchance für Herausforderer.
- Neuer Duel-Reiter mit Commands, Limits, Rollen, Chatvorlagen, Live-Status, Abbruch und Testmodus.
- Duel-Commands in zentraler CommandRegistry, Commands-Seite, Kollisionsprüfung und `!commands` integriert.
- Umfangreiche Unit- und Integrationstests für Punkteerhalt, Race Conditions, All-In und Command-Kollisionen.

## 1.5.8

- `!heist` reagiert wieder zuverlässig im Twitch-Chat.
- Broadcaster mit identischem Botkonto werden nicht länger fälschlich blockiert.
- Moderatoren dürfen einen Heist starten.
- Deaktivierte oder nicht erlaubte Heist-Aufrufe liefern eine verständliche Chatantwort.
- Der Test-Heist sendet drei klar markierte Testmeldungen in den Chat, ohne Punkte oder Jackpot zu verändern.

## 1.5.7

- Startabsturz der neuen Commands-Tabelle behoben.
- Rekursive Aktualisierung des Commands-Modulfilters verhindert.
- Automatisches Startfehler-Log unter `%LOCALAPPDATA%\RaidClipPlugin\logs` ergänzt.
- GUI-Starttest ergänzt, damit Fehler beim Fensteraufbau vor Releases erkannt werden.

## 1.5.6

- Neues gemeinschaftliches Heist-Minispiel mit konfigurierbaren Commands, Beitrittsphase, Berechtigungen und Cooldowns
- Einstellbare Erfolgschance von 0 bis 100 Prozent mit kryptografisch sicherem W100
- Vollständige, atomare Jackpot-Verteilung ohne verlorene Restpunkte
- Eigener Heist-Reiter mit Chattexten, Live-Status, Testmodus und Abbruchfunktion
- Zentrale CommandRegistry, neuer Commands-Reiter, Kollisionsprüfung sowie TXT-/JSON-Export
- Neuer frei benennbarer Chatbefehl !commands mit Seiten, Modulfiltern, Rollenprüfung und Spam-Schutz
- Neue Unit- und Integrationstests für Heist-Regeln, Jackpot-Auszahlung, Rollen und Command-Kollisionen

## 1.4.2

- Minigame-Chat startet wieder zuverlässig bei gleichzeitig aktiven Musikwünschen
- Musikwünsche und passive Minigame-Ereignisse teilen sich eine EventSub-Verbindung
- Chat-Verbindung wird beim Start priorisiert

## 1.4.1

- Neuer vollständiger Stream-Start-Check mit 18 unabhängigen Prüfungen
- Konfigurierbares Check-Profil, Wiederholung, sichere Diagnose und Ergebnisexport
- Bestätigter Streamstart mit optionaler Startszene und OBS-Streaming
- Der Jackpot wird ausschließlich bei einer gewürfelten 100 in `!gamble` ausgeschüttet
- Die zufällige Jackpot-Gewinnchance wurde aus Logik und GUI entfernt
- Der Broadcaster erhält nun Chat-, Anwesenheits- und Lurkerpunkte
- Europäisches Roulette mit Farben, Gerade/Ungerade, 1–18, 19–36 und exakten Zahlen
- Konfigurierbare Roulette-Auszahlungen, Einsatzgrenzen und eigener Cooldown

## 1.4.0

- Schaltflächen zeichnen Beschriftungen zuverlässig und vollständig, auch im dunklen Theme
- Größere Navigation, Tabs und Aktionsbereiche verhindern abgeschnittene Texte
- Fenster und Einstellungsbereiche skalieren sauberer bei langen deutschen Beschriftungen
- Neues optionales Modul „Musikwünsche“ mit Spotify PKCE und sicherer Tokenablage
- Twitch-Kanalpunkte-EventSub, persistente Deduplizierung und Statusupdates
- Spotify-Suche, Queue/Sofortwiedergabe, Geräteauswahl, Filter und Blacklists
- Konfigurierbare Chattexte und Moderator-Commands
- Warteschlangen-GUI mit Wiederholen, Abspielen, Überspringen und Sperraktionen
- Automatisierte Tests für Links, Filter, Limits, Cooldowns und Deduplizierung

## 1.3.0

- Checkboxen im dunklen GUI-Theme wieder klar sichtbar und bedienbar
- Konfigurierbare Punkte-Währung mit Einzahl, Mehrzahl und Live-Vorschau
- Auswahl zwischen !punkte, !points, !perlen und eigenem Abfrage-Command
- Konfigurierbare Punkte-Blacklist mit sicheren Standard-Botkonten
- Zentrale Blacklist-Prüfung für Anwesenheit, Chat, Events, Casino und Vergaben

- Anwesenheitspunkte für stille Zuschauer und Lurker
- Neue Commands `!lurk`, `!unlurk` und `!give all <punkte>`
- Garantierte vollständige Jackpot-Auszahlung bei einer gewürfelten 100
- GUI-Einstellungen bleiben während der Plugin-Verbindung bedienbar
- Neues RaidClip-Anwendungslogo für Fenster, EXE und Installer
- Vollständig überarbeitete dunkle GUI mit roter RaidClip-Sidebar, Statuskarten und dunklen Tabs

## 1.2.7

- `!gamble all` setzt alle verfügbaren Punkte auf einen Wurf
- `!give @name <punkte>` überträgt Punkte sicher zwischen Zuschauern
- `!addpoints @name <punkte>` erzeugt als Broadcaster oder Mod neue Punkte

## 1.2.6

- Modulares Minigame-System mit gemeinsamer Punkte-Datenbank
- Passive Punkte für Watchtime, Chat, Follow, Sub, Raid und Channel Rewards
- Neue Commands !daily, !top, !rang und !profil
- Optionale Casino-Spiele Coinflip und Slots
- Dauerhafter Jackpot mit konfigurierbarer Chance und Einzahlung
- Kontolimit sowie tägliche Spiele-, Verlust- und Gewinnlimits
- Sieben übersichtliche Minigame-Reiter mit Rangliste und Historie
- Sicherer JSON-Export und -Import mit automatischem Backup

## 1.2.5

- Neues optionales Modul „Chat-Minigame“ mit eigener Sidebar-Seite
- Dauerhaftes Punktesystem für aktive Chat-Zuschauer
- !punkte mit User-Cooldown
- !gamble mit W100, Einsatzgrenzen und konfigurierbaren Auszahlungen
- Admin-Commands add/remove/set für Broadcaster und Mods
- Globaler Command-Cooldown und thread-sichere lokale Speicherung
- Punktedaten können in der GUI zurückgesetzt werden

## 1.2.4

- Neue linke Kachelnavigation
- Raid Clip und Chat-Moderation sind vollständig getrennte GUI-Seiten
- Eigene Einstellungen und Statusanzeige pro Modul
- Übersichtlicheres Layout für den Live-Betrieb

## 1.2.3

- Eigenständiges, optionales Modul „Chat-Moderation“
- Twitch-Chat über EventSub mitlesen und in einem eigenen GUI-Tab anzeigen
- Timeout, Ban und Nachrichtenlöschung direkt pro Chatzeile
- Optionaler Wortfilter, der Treffer sicherheitshalber nur löscht
- Optionale Whitelist für Streamer, Moderatoren und VIPs
- Moderationsfehler werden isoliert geloggt und blockieren keine Raid-Clips
- Neue Twitch-Berechtigungen für Chat und Moderation

## 1.2.2

- Sicherer Einrichtungsassistent für Twitch-Zugangsdaten
- OBS-Browserquelle auf Knopfdruck erstellen
- GitHub-Auto-Update mit SHA256-Prüfung
