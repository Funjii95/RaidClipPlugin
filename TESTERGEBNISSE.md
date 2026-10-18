# Testergebnisse – integrierte Cloud-Testversion

18.10.2026, Windows x64, .NET SDK 10.0.400. Separate Kopie von RaidClipPlugin 2.1.22; keine Produktivinstallation überschrieben.

| Prüfung | Ergebnis |
|---|---|
| Plugin-Gesamtsuite | 662 bestanden, 0 fehlgeschlagen, 0 übersprungen |
| Darin neue Cloud-/Stream-Tests | 16 bestanden (8 Cloud + 8 Live/Sitzung) |
| Wiederverwendete Backend-/Publisher-Suite | 180 bestanden, davon 3 neue Vollständigkeits-/Capabilities-Tests |
| Windows Release win-x64, eigene Laufzeit | Publish erfolgreich |
| Tatsächliches Hauptfenster, isolierter Datenordner, automatisches Schließen | Prozess beendet mit Exit-Code 0 |
| Ursprünglicher viewer-points.json SHA-256 | E178D117F6D78C9ABB914B9DC0281BD12AD57963C234AB48EEFD92103A23430E, unverändert |
| Cloud-Deployment / echte Cloud-Schreibvorgänge | Nicht durchgeführt |

Die Cloud-Tests verwenden ausschließlich synthetische Konten und einen simulierten Cloud-Transport mit der echten Snapshot-Validierung und Revisionslogik. Die zusätzlichen HTTP-Vertragstests verwenden den echten lokalen ASP.NET-Testhost. Der Fensterstarttest verbindet weder Twitch noch OBS und führt kein Spiel aus. Ein echter Twitch-Livestream-End-to-End-Test wurde nicht durchgeführt.

## Abgedeckte Anforderungen

- Echte `ViewerPointStore.ApplyRpgAsync`-Änderung wird automatisch vom Hintergrundsender erkannt; keine zweite RPG-Engine.
- Unveränderte Quelldateibytes nach Synchronisierung. Reflection-Prüfung aller tatsächlichen RpgProfile-Felder (außer internen CompletedActions), aller RpgItem- und RpgBuff-Eigenschaften. Dezimale Regeneration bleibt erhalten.
- Ausfall vor Staging: jüngster gespeicherter Zustand nach Neustart nachlieferbar.
- Netzwerkfehler während Versand: persistierte Warteschlange bleibt erhalten, Wiederholung mit gleicher ID.
- Verlorene Bestätigung und Neustart: keine doppelte Anwendung, vorhandene Action-Idempotenz bleibt wirksam.
- Neuere Cloud-Revision verhindert Überschreiben und automatisches Rebase.
- Fehlender vollständiger Serververtrag verhindert jeden Upload; lokale Speicherung funktioniert weiter.
- Atomarer Quelldatei-Commit mit Hash-Intent: Absturz vor und nach Umbenennen, Erkennung widersprüchlicher/älterer Datei.
- Verbindung allein reicht nicht: Live-Bestätigung, Ablauf, Offline-Signal, Reihenfolge, verspätete Antworten, Bereitschaft und Nachrichtentimestamp geprüft.
- Alte Ausführungskontexte und bereits wartende Speicheroperationen bleiben nach Sitzungswechsel gesperrt. Abgelehnte mutable Legacy-Änderungen werden aus dem letzten Commit zurückgesetzt.
- Rückgabe eines schon reservierten Duelleinsatzes nach Disconnect bleibt möglich; neue Spielbuchung bleibt gesperrt.
- Alle bisherigen 646 Plugin-Tests weiterhin enthalten. Ein bereits vorhandener Test hatte eine Leser/Schreiber-Race zwischen Guthabenrückgabe und Chatmeldung: Der Test wartet nun explizit auf die Meldung, statt eine gerade beschriebene Liste zu enumerieren. Keine Spielregel dafür geändert.

## Grenzen und verbleibende Freigaben

Der laufende Publisher unterstützt den erweiterten RPG-Projektionsvertrag noch nicht. Der kompatible Servercode ist enthalten, aber nicht deployed. Bis zum gesondert freigegebenen Update und exakten Abgleich eines ausdrücklich gewählten Test-Baselines bleibt realer Upload gesperrt. Es wurden keine Cloud-Profile still angeglichen, angelegt oder überschrieben. Die vorhandenen Konten und alten Projekte blieben unverändert.

Compiler meldet weiterhin bekannte Paket-, WindowsBase-, Nullable- und xUnit-Analyzer-Warnungen aus dem Ausgangsprojekt. Build und Tests sind erfolgreich; Warnungen wurden nicht durch pauschales Abschalten verborgen. Keine Aussage über einen öffentlichen Release oder eine vollständige Extension-Spielaktionsschnittstelle.

Rohprotokolle liegen in diesem Ordner. Konfiguration und Nachbau: `../RaidClipPlugin-CloudIntegration-Test/CLOUD_INTEGRATION_TEST.md`. Änderungen: `../RaidClipPlugin-CloudIntegration-Test/GEAENDERTE_DATEIEN.md`.
