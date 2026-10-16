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
