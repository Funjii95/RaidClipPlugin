# End-to-End-Abnahmeprotokoll – noch nicht ausgeführt

Ein leeres oder als ausstehend markiertes Feld ist kein Erfolgsnachweis. Datum, Tester, genaue Image-Digests, tatsächliche Revisionen und ActionIds nach Durchführung ergänzen. Niemals JWTs oder Schlüssel in dieses Protokoll aufnehmen.

## Stufe 1: synthetische getaggte Cloud-Revisionen

Voraussetzung: ausdrücklich genehmigter STAGING_PLAN.json, unveränderte Baseline-Revisionen, neue Revisionen mit 0 % normalem Verkehr und tatsächlich von Cloud Run bestätigte rc1-URLs.

Die Stufe prüft echte HTTPS-/Cloud-/Firestore-Verbindungen mit künstlichen Identitäten und Spielständen. Ein mit dem separaten Staging-Secret erzeugter Test-JWT ist ausdrücklich kein von Twitch ausgestellter Viewer-JWT und kein Hosted-Test-Authentifizierungsnachweis.

1. Alte Revisionen/Verkehr/geschützte Profil-Fingerprints erfassen. Geheimnisse ausschließlich im Speicher/DPAPI verwenden.
2. Neues Publisher-Tag: fehlender Schlüssel 401, falscher Schlüssel 401, falscher Nutzer 403; gültiger Schlüssel liefert capabilities mit Projektion 2, Kanal 42812664, sechs synthetischen IDs und Limit >=262144.
3. Nur synthetisches Profil 1001 vorbereiten. Lokal ausschließlich den originalen ViewerPointStore verwenden (ReleaseTools/SeedSynthetic.csproj); Vorlage bereits isoliert erstellt, 2000 Testperlen. Existiert der Cloud-Teststand bereits, dessen aktuelle Revision sichern und nur nach ausdrücklich genehmigtem synthetischem Test-Setup eine vollständige neue Revision veröffentlichen. Niemals eine alte Revision zurückschreiben oder fremde Profile ersetzen. Bei unklarer Herkunft stoppen.
4. Neue Backend-Revision: fehlender/ungültiger JWT 401/403, falscher Kanal/Benutzer abgelehnt; erlaubter synthetischer JWT liefert Profil. /play/state zunächst nicht bereit.
5. Getrennte Windows-Testversion mit FUNJII_RPG_TEST_DATA_ROOT auf work/release-stage-isolated starten. Authentifiziert mit Funjii-Chat verbinden, Minigame-/Perlenmodul und RPG aktivieren. Beide exakt erlaubten rc1-URLs und getrennte DPAPI-Zugänge verwenden. Keine produktiven Dateien kopieren oder überschreiben.
6. Snapshot-Sync muss erfolgreich initialisieren; dann frischer authentifizierter Plugin-Heartbeat. /play/state muss bereit werden. Das alleinige Öffnen einer Sitzung reicht nicht.
7. Eindeutige neue ActionId: buyPerk damage. Bestehende Standardkonfiguration muss die Kosten im Plugin bestätigen; im synthetischen Standardtest sind 2000 -> 1500 und Perk-Level 0 -> 1 zu erwarten. Bei abweichender Konfiguration tatsächliche Regeln dokumentieren, keine Frontend-Kosten erzwingen.
8. Exakt dieselbe ActionId erneut senden und Plugin neu starten: keine zweite Abbuchung und kein zweiter Perk-Level. Bei unklarem Ausgang dieselbe ID prüfen, nie blind eine neue Buchung anlegen.
9. /me lädt den tatsächlich synchronisierten Zustand mit höherer Revision. Alte Snapshot-Nachricht darf keinen neueren Stand überschreiben. Frische ID bei aktivem Cooldown, fehlendem Item und zu wenig Perlen muss regelgerecht abgelehnt werden.
10. Verbindung unterbrechen, angenommene Aktionen/Belege prüfen, wieder verbinden. Keine doppelten Effekte; bestätigter Stand bleibt erhalten, unklarer Ausgang bleibt gesperrt. Während Plugin-Trennung bzw. nach abgelaufenem Heartbeat keine neue Ausführung.
11. Stream darf offline sein und darf nicht sperren. Abschließend Fingerprints aller nichtsynthetischen Profile unverändert prüfen. Plugin stoppen und Testbelege sichern, keine Testprofile automatisch löschen.

## Stufe 2: echter Twitch Hosted Test – gesonderte Freigabe nötig

Ein echter erlaubter Twitch-Testnutzer muss mit seinem Twitch-Helper-JWT und ausdrücklich freigegebenem Testspielstand verwendet werden. Keine frei erfundene Browser-UserId und keine Weitergabe von JWTs. Passende normale Dienstkonfiguration und Snapshot-Pipeline erst gesondert freigeben; synthetische Staging-Token sind hier unzulässig.

| Fall | Soll | Ist |
|---|---|---|
| Desktop-Panel 500 px aktiviert | aktuelles ZIP, richtige Version | ausstehend |
| Twitch-Identität freigegeben | eigenes Profil, kein fremdes Konto | ausstehend |
| Perlen/Inventar/Ausrüstung | exakt wie Backend/Plugin | ausstehend |
| Plugin läuft und RPG bereit | Aktionen freigegeben | ausstehend |
| Stream offline, Plugin bereit | Aktionen weiterhin möglich | ausstehend |
| Eine freigegebene Testaktion | autoritative Änderung genau einmal | ausstehend |
| Snapshot/Neuladen | bestätigter neuer Zustand bleibt | ausstehend |
| Plugin aus / Heartbeat alt | neue Aktionen gesperrt, Profil ggf. lesbar | ausstehend |
| Wiederverbindung | neue Sitzung, kein altes Duplikat | ausstehend |
| Mobile Ansicht | bewusst nicht Teil der ersten Einreichung | ausgenommen |

Erst nach ausgefülltem Protokoll darf „Hosted Test bestanden“ verwendet werden. Öffentliche Genehmigung ausschließlich nach tatsächlicher Twitch-Bestätigung.
