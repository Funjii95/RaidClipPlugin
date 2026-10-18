# Cloud-Integration – getrennte Testversion 2.1.22

Stand: 18.10.2026. Basis: RaidClipPlugin v2.1.22, Commit 187e4e520e4bc4f77c9039450118ce58f7cb4071. Keine zweite RPG-Engine. Die vorhandene ViewerPointStore-Persistenz bleibt maßgeblich.

## Start und getrennte Daten

1. Windows-ZIP in einen neuen Ordner entpacken. `FunjiiOtterRpgIntegratedTest.exe` starten. Die .NET-Laufzeit ist enthalten; die vorhandene WebView2-Laufzeit wird weiterhin gebraucht.
2. Vor dem Verbinden desselben Kanals die normale Plugin-Version beenden. Die Testversion blockiert den Start bei einem laufenden Prozess namens RaidClipPlugin; umbenannte Programme müssen selbst beendet werden.
3. OAuth und Einstellungen im eigenen Einrichtungsdialog konfigurieren. Es werden keine bestehenden Zugangsdaten oder Spielstände automatisch übernommen.
4. Testdaten liegen unter `%LOCALAPPDATA%\FunjiiOtterRpgIntegratedTest\isolated-appdata\RaidClipPlugin`. Optional setzt `FUNJII_RPG_TEST_DATA_ROOT` ein anderes isoliertes LocalAppData-Ersatzverzeichnis; darunter wird ebenfalls `RaidClipPlugin` angelegt. Niemals den ursprünglichen Datenordner auswählen.
5. Nur synthetische Testkonten verwenden. Falls ein vorhandener Teststand übernommen werden soll, vorher beide Programme beenden und ausschließlich eine ausdrücklich ausgewählte Kopie der vollständigen lokalen Testdaten in den getrennten Ordner übernehmen. Keine Live-Spielstände für diesen Test verwenden.

Cloud-Sync ist standardmäßig AUS. Die beigefügte `cloud-sync.example.json` ist eine Vorlage und enthält keinen Zugang. Erst nach den folgenden Voraussetzungen als `cloud-sync.json` im getrennten RaidClipPlugin-Datenordner speichern und Enabled aktivieren.

## Voraussetzung für die Cloud-Verbindung

**Der bestehende Cloud-Dienst wurde nicht aktualisiert.** Er unterstützt den neuen Vollständigkeitsvertrag `rpgProjectionVersion=2` noch nicht. Der Sender stoppt deshalb vor dem ersten Upload. Die passenden Änderungen an Publisher, Backend und Protokoll liegen im Quellcode unter `Sync/`. Ein späteres Deployment braucht eine gesonderte Freigabe und ist nicht Teil dieses Builds.

- Publisher-URL ist auf den bestehenden Testdienst und Kanal 42812664 begrenzt. Keine automatische Erzeugung von Diensten, Datenbanken oder Profilen.
- `AllowedUserIds`: ausdrücklich freigegebene numerische Twitch-Testnutzer-IDs; müssen serverseitig ebenfalls erlaubt sein. Die Beispiele 1001/1002 sind synthetisch und keine eingerichteten Cloud-Profile.
- `ProtectedKeyFile`: absoluter Pfad zur bereits vorgesehenen Publisher-Test-DPAPI-Datei, gebunden an denselben Windows-Benutzer (CurrentUser, ohne zusätzliche Entropie). Keine ADC-Datei und kein Twitch-Extension-Secret verwenden. Datei nicht verschicken oder ins Repository kopieren. Keine Secrets im JSON.
- Der Server prüft den bestehenden Publisher-Zugang gegen seinen gespeicherten Hash. Das Twitch-JWT-Secret und Firestore-Berechtigungen bleiben serverseitig. Der Desktop benötigt den dafür vorgesehenen, DPAPI-geschützten Publisher-Zugang; er wird nur im Prozessspeicher für HTTPS verwendet und nie ins Extension-Frontend gegeben. DPAPI schützt nicht gegen Schadcode im selben Windows-Konto.
- Vor erstmaliger Aktivierung muss der lokale projizierte Teststand exakt zum aktuellen Cloud-Head passen. Die Übernahme der bestehenden Revision erfolgt nur bei gleichem Inhalts-Hash. Fehlende Profile, abweichender Stand, Tombstones oder verlorene/alte Outbox benötigen eine ausdrücklich geplante Abstimmung. Es gibt kein stilles Überschreiben, Rebase oder Provisioning. Die aktuell vorhandenen sechs Profile wurden hier nicht angeglichen oder verändert.
- Nach Korrektur eines Vertrags-/Zugangskonflikts Testprogramm neu starten. Transiente Netzfehler werden automatisch wiederholt.

