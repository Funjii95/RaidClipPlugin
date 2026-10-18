# Aktionskanal – Integration und API

Stand 18.10.2026. Getrennte Testkopie von RaidClipPlugin 2.1.22, aufbauend auf Cloud-Integration b17a04d. Diese Anleitung ergänzt und ersetzt die Aussage „keine Aktionsschnittstelle“ in den älteren Übergabe-/Cloud-Test-Dokumenten. Es wurde nichts deployed oder auf Twitch hochgeladen.

## Verfügbarer Ablauf

Extension → JWT-geschütztes Backend → transaktionale Firestore-Aktionswarteschlange → ausgehender HTTPS-Abruf des laufenden Plugins → vorhandene RPG-Dienste / ViewerPointStore → bestehende Snapshot-Outbox → Publisher → Firestore rpgReadSnapshots → GET /api/v1/me.

- Keine Cloud-Spielengine, keine zusätzliche Spieler-Persistenz. Queued Actions und Zustell-/Ergebnisbelege enthalten Aufträge, keine autoritativen Spielstände.
- Die vorhandene Plugin-Speicherung, Action-Idempotenz, Regeln und Original-JSON-Struktur bleiben erhalten. Keine Migration.
- Keine neuen eingehenden lokalen Ports für den Aktionskanal; ausschließlich vom Plugin initiierte HTTPS-Verbindungen. Bereits vorhandene lokale Overlay-/WebView-Funktionen des Plugins bleiben unverändert und sind kein Cloud-Aktionszugang.
- Getrennte Windows-Testanwendung `FunjiiOtterRpgIntegratedTest.exe`, Version 2.1.22-cloudtest.2. Originalinstallation, alte Testpakete und Produktivspielstände blieben unverändert.
- Die neueste lokale React-Quelle bf1119f einschließlich aller 102 Itembilder und Demo-Funktionen wurde übernommen. Panel/Mobil verwenden die ursprüngliche grafische Oberfläche mit BackendRpgDataSource; die Demo ist ausschließlich im gesonderten Build-Modus demo verfügbar. Keine Demo-Buchung fließt in reale Anfragen.

## Freigabe und Sitzung

Das Backend prüft Twitch-Extension-JWTs mit HS256, dem serverseitigen Extension-Secret, exp/nbf ohne zusätzliche Ablauf-Toleranz, festem channel_id, erlaubter Rolle und verknüpfter numerischer user_id. Die Benutzer-ID stammt ausschließlich aus dem signierten Token. Zusätzliche Browserfelder wie UserId, Kosten, Belohnungen oder frei wählbare Zielkonten werden abgelehnt.

Der Twitch-Livestatus ist ausdrücklich keine Voraussetzung. ActionRelay nutzt ausschließlich die authentifizierte Plugin-Sitzung und frische spielbereite Heartbeats. Für RPG-Aktionen werden weder Helix Get Streams noch Helix-Zugangsdaten benötigt.

Eine serverseitig erzeugte Plugin-Sitzung wird erst nach einem authentifizierten spielbereiten Heartbeat aktiv. Heartbeats alle 4 Sekunden, Lease höchstens 15 Sekunden; Sequenz streng aufsteigend, Sendezeit maximal ±5 Sekunden. Abgelaufene/alte Sitzungen lassen sich nicht durch Heartbeats wiederbeleben. Ein Streamstart oder Streamende verändert die Sitzung nicht. Normale Abmeldung sperrt sofort, Netztrennung spätestens beim Ablauf. Die lokale StreamAdmission prüft ausschließlich die aktuelle Plugin-Verbindung, aktiviertes RPG, Spielbereitschaft und frischen lokalen Heartbeat.

Die Extension muss den SessionId-Wert aus GET /play/state als X-Rpg-Session mitgeben. Eine verspätete Anfrage aus einer alten Sitzung kann nicht in eine neue Sitzung umgehängt werden. Der Header ist kein Secret und ersetzt keine JWT-/Plugin-Authentifizierung.

Aufträge gelten zunächst höchstens 30 Sekunden. Beim Abruf wird erneut Plugin-Bereitschaft und Heartbeat-Frische geprüft; die Ausführung erhält ein höchstens 8 Sekunden langes Zeitfenster, das zusätzlich vor Ablauf der aktuellen Plugin-Lease endet. Plugin prüft Kanal, Testnutzer, Hash und lokale Sitzung. Das Zeitfenster und die lokale Plugin-Freigabe werden nochmals unmittelbar vor dem vorhandenen Spielstand-Commit geprüft. Das Plugin zieht die gesamte Abrufdauer mit einer monotonen Uhr vom serverseitig verbleibenden Fenster ab und prüft dieses Budget ebenfalls beim Commit. Eine verschobene Windows-Uhr oder erneute Zustellung verlängert die Lease nicht. Ein bloß wartender Auftrag kann deshalb nach Ablauf nicht verspätet buchen.

