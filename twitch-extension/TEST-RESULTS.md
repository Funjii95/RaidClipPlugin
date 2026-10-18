# Prüfergebnisse – v2.1.21-Abgleich, 17.10.2026

- Reproduzierbarer Export der reinen C#-Defaults des Tags v2.1.21 erfolgreich; keine Profile gelesen. Das bestehende Windows-Plugin wurde nicht installiert oder ersetzt.
- Strikter TypeScript-/Vite-Build erfolgreich: drei HTML-Einstiege, JS ca. 418 kB / 94 kB gzip; lokale Illustrationen zusätzlich.
- 18 Logiktests bestanden: Katalogumfang/Releasepin, echte Setdefinitionen, Ausrüstung ohne Gratisheilung, passive Heilung und Chat-Cooldown, relative Hot-Time-Chance, Sammler-Duplikate, Zerlegevorschau/Bestätigung/Schutz/Ablauf/Inventaränderungen, serialisierte asynchrone Aktionen, Zutaten aller acht Rezepte, zwei Sushi-Slots, drei unterschiedliche Boss-IDs, Todessperre trotz Heilung, Lebensraub ohne Overkill, Fehlfang/Cooldown, Einsatz-/HP-Prüfung, Hot-Time-Verlängerungssperre und Kernattribute.
- 8 Chromium-Browserprüfungen bestanden: acht Tabs ohne horizontalen Überlauf bei 320×500 und 390×844, acht Rezepte und zwei Sushi-Slots, Abbrechen/Bestätigen beim Zerlegen, sichtbare HP/Heilung, Boss-Cooldown/Gegentreffer, Tauch-/Angelergebnisse sowie Hot-Time-Ablauf mit kontrollierter Uhr.
- Der Charakter-Screenshot bei 320 Pixel Breite wurde visuell geprüft. Keine JS-Laufzeitfehler in den vollständigen Navigationsprüfungen.
- Browserprüfungen verwenden einen nachgebildeten Twitch-Helper und ignorieren das Testzertifikat ausschließlich im Testbrowser. Keine echte Twitch-Sitzung, kein echter Kanal-/App-Test, kein Upload und keine Veröffentlichung.
- Die TypeScript-Regeln sind eine lokale Einzelspieler-Nachbildung. Keine vollständige Gleichheitsgarantie zu den produktiven C#-Diensten; keine echten Plugin-Aktionen, Chat-Ereignisse oder Mehrspieler-Auszahlungen getestet. Die vollständige C#-Testsuite wurde nicht ausgeführt.

## Nachprüfung: Perlen-Minispiele und Heist-Integration (17.10.2026)

- TypeScript-/Vite-Produktionsbuild erfolgreich: drei Einstiegspunkte, JS ca. 450 kB / 104 kB gzip, CSS ca. 17 kB / 5 kB gzip; Illustrationen zusätzlich.
- 36 Logiktests bestanden: 18 bestehende RPG-Prüfungen und 18 neue Minigame-/Controller-/Transportprüfungen. Geprüft wurden sieben Spiele, Heist-Rollen und Null-Einsatz, Beitritt und Teilnehmer, Idempotenz, parallele Aktionen, Einsatz/Auswahl, Erfolg/Fehlschlag aus Fixtures, Rennen-Abrechnung, Duell, Verbindungsverlust/Reconnect, veraltete Ereignisse, Authentifizierungsfehler und aktualisierte Tokens im Live-Transportadapter.
- 13 Chromium-Browserprüfungen bestanden: bisherige acht Prüfungen plus fünf Minigame-Abläufe. Alle neun Haupttabs und sieben Spiele bei 320/390 Pixeln ohne horizontalen Überlauf, Heist-Teilnahme/Teilnehmer/Ergebnis, Bestätigung/Abbrechen/Doppelklick, gemeinsames Demo-Konto, Reconnect, Offline-/Anmeldefehler, absoluter Countdown und reduzierte Bewegung geprüft.
- Selbst erstellte SVG-Heist-Szene anhand des erzeugten Screenshots visuell kontrolliert. Keine externen Bildassets ergänzt.
- Es wurde ausschließlich die Extension erweitert. Der Live-Client ist ein getesteter, nicht aktivierter Transportvertrag. Kein öffentlicher Server, kein echtes JWT-/Plugin-/OBS-/Chat-Ende-zu-Ende-Test, kein Deployment und keine produktiven Perlenänderungen. Kein C#-Release-Build für diesen auf die Extension begrenzten Auftrag.
- Das neue ZIP ist für den manuellen geschlossenen Twitch-Test vorbereitet. Kein automatischer Upload oder Release.

## Nachprüfung: Itemgrafiken aus dem Paket 2.1.22

- 102 Originalmotive anhand der CSV-Item-IDs und SHA256-Prüfsummen verifiziert und zugeordnet. Sämtliche vorhandenen Inventardefinitionen, zwölf Fische und acht Rezepte sind abgedeckt.
- Transparente WebP-Dateien mit maximal 384×384 Pixeln: zusammen 4,73 MiB statt rund 175 MB Quellarchiv. Originaldatei unverändert.
- Bilder in Inventar, Ausrüstung, Loot, Fang und Kochrezepten/Zutaten; lokale Auslieferung, Lazy Loading und Ersatzsymbol bei fehlenden Bildern. HTTPS-Vorschauserver liefert image/webp.
- Produktionsbuild erfolgreich. 54 Logiktests und 16 Browserprüfungen bestanden, einschließlich Zuordnungsabdeckung, Asset-Prüfsummen, tatsächlicher Bilddekodierung aller 102 Dateien unter verschachteltem CDN-Testpfad und Bildanzeige bei 320 Pixel Breite. Die beiden neuen Bildprüfungen nach Verbesserung der Screenshot-Aufnahme nochmals erfolgreich ausgeführt. Inventar- und Sushi-Screenshots visuell kontrolliert.
- Der vorher begonnene Multiplikator-Testadapter-Fix ist ebenfalls enthalten und geprüft: einsatzabhängige Demo-Auszahlung, separate Nettoanzeige und keine feste 200-Perlen-Antwort mehr. Keine produktiven Plugin-Auszahlungen geändert.
- Kein Versionswechsel der RPG-Spielregeln allein durch das Bildpaket. Keine echte Backend-Anbindung, keine automatische Twitch-Veröffentlichung.
