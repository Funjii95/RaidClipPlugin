# Deployment-Anleitung und ausdrückliche Freigabevorlage

Plan-ID: `otter-rpg-desktop-rc1-stage-20261018`. Status: vorbereitet, nicht freigegeben, nicht ausgeführt. Projekt funjii-otter-rpg, Region europe-west3. Erste Einreichung Desktop-Panel 0.0.1, Höhe 500. Keine öffentliche Bereitstellung und kein Twitch-Upload sind Teil dieser ersten Staging-Freigabe.

## Zwei betroffene Dienste

| Dienst | Neues Image (exakter Digest) | Neuer Tag / Verkehr | Rückkehrziel |
|---|---|---|---|
| funjii-otter-rpg-backend-test | `europe-west3-docker.pkg.dev/funjii-otter-rpg/otter-rpg-test/backend@sha256:9ef4cc27df7264dd2456a62d885754abaca24ce79ac92b19f384c6cdc5aac19b` | rc1, 0 %; alte Revision bleibt 100 % | `funjii-otter-rpg-backend-test-00003-kv5` |
| funjii-otter-rpg-publisher-test | `europe-west3-docker.pkg.dev/funjii-otter-rpg/otter-rpg-test/publisher@sha256:c703dfb2d619afc9519f7951b1b37db0709458483cacc9140514421e66e10ad8` | rc1, 0 %; alte Revision bleibt 100 % | `funjii-otter-rpg-publisher-test-00010-zgx` |

Image-Version beider Komponenten: `0.0.1-rc1-2a81f8a`. Die OCI-Archive unter images/ wurden ausschließlich lokal gebaut. Manifest- und sämtliche Blob-Hashes sind geprüft, Linux amd64, Laufzeitbenutzer 1654. Der Registry-Upload und der Cloud-Run-Start stehen aus. Ein lokal gebautes Image ist kein erfolgreicher Container-Lauf.

## Konfigurationsänderungen im synthetischen Staging

Backend, neue getaggte Revision:

- Actions__Enabled=true, Actions__Mode=Firestore, Actions__Collection=rpgActionChannelsTest.
- Actions__AllowedUserIds__0 bis __5 werden einzeln als 1001, 1002, 1003, 1004, 1005, 1006 gesetzt.
- Actions__PluginKeyHash kommt aus dem neuen Secret otter-rpg-action-key-hash-test, feste Version 1. Es ist nur der Hash des neuen DPAPI-geschützten Plugin-Zugangs, nicht der rohe Schlüssel.
- Twitch__ChannelId bleibt 42812664. Für diese synthetische Revision: Twitch__SecretBase64 aus dem bereits vorhandenen otter-rpg-test-twitch-secret, Version 1. Dieses getrennte Test-Secret ersetzt ausdrücklich nicht das reale Extension-Secret auf der alten normalen Revision.
- Data__Mode=FirestoreReadOnly, Projekt und Datenbank bleiben funjii-otter-rpg/(default), Profil-Collection rpgReadSnapshots. Plugin__Enabled bleibt false.
- Exakte bestehende CORS-Origin https://l421amjf3ukq56cmppuarkq093sru9.ext-twitch.tv erhalten. Der neue Code erlaubt den zusätzlichen X-Rpg-Session-Header. Keine Wildcard, kein localhost in Cloud-CORS.
- Neue Laufzeitidentität otter-rpg-actions-test@funjii-otter-rpg.iam.gserviceaccount.com. 1 CPU, 512 MiB, minInstances 0, maxInstances 1, Timeout 15 s. Die bisherige Laufzeitidentität bleibt unverändert lesend.

Publisher, neue getaggte Revision:

- Publisher__Mode=FirestoreProfilesTest, Projekt/Datenbank/Collection unverändert funjii-otter-rpg/(default)/rpgReadSnapshots, Kanal 42812664.
- Die sechs AllowedUserIds werden ausschließlich auf die genannten synthetischen IDs gesetzt. WritesEnabled=true. Die neue Revision darf keine der sechs bisherigen normalen Profil-IDs annehmen.
- Vorhandenen serverseitigen Publisher__ApiKeyHash und die bestehende Publisher-Laufzeitidentität unverändert übernehmen. Kein Schlüsselwechsel am alten Dienst und keine Ausgabe des Hash-/Schlüsselwerts.
- 1 CPU, 512 MiB, minInstances 0, maxInstances 1, Timeout 30 s.
- Neues Image liefert die fehlende /api/v1/snapshots/capabilities-Abfrage mit Projektionsversion 2 und die vollständige öffentliche RPG-Projektion. Das ursprüngliche lokale Spielstandformat wird nicht migriert.

