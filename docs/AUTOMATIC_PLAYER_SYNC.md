# Automatische Spieler-Synchronisation – Aktivierungsplan

Stand: vorbereitet und lokal getestet, noch NICHT aktiviert. Twitch-Einreichung bleibt ausgesetzt.

## Ergebnis
Neue Spieler mit Points > 0 werden aus der bestehenden, vollständig gespeicherten viewer-points.json aufgenommen. Die erste Übertragung wird vor dem Senden in der bestehenden dauerhaften Outbox gespeichert. Nach bestätigter Aufnahme bleiben Spieler auch bei 0 Perlen aufgenommen; der Outbox-Zustand übersteht Neustarts.
Bestehende explizite Freigaben bleiben erhalten. SoundAlerts (ID 216527497) und zusätzlich konfigurierte Perlen-Sperrlisten werden bei automatischer Aufnahme ausgeschlossen. Es gibt keine allgemeine automatische Erkennung beliebiger Bots.
Keine Änderungen am ursprünglichen Spielstandformat, keine neue Spielökonomie, keine Browser-Erstellung eines Profils.
Authentifizierte Veröffentlichung, vollständige Projektion, Kanalbindung, JWT, lokale Spielregeln, Heartbeat, Reihenfolge, Revisionen und Löschsperren bleiben aktiv. Stream-Livestatus ist keine Voraussetzung.
Eine neue Action wird im automatischen Modus nur bei vorhandenem veröffentlichtem Profil angenommen und zusätzlich lokal gegen den bestätigten Aufnahmezustand geprüft.
Ein Konflikt mit einem vorhandenen Cloud-Profil wird NICHT automatisch durch Überschreiben gelöst. Bei fehlender lokaler Outbox muss das lokale Profil für einen bestehenden Cloud-Stand weiterhin exakt übereinstimmen.

## Konkrete Serveränderungen
Projekt: funjii-otter-rpg; Region: europe-west3. Datenbank: (default), STANDARD / FIRESTORE_NATIVE, eur3, am 20.10.2026 lesend verifiziert.
Version beider Images: 2.1.29-auto-sync.1. Container sind gebaut und in das bestehende Registry-Repository hochgeladen, noch nicht bereitgestellt.

### funjii-otter-rpg-backend-test
Image: `europe-west3-docker.pkg.dev/funjii-otter-rpg/otter-rpg-test/backend@sha256:ddc09b1090e0c5bdbd531133b4bf92e72269927f1e6239148a8e9f9469c811dc`

Neue Konfiguration: `Actions__AutomaticPlayerEnrollment=true`. Bestehende Identität, Secrets, Kanal, Datenbank, CORS und Altfreigaben unverändert übernehmen.

Bisherige rc1-Revision: `funjii-otter-rpg-backend-test-00011-twz`.

### funjii-otter-rpg-publisher-test
Image: `europe-west3-docker.pkg.dev/funjii-otter-rpg/otter-rpg-test/publisher@sha256:1437f215c1955a04a5f833673f7694833dff9fe8dacf6f3a9db55113190b01b5`

Neue Konfiguration: `Publisher__AutomaticPlayerEnrollment=true`. Bestehende Identität, Secrets, Kanal, Datenbank, CORS und Altfreigaben unverändert übernehmen.

Bisherige rc1-Revision: `funjii-otter-rpg-publisher-test-00019-xnm`.