Im Windows-Hauptprogramm ist Remote-Freigabe zusätzlich von einer erfolgreich laufenden vollständigen Cloud-Synchronisierung abhängig. Fehlt die Konfiguration, ist der Publisher inkompatibel oder geht der Sender offline, wird keine neue Remote-Sitzung als spielbereit gemeldet. Bereits lokal gespeicherte Änderungen behalten die vorhandene durable Outbox. Normale lokale Plugin-Funktionen werden durch Cloud-Ausfall nicht beendet.

## Unterstützte Aktionen

| Kind | arguments | Vorhandener Aufruf |
|---|---|---|
| status | {} | vorhandener Store + CharacterStatService |
| inventory | {} | vorhandener Store, erlaubte Snapshot-Projektion |
| sets | {} | RpgSetService.Progress |
| perks | {} | vorhandenes Profil / Konfiguration |
| equip | instanceId, optional slot | RpgProgressionService.EquipSelectionAsync, ausschließlich stabile Instanz-ID |
| unequip | slot | RpgProgressionService.UnequipAsync |
| buyPerk | perkId | RpgProgressionService.BuyPerkAsync |
| dive | stake als dezimale Ganzzahl-Zeichenfolge | RpgAdventureService.ExecuteAsync(Dive) |
| fish | stake als dezimale Ganzzahl-Zeichenfolge | RpgAdventureService.ExecuteAsync(Fishing) |
| craft | recipeId | RpgProgressionService.CraftAsync |
| eat | recipeId | RpgProgressionService.ActivateSushiAsync |

Kosten, Zufall, Besitz, Inventar, Buffs, Belohnungen, Limits und Cooldowns werden ausschließlich im bestehenden Plugin-Code geprüft. Browser und Cloud dürfen keine dieser Größen vorgeben. Eigene Profile müssen bereits vorhanden und im Testumfang freigegeben sein; keine automatische Kontenerstellung. Bestehende Blacklists, Modulschalter und Command-Freigaben werden beachtet. Remote unterstützt zunächst ausschließlich Befehle, die im CommandRegistry als Viewer freigegeben sind; privilegierte Overrides werden sicher abgelehnt, solange kein vollständiger verifizierter Rollenweg besteht.

## Bewusst noch gesperrte Funktionen

- **Bossangriff/-abschluss:** ExecuteBossAttackAsync ist vorhanden, kann aber TickCore und gemeinsame Belohnungs-/Abschlussoperationen für mehrere Spieler auslösen. Der gemeinsame Rundenzustand wird außerdem nicht gemeinsam mit allen Spielerbuchungen atomar committed. Unter der Vorgabe „keine fremden Spielstände ändern“ ist dieser Pfad nicht sicher benutzerspezifisch freizugeben. Keine Änderung der Bossregeln vorgenommen; kein bossAttack-Endpunkt freigegeben. Eine gesonderte Entscheidung über gemeinsame Bossaktionen plus Wiederherstellung eines unterbrochenen Abschlusses ist nötig.
- **Zerlegen:** bestehende PreviewInstancesAsync/ConfirmAsync verlangen einen bestätigten, befristeten, instanzgebundenen Quote. Dafür fehlt noch ein durchgängiger Remote-Preview-/Bestätigungs-/Recovery-Vertrag; nicht auf einen ungesicherten Ein-Schritt-Aufruf reduziert.
- **Favorit/Zerlegesperre:** vorhandenes SetProtectionAsync verwendet Chat-Inventarnummern mit sitzungsgebundenem Inventar-View. Kein stabiler Instanz-ID-Aufrufweg für Remote ergänzt; deshalb gesperrt.
- **Heist, Duell, Rennen, Handel/Transfers, Moderator-/Adminbefehle, Hot-Time-Steuerung:** kein Bestandteil des individuellen RPG-Vertrags. Bestehende lokale Funktionen/Demo bleiben erhalten. Keine beliebige Chatkommando- oder Code-Ausführung über HTTP.
- **Vollständige Wiederherstellung unklarer Ergebnisse:** bewusst keine automatische Neubuchung. Falls Plugin nach Intent-Protokollierung abstürzt, bleibt der Auftrag unbekannt, bis Stand/Belege geprüft wurden. Es gibt keine automatische administrative „erneut ausführen“-Route.

