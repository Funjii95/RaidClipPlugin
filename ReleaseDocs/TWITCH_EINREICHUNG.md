# Einreichungsunterlagen – Desktop, Entwurf

Stand 18.10.2026. Bestehende Extension l421amjf3ukq56cmppuarkq093sru9, Version 0.0.1. Panel panel.html, Höhe 500, Konfiguration config.html. Mobil für diese Einreichung deaktivieren; keine neue Extension anlegen und keine alte Version löschen.

## Vorschlag für die sichtbaren Texte

Name: Funjii Otter RPG

Autor: Funjii

Kategorie: Spiele in Erweiterungen (in Konsole prüfen/setzen)

Support: funjii95@gmail.com

Zusammenfassung (unter 140 Zeichen):
Tauche mit deinem Otter ab, angle, verwalte Ausrüstung und Perks und stelle Sushi her – mit dem Spielstand des RaidClipPlugins.

Beschreibung (unter 1024 Zeichen):
Funjii Otter RPG verbindet das Desktop-Panel im Funjii-Kanal mit dem laufenden RaidClipPlugin. Schau dir deinen Charakter, Perlen, Inventar und deine Ausrüstung an. Gehe tauchen oder angeln, lege Gegenstände an und ab, verbessere Perks und stelle Sushi her oder benutze es. Spielstand und Cooldowns sind dieselben wie im Chat-RPG. Spielen ist möglich, solange das Plugin authentifiziert verbunden und das RPG bereit ist; der Stream muss dafür nicht live sein. Ist das Plugin offline, sind neue Aktionen gesperrt, gespeicherte Profildaten können lesbar bleiben. Die Twitch-Identitätsfreigabe ordnet dir dein eigenes Konto zu. Für Zuschauer ist keine Zusatzsoftware nötig. Bossaktionen, Zerlegen, Lock/Favoriten und Perlen-Minispiele sind in diesem Stand noch nicht als Extension-Aktion verfügbar. Erste Version: nur Desktop, kein Mobil-Support.

Zuschauerhinweis:
Gib deine Twitch-Identität frei. Spielen geht bei verbundenem, spielbereitem Plugin – auch wenn der Stream offline ist.

Diese Texte sind nur für einen ehrlich begrenzten ersten Umfang geeignet. Für das ursprünglich verlangte vollständige RPG müssen die fehlenden Funktionen erst angeschlossen werden. Keine fertigen Boss-/Minigame-Funktionen bewerben. Die Extension sendet keine Extension-Chatnachrichten; das eigenständige Plugin nutzt bereits Chat-RPG. Chat-Capability daher nicht ohne tatsächlichen Bedarf aktivieren.

## Reviewer-Walkthrough (Englisch)

Review channel: https://www.twitch.tv/funjii

This version is a desktop panel (500 px height). Mobile support is intentionally disabled. The broadcaster must run the separately installed RaidClipPlugin with RPG enabled and an authenticated, fresh connection. Viewers do not install software. The plugin is the authoritative game engine; the backend relays authenticated requests and publishes snapshots. Playing does not require the stream to be live.

Please coordinate a review window with funjii95@gmail.com so that the broadcaster can keep the plugin ready. Reviewer identities must first be authorized for the closed test and given an explicit test profile; never publish test credentials. Grant Twitch identity sharing, open the panel, compare character/inventory/equipment, and perform the agreed safe action. Confirm the updated server snapshot after reload. Disconnecting the plugin must disable new actions. Reconnecting must not replay a previous action.

The current candidate does not yet provide safe extension calls for boss attacks, salvage, lock/favorite changes or pearl minigames. Do not submit a walkthrough that presents those actions as working. Complete and record the real hosted test before review submission.

Changelog: replaced normal demo data with an authenticated backend adapter while preserving the original graphical UI; shared authoritative plugin state and cooldowns; durable idempotent action handling; offline-stream play allowed when the plugin is ready; desktop-only first release.

## Offizielle Anforderungen – geprüft am 18.10.2026

- Lesbarer Anwendungscode, getrennte Drittbibliotheken, Helper zuerst, relative lokale Dateien und erlaubte HTTPS-Backend-Domain. Keine fremden Frames/Inline-Skripte einführen.
- Tatsächliche Funktionen korrekt beschreiben; Twitch-/Glitch-Branding nicht als eigenes Produktbranding verwenden. Keine Zuschauer-Zusatzsoftware verlangen.
- Identitätsverknüpfung und gespeicherte Nutzerdaten benötigen eine passende Datenschutzerklärung. Die Konsole meldet die fehlende URL bereits als Blocker.
- Bildunterlagen prüfen: Logo 100x100 PNG, Discovery 300x200 PNG, mindestens ein tatsächlicher 4:3-Screenshot 1024x768. Das vorhandene Logo ist in der Konsole zu sehen; Discovery fehlt noch bzw. ist zu verifizieren. Der vorbereitete Screenshot ist ein lokaler synthetischer UI-Nachweis und kein echter Hosted-Test-Beleg.
- Für die Begutachtung ein erreichbares Review-Setup und ein Zeitfenster abstimmen. Das ist keine Live-Spielbedingung; ein Panel benötigt laut Lifecycle keine ununterbrochen live laufende Review-Übertragung.

Quellen: [Guidelines & Policies](https://dev.twitch.tv/docs/extensions/guidelines-and-policies/), [Life Cycle / Required Fields](https://dev.twitch.tv/docs/extensions/life-cycle/), [Building / Helper](https://dev.twitch.tv/docs/extensions/building/), [CSP-Modell](https://dev.twitch.tv/docs/extensions/).

Die offizielle mobile Startgrößenbegrenzung bleibt nur für die spätere Mobil-Version relevant. Mobil-Dateien bleiben im Quellcode, aber nicht im Desktop-ZIP. Kein Upload, Review-Antrag oder öffentlicher Release wurde vorgenommen.

## Vor Einreichung auszufüllen

Bestätigter Betreiber/Verantwortlicher, EULA-/Nutzungsbedingungen-URL und Datenschutz-URL fehlen. Autoren-E-Mail in der Konsole prüfen/verifizieren; als öffentliche Support-Mail wurde funjii95@gmail.com angegeben. Review-Zeitfenster erst mit dem Betreiber vereinbaren, keine erfundenen Uhrzeiten eintragen. Die Konto-/Kanalfreigaben müssen zum tatsächlichen öffentlichen Umfang passen.
