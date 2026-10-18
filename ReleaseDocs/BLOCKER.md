# Verbleibende Blocker

## Vor echtem Staging-End-to-End-Test

1. Ausdrückliche Freigabe des konkreten Staging-Plans für beide Dienste, Image-Uploads, begrenzte neue Identität/Rolle und Secret-Referenz fehlt.
2. Backend-Aktionsendpunkte und Publisher-Kompatibilitätsabfrage fehlen in den aktuellen Deployments. Neue Revisionen wurden nur lokal gebaut.
3. Neue synthetische Staging-Sitzung und tatsächliche Cloud-Berechtigungen müssen live geprüft werden. Die sechs bisherigen Profile dürfen dabei nicht verändert werden.

## Vor echtem Twitch Hosted Test dieses Kandidaten

4. Nach dem synthetischen Test sind eine separate Konfigurations-/Routing-Freigabe und ein explizit benannter Twitch-Testnutzer mit freigegebenem Testspielstand nötig. Die synthetischen IDs 1001..1006 sind keine echten Twitch-Konten und keine echte Twitch-Identitätsprüfung.
5. Der normale Frontend-Build verwendet die normale Backend-URL. Getaggte synthetische Staging-URLs werden nicht heimlich in den Hosted-Test-Build umgebogen. Erst eine ausdrücklich freigegebene passende Serverkonfiguration schließt diese Verbindung.
6. Desktop-ZIP ist noch nicht auf Twitch hochgeladen. Mobil muss für diese erste Version in der vorhandenen 0.0.1 deaktiviert werden; Quellcode bleibt erhalten.
7. Echte Freigabe der Twitch-Identität, Abgleich des Kontos, Plugin an/aus, Stream offline, Aktion, Snapshot und Browser-Neuladen sind noch zu protokollieren.

## Vor öffentlicher Einreichung / vollständigem Funktionsumfang

8. Bossaktionen, Zerlegen, Lock/Favoriten und Perlen-Minispiele fehlen als sichere echte Backend-Aufrufe. Keine erfundenen Regeln oder Mockaktionen als Ersatz.
9. PublisherSettings ist eine explizite Sechs-Konten-Testkonfiguration; Backend hat Testnutzerfreigaben und einen festen Kanal. Öffentlicher Betrieb für alle Zuschauer erfordert eine gesondert geplante Konfigurationserweiterung und Tests. Für eine auf Funjii begrenzte Veröffentlichung muss außerdem die Streamer-Allowlist dazu passen.
10. Datenschutz-/Nutzungsbedingungen-URLs, bestätigte Betreiberangaben, Lösch-/Aufbewahrungsregeln und ggf. Auftragsverarbeitung sind offen. Support-Mail funjii95@gmail.com wurde vom Nutzer angegeben. Entwürfe sind keine veröffentlichten oder abschließend bestätigten Rechtstexte.
11. Kategorie „Spiele in Erweiterungen“, Discovery-Bild, Logo und wahrheitsgetreuer Screenshot sowie Review-Zeitfenster prüfen. Die aktuelle Beschreibung verspricht noch nicht angeschlossene Bosskämpfe.
12. Twitch muss die Version selbst genehmigen. Hosted Test ist weder Genehmigung noch öffentliche Veröffentlichung.

Mobil bleibt bewusst für später: ursprünglicher Ladegrößen-Blocker 6,56 MB. Desktop-Startbilder sind ebenfalls groß; Optimierung ist empfohlen, aber die mobile Einreichungsgrenze wird nicht als Desktop-Kriterium ausgegeben.
