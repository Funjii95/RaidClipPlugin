# Tatsächliche Prüfergebnisse · 16.10.2026

Ausgeführt unter Windows, Node 24.19.0, pnpm 11.25.0, Chromium via Playwright 1.64.1.

| Prüfung                       | Ergebnis                                                                                                                            |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `pnpm build`                  | Erfolgreich: striktes TypeScript und Vite 8.3.4, drei HTML-Einstiege; JS 238,42 kB / 75,40 kB gzip                                  |
| `pnpm test`                   | 8/8 bestanden; 1 Testdatei                                                                                                          |
| `pnpm test:e2e`               | 3/3 bestanden (6,4 s)                                                                                                               |
| HTTPS panel/mobile/config     | Alle HTTP 200 über TLS auf localhost:8080                                                                                           |
| Alle acht Navigationsbereiche | Im 320×500-Panel und bei 390×844 geprüft                                                                                            |
| Responsive                    | Kein horizontales Überlaufen in allen acht Bereichen bei beiden Größen                                                              |
| Tauchen/Angeln                | Ergebnis, Perlen und Cooldown im Browser geprüft; Einsatz und Loot zusätzlich im Logiktest                                          |
| Inventar/Ausrüstung           | Favoritenschutz, Zerlegen, Ausrüsten und Ablegen im Browser; Slots/Setwerte zusätzlich im Logiktest                                 |
| Perks/Boss                    | Kauf/Schaden im Browser; Maxlevel/Buffverbrauch/Boss-Neustart im Logiktest                                                          |
| Hot Time                      | 30→29 Sekunden und Beenden im Browser; Ablauf/Belohnungsbonus mit kontrollierter Uhr im Logiktest                                   |
| Bewegung                      | reduced-motion deaktiviert CSS-Animationen; Checkbox im Aktionsablauf verwendet                                                     |
| Twitch Helper/CSP             | Helper als erstes Skript auf allen drei Seiten; connect-src 'none' geprüft                                                          |
| Fehler/Netzwerk               | Keine JS-Laufzeitfehler in beiden vollständigen Spielabläufen; nur lokale Assets und Helper-URL angefragt (Helper im Test gestubbt) |
| Produktionsabhängigkeiten     | `pnpm audit --prod`: keine bekannten Schwachstellen gemeldet                                                                        |
| Bestehendes Plugin            | Keine C#-Dateien, Dienste, Konfigurationen oder Spielstanddateien verändert; alle hinzugefügten Dateien unter twitch-extension/     |

Keine Aussage über echte Twitch-Authentifizierung oder erfolgreiche Einbettung auf einem Kanal: Twitch-Helper im Browser automatisiert gestubbt; keine Developer-Console-/Kanal-/iOS-/Android-Tests durchgeführt. Zertifikatsimport wurde nicht am Benutzerkonto vorgenommen. Der echte Kanaltest anhand README steht aus. Das C#-Projekt wurde nicht gebaut; es ist von dieser isolierten Frontend-Änderung unberührt.

## Nachprüfung: zerlegte Tauchbeute (16.10.2026)

Fehlerbehebung: Beim Zerlegen eines Fundstücks wird dieselbe Iteminstanz auch aus dem Tauchergebnis entfernt. Nach dem letzten Fundstück schließt sich die Lootanzeige. Zerlegen anderer Inventargegenstände verändert das aktuelle Tauchergebnis nicht; wiederholtes Zerlegen vergibt keine weiteren Perlen.

- TypeScript/Vite-Build erfolgreich (JS 238,61 kB / 75,45 kB gzip).
- Logiktests: 9/9 bestanden.
- Chromium-Browsertests: 5/5 bestanden, 7,8 Sekunden; neuer Ablauf auf Panel und Mobil geprüft, einschließlich Wechsel ins Inventar und zurück.
- Neue Tests verwenden garantierte Mockbeute; kein produktives Backend kontaktiert.
- Aktualisiertes ZIP für manuellen Hosted-Test-Upload vorbereitet, nicht automatisch hochgeladen.


## Nachprüfung: Designvariante 3 „Leuchtende Tiefe“ (17.10.2026)

- Strikter TypeScript-/Vite-Build erfolgreich: drei Einstiegspunkte; JS 241,08 kB / 76,36 kB gzip, CSS 9,81 kB / 3,28 kB gzip. Die beiden lokalen PNG-Illustrationen sind zusätzliche Assets (nicht Teil dieser JS-Größen).
- 9/9 Logiktests bestanden.
- 7/7 Chromium-Browserprüfungen bestanden. Zusätzlich zu den bestehenden fünf Abläufen prüfen zwei neue Fälle die Bild-/CSS-Pfade unter einem verschachtelten simulierten Twitch-CDN-Versionspfad, die geladenen Illustrationen, fehlgeschlagene Assetanfragen und Überlauf bei 280, 320, 390 und 760 Pixel Breite.
- Visuelle Kontrolle der erzeugten Screenshots: Charakter-Panel (320×500) und Abenteuer in Panel/Mobil (vollständige Scrollansicht).
- Gameplay, Demo-Isolation, reduzierte Bewegung und Lootfenster-Korrektur bleiben geprüft. Keine neue Backend-Verbindung.
- Dies ist kein Test auf dem echten Twitch-CDN: Die neue Version ist als ZIP vorbereitet und muss vom Nutzer in den geschlossenen Hosted Test hochgeladen werden. Keine Veröffentlichung, kein Merge.


## Nachprüfung: Otter-HP und Gegnerschaden (17.10.2026)

- TypeScript-/Vite-Build erfolgreich. Gebautes JS 243,24 kB / 77,00 kB gzip, CSS 11,20 kB / 3,64 kB gzip; lokale Illustrationen zusätzlich.
- 12/12 Logiktests bestanden: je Runde genau ein Gegentreffer; keine HP-Änderung durch andere Aktionen; kein Heilen durch vorzeitigen Neustart; HP-Untergrenze 0; Niederlage blockiert Angriffe ohne Zustandsänderung; Kampfneustart erhält Perlen/Items/Perks/Ausrüstung und verbrauchte Buffs; Boss-Todesstoß ohne Gegenangriff; vollständiger Demo-Reset.
- 9/9 Chromium-Browserprüfungen bestanden, 11,1 Sekunden. Die zwei neuen HP-Abläufe testen je 29 Runden bis zur Niederlage, Angriffsperre, Anzeige auf anderen Tabs, Neustart mit 1.000 HP und erneutem Gegentreffer bei 320 und 390 Pixel Breite. Reduzierte Bewegung deaktiviert den Trefferimpuls.
- Screenshots des Kampfes und der Niederlage visuell kontrolliert.
- Das ZIP kombiniert Designvariante 3, den Lootfenster-Fix und HP/Gegnerschaden. Noch kein Upload dieses Updates auf Twitch und kein Merge. Tatsächlicher Twitch-Kanaltest dieser neuen Fassung steht aus.
