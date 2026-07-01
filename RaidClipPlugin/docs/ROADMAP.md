# 🚀 RaidClipPlugin Roadmap

> Roadmap für die Entwicklung von RaidClipPlugin.
>
> Ziel ist es, ein stabiles und professionelles Tool für Twitch-Streamer zu entwickeln, das automatisch Clips von Raid-Streamern in OBS abspielt.

---

# ✅ Version 1.0 - Grundfunktionen

## Fertig

- [x] Twitch OAuth (Device Code)
- [x] EventSub Raid-Erkennung
- [x] OBS WebSocket Integration
- [x] Lokaler Clip-Player
- [x] Zufällige Clip-Auswahl
- [x] Browser Source Steuerung
- [x] Konfigurationsdatei
- [x] Testmodus
- [x] PlaybackService
- [x] CommandService

---

# 🚧 Version 1.1 - Stabilität

## Hohe Priorität

- [ ] Browser OAuth Login
- [ ] Automatischer Token Refresh
- [ ] Retry-System bei fehlerhaften Clips
- [ ] Logging
- [ ] Verbesserte Statusanzeige
- [ ] Test beliebiger Twitch-Kanäle
- [ ] Reload der Konfiguration ohne Neustart
- [ ] Bessere Fehlermeldungen

---

# 🚧 Version 1.2 - Verbesserungen

## Streaming-Komfort

- [ ] Kein Clip doppelt hintereinander
- [ ] Clip-Historie
- [ ] Mehrere Zufallsversuche
- [ ] Mindestanzahl an Clip-Aufrufen
- [ ] Maximales Clip-Alter
- [ ] Ausschluss eigener Clips (optional)
- [ ] Clip-Dauer konfigurierbar

---

# 🚧 Version 1.3 - Komfortfunktionen

## Konsole

- [ ] help erweitern
- [ ] clear
- [ ] reload
- [ ] version
- [ ] about

## Monitoring

- [ ] Letzten Raid anzeigen
- [ ] Letzten Clip anzeigen
- [ ] Anzahl abgespielter Clips
- [ ] Laufzeit anzeigen

---

# 🚧 Version 2.0 - Desktop GUI

## Benutzeroberfläche

- [ ] Dashboard
- [ ] Twitch Login
- [ ] OBS Status
- [ ] Live Log
- [ ] Testbutton
- [ ] Einstellungen bearbeiten
- [ ] Start/Stop Monitoring
- [ ] Dark Mode

---

# 🚧 Version 2.1 - Erweiterte Einstellungen

- [ ] Mehrere OBS Szenen
- [ ] Mehrere Browserquellen
- [ ] Mehrere Twitch Accounts
- [ ] Profile
- [ ] Backup der Konfiguration

---

# 🚧 Version 3.0 - Profi Features

## Clips

- [ ] Favorisierte Streamer
- [ ] Priorisierte Clips
- [ ] Clip Queue
- [ ] Intelligente Clip-Auswahl
- [ ] Clip-Cache

## Statistiken

- [ ] Anzahl Raids
- [ ] Durchschnittliche Zuschauer
- [ ] Clip-Statistiken
- [ ] Monatsübersicht

---

# 🔬 Investigation

Diese Themen müssen zunächst untersucht werden.

## Twitch

- [ ] Browser OAuth Integration
- [ ] Mature / Content Classification Labels
- [ ] Autoplay-Verhalten des Twitch Players
- [ ] Alternative Player-Lösungen

## OBS

- [ ] Browser Source Optimierungen
- [ ] Ladezeiten minimieren

---

# 💡 Ideen

Diese Features haben aktuell keine Priorität.

- [ ] Discord Benachrichtigungen
- [ ] Soundeffekte
- [ ] Chat Overlay
- [ ] Streamer Profilbilder anzeigen
- [ ] Clip Vorschau
- [ ] Animationen
- [ ] Mehrsprachigkeit

---

# 🐞 Bekannte Probleme

- Clips mit Twitch Content Classification Labels werden teilweise nicht automatisch abgespielt.
- Browser OAuth muss den Device-Code-Login ersetzen.
- Retry-System für problematische Clips fehlt.

---

# 🎯 Langfristiges Ziel

RaidClipPlugin soll ein leicht bedienbares, stabiles und professionelles Tool für Twitch-Streamer werden, das automatisch auf Raids reagiert und Clips zuverlässig in OBS wiedergibt.

Der Fokus liegt auf:

- Stabilität
- Zuverlässigkeit
- Einfache Einrichtung
- Gute Performance
- Erweiterbarkeit