## Bereitstellungsreihenfolge (erst nach Freigabe)
1. Vor jeder Änderung aktuelle Revisionen und Konfiguration erneut prüfen. Abweichungen zur BASELINE.json neu bewerten.
2. Vollständige Cloud-Run-Konfiguration nur lokal DPAPI-geschützt sichern. Bestehende Traffic-Ziele einschließlich rc1 zunächst auf ihre konkreten Revisionen festschreiben: rc1 steht aktuell auf LATEST und darf nicht unbeabsichtigt einer neuen Canary-Revision folgen.
3. Neuen Publisher mit automatischer Aufnahme als getrennte Canary-Revision ohne Standard-Traffic starten. Geschützten Capability-Handshake, Ablehnung ohne Zugang und Create/Duplicate/Stale/Tombstone ausschließlich an einem neuen synthetischen Konto prüfen.
4. Neues Backend mit automatischer Aufnahme als Canary ohne Standard-Traffic starten. JWT-/Kanal-/Identitäts-/Readiness-Sperren und neues synthetisches Profil prüfen.
5. Erst nach erfolgreichen Checks die beiden rc1-Tags auf die geprüften neuen Revisionen setzen. Standardrouten und vorhandene Privacy-Tags/Revisionen erhalten.
6. Lokales Plugin regulär beenden. Bestehende cloud-sync.json und action-channel.json sichern. In BEIDEN Dateien AutomaticPlayerEnrollment=true ergänzen; alle anderen Werte einschließlich DPAPI-Dateipfade und bisherigen AllowedUserIds erhalten.
7. Separaten Windows-Testbuild starten, ohne die alte Installation zu überschreiben. Er verwendet im normalen Modus dieselbe vorhandene Persistenz. Keine zweite Instanz gleichzeitig starten.
8. Erstaufnahme mit einem synthetischen Konto prüfen, danach die vom Nutzer freigegebenen bestehenden Konten aufnehmen lassen. Profile/Perlen vor und nach Synchronisation vergleichen. Neustart und Nullsaldo prüfen.
9. Twitch-Frontend benötigt hierfür keinen neuen Build. Ein neuer Twitch-Nutzer ohne bestehendes Plugin-Profil bzw. ohne bisherige Perlen ist dadurch noch nicht automatisch registriert: erst vorhandener normaler Chat-/Plugin-Vorgang mit Perlen führt zur Aufnahme.
10. Reviewer-Zugang und Betriebszeit des Plugins weiterhin mit Twitch abstimmen. Danach Einreichung fortsetzen.

## Auswirkungen
Bei Aktivierung werden zusätzliche bestehende und künftig berechtigte Spielerprofile automatisch an Firestore übertragen und über den bisherigen authentifizierten Zugriff erreichbar. Vorhandene Spielerbestände werden nicht zurückgesetzt oder migriert.
Mehr Spieler verursachen zusätzliche Firestore-Lese-/Schreibzugriffe. Die vorhandenen Ratenbegrenzungen bleiben unverändert; große Erstsynchronisationen können mehrere Wiederholungsrunden benötigen. Kein Versprechen beliebiger Nutzerzahlen oder gleicher Latenz bei hoher Last.
Das Windows-Paket ist eine Testversion 2.1.29-auto-sync.1; kein GitHub-Release und kein Auto-Updater-Update.

## Rollback
- Plugin regulär beenden; beide lokalen AutomaticPlayerEnrollment-Schalter auf false setzen bzw. gesicherte Konfiguration wiederherstellen.
- rc1-Tags auf die in BASELINE.json gespeicherten konkreten Vorgängerrevisionen setzen. Standardrouten/Privacy-Tags unverändert erhalten.
- Vorherige Plugin-Version starten. Neue Cloud-Profile bleiben gespeichert; NICHT löschen oder auf alte Datenstände zurücksetzen. Spieler außerhalb der alten Freigabeliste können nach Rollback vorübergehend keine Extension-Aktionen ausführen.
- Persistente Outbox und Aktionsjournale erhalten. Niemals zum Rollback Spielstände oder Bestätigungsdateien löschen.

## Noch offene Live-Prüfungen
Canary-/IAM-Prüfung gegen tatsächliches Firestore, echte erstmalige Aufnahme und Wiederanlauf nach Aktivierung sowie Twitch-Mehrspielerprüfung. Die lokalen Firestore-Tests verwenden einen Transaktions-Transport-Doppelgänger, keine vollständige Firestore- oder IAM-Emulation.