## Zusätzlich anzulegende / zu ändernde Zugriffe – ebenfalls freigabepflichtig

1. Neue Laufzeitidentität für die Backend-Staging-Revision anlegen. Kein Dienstkontoschlüssel wird heruntergeladen.
2. Neue benutzerdefinierte Rolle otterRpgActionQueueWriter mit datastore.databases.get sowie datastore.entities.get/list/create/update; kein delete, kein Editor/Owner. Bindung nur an die neue Identität mit Bedingung `resource.name == "projects/funjii-otter-rpg/databases/(default)"`.
3. Wichtig: Firestore-IAM ist hier auf Datenbankebene begrenzt, nicht auf eine Collection. Der Backend-Code begrenzt Schreibzugriffe auf die konfigurierte Aktionswarteschlange; die IAM-Rechte dürfen nicht fälschlich als technisch collection-exklusiv beschrieben werden. Für strengere physische Trennung wäre eine gesondert genehmigte Datenbank-/Dienstplanung nötig.
4. Neues Secret otter-rpg-action-key-hash-test anlegen, Version 1 mit dem Hash des lokal vorbereiteten DPAPI-Schlüssels. Zugriff nur für die neue Laufzeitidentität. Zusätzlich Secret-Accessor am bestehenden Test-JWT-Secret nur für diese Identität. Kein Zugriff auf alle Projekt-Secrets und in dieser Stufe noch kein Zugriff auf das echte Twitch-Extension-Secret.
5. Images in das vorhandene Artifact-Registry-Repository otter-rpg-test hochladen. Kein neues GitHub-Repository, kein neuer Cloud-Run-Dienst, keine neue Datenbank und kein neues Netzwerk-Exposure.

## Mögliche Auswirkungen

Die normale URL jedes Dienstes bleibt zunächst zu 100 % auf der aufgenommenen alten Revision. Nur die neuen rc1-URLs führen zur synthetischen Staging-Konfiguration. Bestehende Hosted-Test-Nutzer erhalten dadurch nicht heimlich eine neue API oder neue JWT-Konfiguration.

Staging kann ausschließlich die ausdrücklich synthetischen Dokumente 1001..1006 und deren Aktionsbelege verändern. Das bereits vorhandene synthetische Profil 1001 darf für diesen Test nach Sicherung seiner Metadaten mit einer neuen, strikt höheren Revision auf den isoliert erzeugten Standardteststand gesetzt werden (2000 Testperlen); keine Rücksetzung einer alten Revision. Die sechs anderen vorhandenen Profile bleiben unverändert. Bei unklarer Herkunft oder Scope-Abweichung stoppen.

Cloud Run, Firestore, Artifact Registry und Secret Manager können Kosten verursachen. Maximal eine neue Instanz je Dienst, keine Mindestinstanz; ein aktiv pollendes Plugin kann die Staging-Instanz dennoch ständig nutzen und Firestore-Lese-/Schreibkosten erzeugen. Staging nach dem Test stoppen. Keine konkreten Eurokosten oder harte Ausgabenobergrenze zugesichert.

## Reproduzierbarer Ablauf nach ausdrücklicher Freigabe