## Idempotenz und Ausfälle

ActionId ist eine zufällige UUID pro absichtlicher Aktion. Die serverseitige Aktionsdatei bindet sie dauerhaft an Nutzer, kanonischen Payload-Hash, Stream und Sitzung. Erneute identische POSTs geben denselben Beleg zurück, abweichender Nutzer/Payload ergibt 409. Eine wegen veralteter Sitzung/Offline abgewiesene gültige ActionId wird als terminaler rejected-Beleg persistiert. Auch nach Wiederverbinden kann dieselbe verspätete Anfrage dadurch niemals doch noch zu einem Spiel werden. Die Extension darf diese bestätigte Ablehnung abschließen; Netzwerkfehler bleiben weiterhin offen. Terminale Belege werden nicht automatisch per TTL entfernt. Warteschlange pro Kanal maximal 128; keine stillen Löschungen/Neuvergaben von ActionIds.

Das Plugin schreibt vor jedem RPG-Aufruf einen lokalen, geflushten und atomar umbenannten Ausführungsbeleg unter action-receipts. Nach dem Aufruf wird das Ergebnis persistiert. Wiederholte Zustellung und Neustart liefern den bestehenden Beleg. Ein begonnener Beleg ohne Abschluss wird unknown und nicht erneut ausgeführt. Zusätzlich bleibt die vorhandene CompletedActions-Prüfung innerhalb des originalen Spielstands wirksam. Somit gilt: keine mehrfache Wirkung derselben ActionId; bei Absturz kann ein Auftrag absichtlich ohne Ausführung/ohne rekonstruierbares Ergebnis bleiben. Eine universelle „garantiert ausgeführt trotz beliebigem Absturz“-Zusage wäre falsch.

Bei verlorener Cloud-Antwort sendet das Plugin höchstens denselben Abschluss erneut. Alte Claims werden nach Frist unknown statt neu in die Queue gestellt. Ein späterer authentifizierter Abschluss derselben Ausführung darf unknown auflösen. Die Extension hält eine offene ActionId mit ihrer Sitzung im sessionStorage, erneuert JWTs aus dem Twitch Helper nur im Speicher und blockiert automatische neue Buchungen bei unklarem Ergebnis. Ein erneuter Prüfversuch sendet genau dieselbe ID/Anfrage, niemals eine Ersatzbuchung. Spielstand-Anzeige kann bis zum Snapshot-Versand nachlaufen; Zeitstempel bleibt sichtbar.

Keine Beleg-/Outbox-Dateien einzeln löschen, zurücksetzen oder aus verschiedenen Backups mischen. Firestore-Aufträge brauchen ebenfalls dauerhafte Aufbewahrung, bis eine explizit geplante Archivierungs-/Idempotenzstrategie besteht. Keine Secrets, OAuth-Daten oder interne Historien sind Bestandteil dieser Belege/API-Ergebnisse.

## HTTP-Vertrag

Alle Beispiele sind synthetisch und enthalten keine Zugangsdaten. Base-URL entspricht dem konfigurierten Test-Backend. JWT bzw. DPAPI-geschützter Plugin-Zugang werden im Authorization: Bearer Header über HTTPS übertragen. Keine Querystring-Tokens. Antworten: Cache-Control: no-store. Browser-CORS nur für explizite Extension-Origin und Header Authorization, Content-Type, X-Rpg-Session; Browser-UserId wird nicht angenommen.

### Extension

GET `/api/v1/play/state` (JWT):

```json
{"available":true,"reason":"ready","catalogJson":"{\"supported\":[\"buyPerk\"]}","sessionId":"11111111-1111-4111-8111-111111111111"}
```

POST `/api/v1/play/actions` (JWT, Content-Type application/json, X-Rpg-Session aus state):

```json
{"actionId":"22222222-2222-4222-8222-222222222222","kind":"buyPerk","arguments":{"perkId":"damage"}}
```

HTTP 202:

```json
{"actionId":"22222222-2222-4222-8222-222222222222","state":"queued","outcome":null}
```

GET `/api/v1/play/actions/22222222-2222-4222-8222-222222222222` (derselbe JWT-Nutzer):

```json
{"actionId":"22222222-2222-4222-8222-222222222222","state":"applied","outcome":{"success":true,"code":"applied","message":"","dataJson":"{\"Balance\":1500}"}}
```

Weitere Beispiele für arguments:

```json
{"instanceId":"vollstaendige-eigene-item-instanz-id","slot":"Ring1"}
```
```json
{"stake":"100"}
```
```json
{"recipeId":"fishing"}
```

