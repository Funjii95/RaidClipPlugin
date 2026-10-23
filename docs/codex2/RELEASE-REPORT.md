# Deep Sea Codex 0.5.0 / Plugin 2.5.0

## Inhalt
- 12 Waffen mit persistenter Mana-Regeneration, Magie und Wasser-, Licht- und Abyss-Schaden.
- 3 zusätzliche Eternal-8er-Sets, 24 feste Itemdefinitionen und 24 individuelle transparente PNG-Icons. Bestehende Eternal-6er- und Divine-Sets bleiben erhalten.
- 10 Gebietsbosse mit eigener Grafik, festen Angriffswerten und persönlicher Auswahl. Ein globaler normierter HP-Wert wird zusammen mit dem Spielerzug atomar gespeichert; Ereignisabschluss und individuelle Belohnungen sind wiederholungsgeschützt.
- Basisformel: Beitrag = bestätigter Schaden / persönliche Boss-Basis-HP. Fortschritt bleibt auch bei Datenschutzlöschung eines Teilnehmers erhalten.
- Persönliche Auswahl ist nach dem ersten Treffer gesperrt. Eonath-Drops bleiben berechtigten überlebenden Eonath-Teilnehmern vorbehalten. Divine fällt nie als Beute.
- 15 Windows-Hauptbereiche, Bossgalerie für Desktop und Twitch, Moderatorfreigabe per Twitch-ID, Besitzerrechte für Items und Divine.
- Sammelobjekte sind im Sammelinventar sichtbar. Ungeschützte Exemplare über fünf werden innerhalb erfolgreicher Spielstandtransaktionen atomar zum bestehenden Zerlegewert umgewandelt. Favoriten/Sperren und Kontolimits bleiben wirksam.
- OBS-Browserquelle bleibt auf ausdrücklichen Wunsch unverändert.

## Prüfung
- 1.115 Plugin-Tests bestanden, darunter 17 Gebietsboss-Tests, 14 Eternal-8er-Tests und 4 Sammlungstests.
- 272 Backend-Tests bestanden.
- 131 Desktop-Oberflächentests und 118 Twitch-Oberflächentests bestanden.
- 7 Windows-Updater-Tests bestanden.
- Nativer Windows-Start mit eingebetteter Oberfläche und API-Brücke bestanden.
- Twitch-Panel, Mobilansicht und Konfiguration gegen das vorhandene Backend gebaut und geprüft.
- Projektion 9 erforderlich; Cloud-Rollout und öffentliche Veröffentlichungen werden separat überprüft.

## Assets und Quellen
- Itemdefinitionen und Seteffekte: expansions/eternal-eight/catalog.json und ITEMS.md.
- Einzelicons: otter-ui/public/items/eternal-eight/; Zuordnung mit SHA-256 in otter-ui/src/eternal-eight-art.json.
- Bossporträts: otter-ui/public/bosses/; Zuordnung in otter-ui/src/boss-art.json.
- Die Bilder wurden mit dem eingebauten Bildgenerator tatsächlich erstellt; Prompts liegen unter expansions/eternal-eight/image-prompts.json.
- Die C#-Änderungen sind zusätzlich unter expansions/eternal/plugin-overlay/ und im Plugin-Repository gesichert. plugin-files.json enthält die vollständige Dateiliste.

## Für die nächste Version vereinbarte Extras
- Drei gesonderte Setübersichtsgrafiken.
- Inventarsortierung und zusätzliche Komfortfilter.
- Weitere Admin-Debugansichten und optischer Feinschliff am Codex-Layout.
- Permanente Talentdefinitionen bleiben vorerst durch den gemeinsamen autoritativen Katalog festgelegt; Perkwerte sind konfigurierbar.

## Besondere Umsetzungen
- Nhar'Zuls Lebensentzug erzeugt einen begrenzten persönlichen Schutz gegen den nächsten eigenen Treffer; er erhöht niemals die globalen Event-HP.
- Moderklaues Fluch tickt bei persönlichen Angriffszyklen; es gibt keinen zusätzlichen globalen Polling-Timer.
- Alle Kämpfe benutzen die vorhandenen autoritativen Dienste. Es wurden keine echten Spieler, Belohnungen oder produktiven Bossereignisse zum Test verändert.

## Nachprüfung und lokale Installation
- Windows 0.5.0 und Plugin 2.5.0 wurden veröffentlicht; die lokalen älteren Windows-Dateien 0.4.2 wurden auf 0.5.0 aktualisiert. Alle 24 Eternal-8er-Icons sind im installierten Web-Paket vorhanden.
- Plugin 2.5.1 korrigiert die Empfangsgrenze für bestätigte Aktionsantworten: Der neue Admin-Katalog überschreitet 100.000 Bytes. Die Grenze folgt nun dem gemeinsamen Protokolllimit und bleibt beschränkt. Zwei Regressionstests; insgesamt 1.117 Plugin-Tests bestanden.
- Bestehende Einstellungen und Spielstände wurden vor der lokalen Installation gesichert und beim Dateiaustausch per Hash unverändert bestätigt.
