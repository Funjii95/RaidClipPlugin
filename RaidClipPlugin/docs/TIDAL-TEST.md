# TIDAL – zusätzlicher experimenteller Anbieter ab 2.8.16

Stand: 13. November 2026. Spotify bleibt unverändert; es gibt keine Provider-Umschaltung des laufenden Musikwunschsystems.

## Was bereits funktioniert

Unter **Musikwünsche → TIDAL (Test)** einen vollständigen Track-Link einfügen, **Link lokal vormerken**, anschließend den Eintrag auswählen und **Im TIDAL-Player öffnen**. Unterstützt werden HTTPS-Track-Links von tidal.com, www.tidal.com, listen.tidal.com sowie originale Track-Embed-Links. Keine Alben, Playlists, Kurzlinks oder Suchtexte.

Die getrennte lokale Datei `tidal-test-queue.json` enthält maximal 100 Links. Einträge bleiben über Neustarts erhalten; Titel, Künstler, Länge und Explicit-Status werden ohne Katalogzugang bewusst nicht als bekannte Metadaten angezeigt. Ein gültiges Linkformat bestätigt weder Existenz noch Abspielbarkeit eines Songs. Beschädigte Dateien werden nicht still überschrieben.

Der unveränderte offizielle Embed wird erst nach einem bewussten Klick geladen. Play muss dort manuell gedrückt werden. Eine ggf. angebotene Anmeldung findet direkt auf TIDAL-Seiten statt; nach Anmeldung den gewählten Link erneut öffnen. Dafür gibt es ein eigenes WebView2-Profil unter dem Plugin-Datenordner (`TidalWebView2`), getrennt von Twitch und Spotify. Das Plugin bekommt weder Passwort noch API-Token und behauptet keinen erfolgreichen API-Login. HTTPS-Navigation ist auf TIDAL-Domains begrenzt; Dateidownloads sind im Testplayer gesperrt. Das Profil kann eine TIDAL-Browsersitzung behalten; Abmelden erfolgt im offiziellen Player. **Player schließen** beendet die eingebettete Ansicht, ohne Spotify zu steuern.

**Im Browser öffnen** öffnet den kanonischen offiziellen Track-Link. Das ist keine Fernsteuerung oder bestätigte Wiedergabe in der TIDAL-Desktop-App. Auch das Entfernen aus der Merkliste stoppt keinen Titel.

## Bewusste Grenzen

- Keine TIDAL-Entwickler-App vorhanden: daher noch kein OAuth-/Katalogtest und keine Textsuche.
- Vollständige Songs, Anmeldung, Abo-/Länderfreigaben und DRM müssen mit einem echten Konto im Windows-WebView2 geprüft werden. Ein geladenes Dokument ist kein Wiedergabenachweis.
- Ohne zuverlässigen Wiedergabeweg keine automatische Verarbeitung von `!sr`, `!srnow` oder Twitch-Belohnungen für TIDAL. Es wird nichts abgebucht, erfüllt oder im Chat als gespielt gemeldet.
- Keine automatische Wiedergabefortsetzung, TIDAL-Connect-Steuerung oder Manipulation der TIDAL-App. Keine privaten APIs, geliehenen Zugangsdaten, Stream-Extraktion oder Tastatursteuerung.
- Keine automatische Folge-Wiedergabe dieser Merkliste. Sie ist nicht die native TIDAL-Warteschlange.
- Spotify und TIDAL können technisch gleichzeitig Ton ausgeben. Vor einem manuellen Play-Test Spotify selbst pausieren; das Plugin macht das nicht automatisch.

## Tatsächlich durchgeführte Prüfung

- Öffentliche TIDAL-Trackseite und oEmbed (offiziell ausgewiesener Endpunkt) antworten erfolgreich. Der oEmbed liefert einen offiziellen Embed-Link, aber bei der Probe keine ausreichenden Titel-/Künstler-Metadaten für eine Katalogintegration.
- Frischer anonymer Chromium-Browser: Embed antwortet HTTP 200, Play-Klick möglich. Danach keine bestätigte Medienwiedergabe (`currentTime = 0`, pausiert); kein Nachweis für vollständige Songs oder Konto-Login. Dies ist auch kein Nachweis, dass angemeldete Windows-Wiedergabe unmöglich ist.
- Automatisierte Tests prüfen Linkgrenzen, kanonische URLs, keine Abspielbarkeitsbehauptung, Persistenz, doppelte Anfragen, beschädigte Dateien, getrennte Spotify-Daten und ehrliche Fähigkeiten.
- Isolierter Programmstart ohne produktive Zugangsdaten/Spielstände erfolgreich.

Optionaler Online-Test: `node Tools/Test-TidalEmbed.cjs` mit installiertem Playwright. `PLAYWRIGHT_MODULE` kann auf das Paket zeigen, `TIDAL_CHROMIUM` auf einen vorhandenen Browser. Die Probe verwendet einen frischen anonymen Browser, protokolliert keine Tokens oder Stream-URLs und verändert keine Spotify-/Twitch-/OBS-Sitzung.

## Nächster aussagekräftiger Schritt

Mit eigenem TIDAL-Konto im Testreiter einen vollständigen Song manuell starten und über mindestens eine Minute beobachten. Erst nach diesem Nachweis und einer Entwickler-App können Katalogsuche und eine kontrollierte weitere Integration bewertet werden. Bestehende Spotify-Logik erst dann gegebenenfalls über einen Adapter erweitern, nicht ersetzen.

Offizielle Grundlagen: [Embeds](https://developer.tidal.com/documentation/embeds/embeds-overview), [API-Referenz](https://tidal-music.github.io/tidal-api-reference/), [OAuth/PKCE](https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization). Die alte Analyse `TIDAL-INTEGRATION-ANALYSIS.md` beschreibt weiterhin die geplante, nicht vollständig implementierte API-Anbindung.