## Speicherung und Wiederaufnahme

Die ursprüngliche Dictionary-JSON-Struktur und Serializeroptionen bleiben erhalten. Eine RPG-Änderung wird zuerst normal lokal gespeichert. Ein Hash-Commit-Marker neben `viewer-points.json` erkennt die beiden Absturzfenster um das atomare Umbenennen. Der Marker enthält keine zweite Spieler-Datenbank. Externe Veränderungen/ältere Restores werden gesperrt und erfordern bewusste Wiederherstellung.

Der Hintergrundsender liest ausschließlich vollständig gespeicherte Dateibytes unter der vorhandenen Store-Sperre. Netzwerk und Wiederholungen laufen unabhängig vom UI. Alle tatsächlichen RPG-Profilfelder werden projiziert; zusätzlich vollständige Item- und Buff-Metadaten, Fische, Sushi, Perks, Cooldowns sowie dezimale Sushi-Regeneration. Interne CompletedActions und Historien werden nicht als öffentliches Profil übertragen; die originale lokale Idempotenzliste bleibt erhalten.

Die vorhandene Outbox unter `cloud-sync-outbox` speichert ausstehende vollständige Snapshots mit Revision, ID und Hash. Bestätigung nur bei Applied/Duplicate. Verlorene Antworten führen zum erneuten Senden derselben ID, nicht zum erneuten Spielen. Bei Netzfehlern Wiederholung nach 2/4/8/16/32/60 Sekunden. Beim Start und spätestens nach 30 Sekunden werden neuere lokal gespeicherte Änderungen erkannt. Noch nicht in die Outbox übernommene Zwischenstände dürfen zu einem aktuellen Vollsnapshot zusammengefasst werden; die lokalen Spieländerungen bleiben gespeichert. Die Outbox ist eine Zustellwarteschlange, keine Spielstandautorität.

Cloud-Revisionen werden vor dem Versand geprüft. Kein automatisches Hochzählen gegen einen abweichenden Cloud-Head. Cloud-Schreibvorgänge sind pro Profil transaktional; mehrere Spielerprofile werden nicht als eine gemeinsame Cloud-Transaktion veröffentlicht. Die lokale Datei wird immer als konsistenter Gesamtstand gelesen.

Die zusätzlichen `.commit.json`, `.writer.lock` und Outbox-Dateien nicht einzeln löschen oder aus unterschiedlichen Backups mischen. Keine Änderung des ursprünglichen Spielstandformats und keine zusätzliche Migration. Vorhandene 2.1.22-Migrations-/Backupmechanismen bleiben bestehen.

## Live-Regel

Eine frische echte Twitch-Helix-Antwort muss den Kanal als live bestätigen. Zusätzlich benötigt die aktuelle Plugin-Sitzung laufende Minigames, einen höchstens 20 Sekunden alten Heartbeat und aktive Chat-WebSocket-/Subscription-Verbindung. Abfrage alle 10 Sekunden, Timeout 5 Sekunden, maximale Bestätigungsdauer 20 Sekunden ab Beginn der Abfrage.

Offline-/Fehlerantworten sperren, verspätete oder überholte Antworten werden verworfen. Neue Broadcast-ID erfordert eine neue Plugin-Sitzung. Alte Nachrichten/Async-Kontexte dürfen auch nach Wiederverbinden keine neue Änderung speichern. Die Speicherung prüft nochmals unmittelbar vor dem Commit. Ein bereits reservierter Duelleinsatz darf beim Abbruch technisch zurückgebucht werden; das erlaubt kein neues Spiel und keinen Gewinn. Diese bestehende Rückabwicklung bleibt erhalten.

Die volle Twitch-Extension-Spielaktionsschnittstelle ist weiterhin nicht Teil dieses Auftrags. Dieser Build integriert Plugin-zu-Cloud-Spielstandsübertragung und die lokale Live-Sperre, keine neue Cloud-Spielengine oder zusätzliche Ökonomie.

## Nachbauen

Im Quellcodeordner PowerShell `./Build-CloudIntegrationTest.ps1` ausführen. Voraussetzung: .NET SDK 10, Windows/PowerShell und Paketdownload. Der Ablauf führt beide Testsuiten aus und veröffentlicht ausschließlich lokal nach `artifacts/Windows-Test`. Kein Push oder Deployment.