`slot`: Weapon, Helmet, Chest, Gloves, Boots, Ring1, Ring2, Amulet. Einsätze sind kanonische nichtnegative Int64-Zeichenfolgen; keine Double-Buchungen oder Rundungen im Browser. Große Int64-Anzeigewerte werden im Client verlustfrei als Zeichenfolgen gelesen.

Nach Erfolg GET `/api/v1/me` zum tatsächlichen gespeicherten Snapshot. Erfolgreicher Aktionsbeleg bedeutet lokalen Commit, nicht atomaren gemeinsamen Commit mit der Cloud. Snapshot-Synchronisierung folgt automatisch. Serverzustände: queued, executing, applied, rejected, unknown. Read-Aktionen werden bei Erfolg ebenfalls als applied abgeschlossen, ohne Spielwirkung. Fachliche Ablehnung steht im Beleg unter rule_rejected mit Originalfehlermeldung des Plugins.

HTTP 400: ungültiger Vertrag/zusätzliche Felder; 401/403: Authentifizierung/Identitäts- oder Testumfang fehlt; 404: eigener Beleg nicht vorhanden oder fremd; 409: Sitzung/offline/Frische oder Idempotenzkonflikt; bei einer dauerhaft protokollierten Nicht-Ausführung enthält der Body einen rejected-Beleg mit passender ActionId; 413: Bodygrenze; 429: Rate-/Queue-Limit; 5xx/Timeout: nicht bestätigt, ausschließlich dieselbe ActionId prüfen. Unklarheit nicht durch eine neue ID „lösen“.

### Plugin (eigenes geschütztes Authentifizierungsschema)

- POST `/api/v1/plugin/actions/session` ohne Body → `{sessionId,expiresAt}`; allein noch keine Spielbereitschaft.
- POST `/api/v1/plugin/actions/heartbeat` → `{sessionId,sequence,sentAt,ready,catalogJson}`; Cloud prüft Twitch unabhängig. Ergebnis `{sessionId,expiresAt}`.
- POST `/api/v1/plugin/actions/claim/{sessionId}` → 204 oder `{channelId,userId,sessionId,streamId,executeBefore,intent,intentHash,observedAt}`. Benutzer und Payload kommen aus dem serverseitig gebundenen Auftrag.
- POST `/api/v1/plugin/actions/complete` → `{sessionId,actionId,outcome}`. Nur bereits geclaimte Aktion derselben Sitzung; identischer Abschluss ist idempotent.
- DELETE `/api/v1/plugin/actions/session/{sessionId}` sperrt die passende Sitzung. Alter Disconnect beendet keine neuere Sitzung.

Plugin-Zugang wird als SHA-256 des dekodierten zufälligen 32–64-Byte-Schlüssels verglichen. Er ist getrennt von Twitch-JWT und bisherigem reinem Plugin-Lesezugang. Der Plugin-Client folgt keinen Redirects, verwendet keine Cookies und begrenzt Zeit/Antwortgröße. Server begrenzt Requestgrößen, Felder, Nutzerscope und Raten. Diese Endpunkte sind keine Browser-API.

## Firestore und Konfiguration – noch nicht eingerichtet

Verwendet wird die bereits bestimmte Standard-Datenbank des Testprojekts funjii-otter-rpg, `(default)`. Snapshot-Collection bleibt rpgReadSnapshots. Neue Queue-Pfade im vorbereiteten Code:

- `rpgActionChannelsTest/{channelId}`: aktuelle Lease, Sequenz, öffentliche Katalogdaten, maximal 128 Queue-IDs.
- `rpgActionChannelsTest/{channelId}/actions/{actionId-ohne-Bindestriche}`: Auftrag, Eigentümer, Bindungen und Ergebnis.

Jede Queue-Operation verwendet eine Firestore-Transaktion; keine Prozessspeicher-Queue in Cloud Run, keine Composite-Index-Abfrage. Der MemoryRelayStore ist ausschließlich im Testing-Host mit synthetischen Konten zulässig und wird außerhalb Testing abgelehnt. Der gRPC-Testtransport bildet nur die benutzten Firestore-Transaktionen ab, keine komplette Emulator-/IAM-Prüfung.