1. Alte Revisionen, Image-Digests, Verkehr und Secrets-Referenzen erneut lesen und mit evidence/release-cloud-baseline.sanitized.json abgleichen. Bei Drift oder bereits existierendem rc1-Tag stoppen und den geänderten Plan erneut zeigen. Den Fingerprint der sechs nichtsynthetischen Profile aufnehmen; nicht nur die gesamte Collection vergleichen, weil sich Profil 1001 im Test ändern darf.
2. Lokal prüft `python Upload-ApprovedImage.py --component backend` bzw. publisher ausschließlich Archive/Hashes, ohne Netzwerk. Erst nach Freigabe werden --execute und --approved-digest mit dem jeweiligen oben angegebenen Digest verwendet. Einen aktuellen ADC-Zugriffstoken ausschließlich über stdin zuführen, nicht als Argument oder Logausgabe. Der Uploader lädt nur das genehmigte Image hoch, er deployed keinen Dienst. Danach Registry-Digest erneut lesen und exakt vergleichen.
3. Identität, Rolle, bedingte Bindung und resource-spezifische Secret-Zugriffe wie oben einrichten. Bestehende Policies mit ETag und Versionsbedingungen erhalten, keine vollständige IAM-Policy blind ersetzen. DPAPI-Rohschlüssel niemals in Cloud oder ZIP ablegen; serverseitig nur Hash.
4. Publisher mit dem obigen Image und seiner Staging-Konfiguration als neue Revision mit --no-traffic --tag rc1 bereitstellen. Normale Traffic-Zuordnung explizit auf die alte Revision pinnen, nicht auf „latest“. Image, Konfigurationsdiff, Auswirkungen und Rückkehrziel unmittelbar vor Ausführung nochmals zeigen. Readiness und tatsächliche Tag-URL prüfen; diese muss exakt `https://rc1---funjii-otter-rpg-publisher-test-cwzl75ajla-ey.a.run.app` entsprechen. Andernfalls die Plugin-Allowlist nicht auf beliebige Hosts erweitern.
5. Backend mit dem obigen Image, neuer Identität und Staging-Konfiguration ebenso --no-traffic --tag rc1 bereitstellen. Normale Traffic-Zuordnung auf der alten Revision lassen. Unmittelbar vorher dieselbe konkrete Freigabeprüfung. Tatsächliche Tag-URL muss exakt `https://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app` sein.
6. Publisher zuerst auf gültige capabilities prüfen; Backend auf Health, fehlende/falsche Authentifizierung, Kanal-/Identitätsbindung und noch nicht bereite Sitzung. Ein erfolgreicher Healthcheck allein beweist keine Aktionsrechte.
7. E2E_PROTOKOLL.md Stufe 1 mit dem isolierten Testordner und synthetischem Konto durchführen. Der neue Windows-Testbuild erlaubt nur diese beiden exakten zusätzlichen HTTPS-Hosts, weiterhin nur Kanal 42812664. Weder öffentliche lokale Ports noch Routerfreigaben nötig. Anmeldedaten nur geschützt lokal verwenden; das normale Plugin/Produktivverzeichnis nicht ersetzen.
8. Ergebnis, echte Revisionen, ActionId und neue Snapshot-Metadaten ohne Secrets protokollieren. Geschützte Profile unverändert nachweisen. Bei Fehlern neue Aktionen sperren und ROLLBACK.md verwenden; nichts als bestanden markieren.

Für manuelle gcloud-Nutzung müssen die ausführende Identität und deren Rechte separat verfügbar sein. Auf diesem Rechner ist derzeit ADC verfügbar; die Vorbereitung nutzt dessen Token nur im Speicher. Es wurde kein zusätzlicher Login verlangt und kein Token ins Paket geschrieben. Die REST- oder CLI-Umsetzung muss dieselben festgeschriebenen Images und Konfigurationswerte verwenden.

## Spätere Stufe für echte Twitch-Nutzer – nicht mitfreigegeben

Nach synthetischem Erfolg ist ein neuer konkret gezeigter Plan nötig: reale Twitch-Extension-Secret-Referenz, namentlich erlaubter Twitch-Testnutzer, dessen ausdrücklich freigegebener Testspielstand, passende Publisher-/Backend-Nutzerfreigaben sowie erst dann Routing auf die normalen URLs oder ein ausdrücklich gebauter passender Hosting-Kandidat. Die normalen URLs des bisherigen Desktop-ZIPs werden nicht automatisch umgebogen. Ein vom separaten Test-Secret signierter synthetischer JWT ist kein echter Twitch-Helper-Test.

Öffentliche Bereitstellung, Aufheben der Testnutzerbeschränkung, neue Kanalunterstützung, Twitch-Upload und Review-Einreichung erfordern jeweils gesonderte Vorbereitung/Freigabe. Sie sind wegen der dokumentierten Funktions- und Einreichungsblocker noch nicht bereit.

Offizielle Grundlage: [Cloud Run Revisionen und Traffic](https://docs.cloud.google.com/run/docs/rollouts-rollbacks-traffic-migration), [Deployment mit Image-Digest](https://docs.cloud.google.com/run/docs/deploying), [Firestore-Datenbanken und IAM-Bedingungen](https://cloud.google.com/firestore/docs/manage-databases), [Firestore Server-IAM statt Client-Regeln](https://cloud.google.com/firestore/native/docs/security/rules-structure).
