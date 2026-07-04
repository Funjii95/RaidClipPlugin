# Changelog

## 1.6.6

- Absturz beim automatischen Scrollen der Moderations-Chat-Tabelle behoben.
- Auto-Scroll wird nur ausgeführt, wenn die Tabelle sichtbar ist und tatsächlich Platz für mindestens eine Zeile besitzt.
- Kurzzeitige WinForms-Layoutzustände beim Tabwechsel oder Verkleinern werden defensiv abgefangen.
- Moderations-Chat nutzt feste Zeilenhöhen und deaktivierten Textumbruch.
- Regressionstest verarbeitet 300 Nachrichten bei unsichtbarer Tabelle und bestätigt das 250-Zeilen-Limit.


## 1.6.5

- Texteingabe und Senden-Button sind im offiziellen Twitch-Chat und im separaten Popout wieder dauerhaft vorhanden.
- Neuer Custom-Command-Import für JSON, CSV und eingefügten Text mit flexiblen StreamElements-ähnlichen Feldnamen.
- Vorschautabelle mit Quelle, Status, Konfliktmeldung und den Aktionen Überspringen, Überschreiben oder Umbenennen.
- Case-insensitive Kollisionsprüfung gegen System-, Punkte-, Minigame-, Musik-, Clip-, Giveaway- und bestehende Custom Commands einschließlich Aliase.
- Eingebaute Commands können nicht überschrieben werden; sichere Ein-Klick-Option importiert nur konfliktfreie Einträge.
- Importzusammenfassung und Fehler werden im bestehenden Log protokolliert.
- Vorschauzeilen besitzen feste Höhen; lange oder mehrzeilige Antworten und Konflikttexte werden sicher gekürzt, während der vollständige Originaltext erhalten bleibt.


## 1.6.4

- Offizielle 7TV- und BetterTTV-Browser-Erweiterungen lassen sich im WebView2-Twitch-Chat aktivieren.
- Erweiterungen werden beim ersten Verbinden aus den offiziellen GitHub-Releases geladen, lokal zwischengespeichert und im gemeinsamen Chatprofil verwendet.
- Animierte 7TV-/BTTV-Emotes bleiben als GIF/WebP animiert und werden nicht in statische Vorschaubilder umgewandelt.
- Eigene GUI-Schalter für 7TV und BTTV; Fehler einer Erweiterung blockieren den Twitch-Chat nicht.
- BetterTTV-Pakete werden vor der Installation mit SHA256 geprüft.


## 1.6.3

- Neuer eigener Livechat-Bereich in der Sidebar mit bisherigem Plugin-Chat und offiziellem Twitch-Popout-Chat.
- WebView2 lädt den Chat des konfigurierten Twitch-Kanals und zeigt verständliche Verbindungs- und Fehlerzustände.
- Separates, einzelnes Popout-Fenster mit Direktfokus, TopMost sowie dauerhaft gespeicherter Größe und Position.
- Ungültige oder nicht mehr sichtbare Popout-Positionen werden automatisch zentriert.
- Vorbereitete IChatProvider-Architektur und Emote-Modelle für einen späteren nativen Twitch-/BTTV-/7TV-/FFZ-Chat.




## 1.6.2

- Custom Commands mit frei wählbarem Befehl, Aliasen, Chatantwort, Rollen und Cooldowns.
- Deaktiviertes !raid-Beispiel mit der Funjii-Otter-Chatantwort ist vorbereitet.
- Berechtigungen bestehender Commands lassen sich pro Befehl zwischen Zuschauer und Broadcaster überschreiben.
- Livechat erhält eine Eingabezeile zum direkten Schreiben in den Twitch-Chat.
- Doppelte Subscriber- und erhöhte VIP-Gewinnchancen wurden aus Giveaways entfernt.

## 1.6.1

- Livechat-Funktionsleisten passen sich an Fensterbreite und Windows-Skalierung an; alle Einstellungsleisten bleiben scrollbar erreichbar.
- 7TV nutzt Windows-kompatible GIF-Dateien inklusive statischem Fallback; BTTV erkennt animierte Emotes zuverlässig.
- Discord-Clip-Embeds verwenden jetzt Titel, Link und Vorschaubild des tatsächlich veröffentlichten Twitch-Clips.
- Leere Discord-Embed-Felder werden verhindert und durch einen sicheren Platzhalter ersetzt.

## 1.6.0

- Neuer read-only Livechat-Reiter neben Log und Clip-Historie auf Basis der bestehenden EventSub-Chatverbindung.
- Thread-sichere, strikt begrenzte Chat-Historie mit Suche, Pause, Auto-Scroll sowie Command-, Bot- und Systemfiltern.
- Rollen, Badges, Twitch-Benutzerfarben und Twitch-Emote-Fragmente werden aus vorhandenen Chatdaten übernommen.
- Vorbereitete und optional aktivierbare BTTV- und 7TV-Anbieter mit Cache, Fehler-Fallback und ohne API-Aufrufe im deaktivierten Zustand.
- Gebündelte UI-Aktualisierung für stabilen Betrieb bei hohem Chat-Aufkommen.
- Neue Tests für Filter, Limits, Pause, Konfigurationskorrektur, externe Emotes und hohe Nachrichtenmengen.

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