**Vor einer tatsächlichen Verbindung nötig, separat freizugeben:** kompatiblen Backend-/Publisher-Code bereitstellen, bestehenden Snapshot-Baseline-Abgleich abschließen, Runtime-IAM um eng begrenztes Queue-Lesen/Anlegen/Aktualisieren erweitern, Browserzugriffe auf interne Queue weiterhin sperren, Extension-Fetch-Allowlist/CORS auf die Test-Backend-Origin setzen und ZIP im geschlossenen Hosted Test aktualisieren. Keine Security Rules, IAM-Rollen, Ressourcen oder Portal-Einstellungen wurden hier geändert. Server-SDK verwendet IAM, nicht Browser-Firebase-Auth; niemals direkte Firestore-Rechte für Zuschauer erteilen.

Backend-Umgebungsvariablen (keine Secrets in Dateien/Images einchecken):

| Variable | Wert/Zweck |
|---|---|
| Actions__Enabled | false standardmäßig; erst nach Freigabe true |
| Actions__Mode | Firestore |
| Actions__Collection | rpgActionChannelsTest |
| Actions__AllowedUserIds__0 usw. | ausdrücklich gewählte synthetische Testkonten |
| Actions__PluginKeyHash | Hash des vorgesehenen Plugin-Aktionszugangs, geschützt konfigurieren |
| Twitch__SecretBase64 | bestehendes serverseitiges Extension-JWT-Secret |
| Twitch__ChannelId | 42812664 für das vorhandene Testprojekt |
| Twitch__AllowedOrigins__0 | exakte HTTPS-Origin der Extension, ohne Slash |
| Data__Mode / ProjectId / DatabaseId / Collection | FirestoreReadOnly / funjii-otter-rpg / (default) / rpgReadSnapshots |

Windows: `action-channel.example.json` als `action-channel.json` im isolierten RaidClipPlugin-Datenordner anlegen, Enabled zunächst false. Der Pfad liegt standardmäßig unter `%LOCALAPPDATA%\FunjiiOtterRpgIntegratedTest\isolated-appdata\RaidClipPlugin`. Gleicher isolierter Datenordner und Instanzschutz wie im Vorgängerpaket. Optionen enthalten BackendUrl, ChannelId, AllowedUserIds und ProtectedKeyFile. Alle Aktionsnutzer müssen im aktivierten cloud-sync.json ebenfalls enthalten sein.

ProtectedKeyFile zeigt auf einen für diesen Prozess vorgesehenen DPAPI-CurrentUser-Zugang desselben Windows-Benutzers, mit zufälligem 32–64-Byte-Inhalt. Ein vorhandener Publisher-Test-DPAPI-Zugang kann nur dann wiederverwendet werden, wenn sein Hash bewusst auch als Aktionszugang freigegeben wurde; dies wurde nicht automatisch gemacht. Niemals ADC oder Twitch-Extension-Secret im Plugin verwenden. Der Klartext bleibt ausschließlich im Prozessspeicher; keine Schlüssel im Frontend/sessionStorage. DPAPI schützt nicht vor Schadcode im selben Windows-Benutzerkonto.

Keine Konten/Schlüssel wurden automatisch kopiert. OAuth und Teststand bleiben isoliert. Normale Version vor Verbinden desselben Kanals beenden. Testanwendung nicht parallel zur Vorgängertestversion starten.

## Nachbauen und Nachweis

`Build-ActionChannelTest.ps1`: Backend- und Plugin-Tests, React-Tests und Build, Windows-Publish – ausschließlich lokal. SDK .NET 10, Node 22.12+ und pnpm erforderlich. `Sync/Dockerfile.backend` ist aus dem bestehenden Dockerfile angepasst; Kontext Sync. Kein Docker-Image wurde gepusht oder deployed.

Siehe TESTERGEBNISSE_ACTIONS.md und den TRX-Auszug E2E_NACHWEIS.txt. Der vollständige synthetische Durchlauf verwendet echte ASP.NET-Endpunkte, JWT-Prüfung, den ausgehenden Plugin-Pump, originale RPG-Dienste, den automatischen SnapshotWorker, HTTP-SnapshotPublisher und den echten Firestore-.NET-SDK gegen einen lokalen Transaktions-Testtransport. Kein echter Twitch-Livestream / produktives Firestore wurde benutzt. Twitch-Helper-/Browserverkehr wird in Browsertests kontrolliert simuliert.

Referenzen: [Twitch Extension-JWTs](https://dev.twitch.tv/docs/tutorials/extension-101-tutorial-series/jwt/), [Twitch Helper / Identität](https://dev.twitch.tv/docs/extensions/reference/), [Twitch Get Streams](https://dev.twitch.tv/docs/api/reference/#get-streams), [Firestore .NET-Transaktionen](https://docs.cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/Google.Cloud.Firestore.Transaction).
