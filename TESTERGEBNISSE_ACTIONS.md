> Historischer Bericht des vorigen Arbeitspakets. Für den aktuellen Build gelten GRAPHICAL_BACKEND_INTEGRATION.md und TESTERGEBNISSE_GRAPHICAL.md im Repository-Stamm. Eine frühere Twitch-Live-Pflicht ist aufgehoben; Spielfreigabe nur nach Plugin-Bereitschaft und frischem Heartbeat.

# Testergebnisse – sicherer RPG-Aktionskanal

18.10.2026; Windows x64, .NET SDK 10.0.400, Node 24.22.0. Ausschließlich synthetische Konten. Neue getrennte Testversion 2.1.22-cloudtest.2; keine vorhandene Installation überschrieben.

| Prüfung | Ergebnis |
|---|---|
| Plugin-Gesamtsuite inklusive echter autoritativer RPG-Dienste | 672 bestanden |
| Backend-/Publisher-/Firestore-Vertragstests | 195 bestanden |
| TypeScript/Vitest inklusive neuer Live-Adaptertests | 60 bestanden |
| Browser-/Panel-/Mobiltests inklusive bisheriger Demo/102 Bilder | 18 bestanden |
| Summe | **945 Tests bestanden** |
| Windows x64 self-contained Release | erfolgreich kompiliert/veröffentlicht als lokales Testpaket |
| Tatsächliches Hauptfenster im isolierten Starttest | erfolgreich, Exit-Code 0 |
| Backend Release-Publish und Wiederherstellung mit Lockfile | erfolgreich |
| React/TypeScript/Vite-Produktionsbuild | erfolgreich |
| Git diff whitespace check | bestanden |

Die alten Tests und Features bleiben enthalten; 33 zusätzliche Tests gegenüber dem Cloud-Paket plus der bereits bestehenden Extension-Suite. Übliche Paket-/Nullable-/WindowsBase-/Analyzer-Warnungen aus dem Ausgangsprojekt bleiben sichtbar. Kein pauschales Abschalten von Warnungen. Docker-Rezept vorbereitet; kein Image-Push oder Cloud-Deployment.

## Vollständiger Durchstich

Der Test `Full_HTTP_queue_plugin_sync_firestore_and_viewer_snapshot_roundtrip` prüft in einem Ablauf:

1. Synthetischer JWT mit gültiger HS256-Signatur bindet Nutzer 1001 an Kanal 42. Lokaler Ausgangsstand: 2.000 Perlen, Cloud-Snapshot Revision 1.
2. Geschützter Plugin-Client öffnet die ausgehende Sitzung und sendet einen frischen Heartbeat. Backend bestätigt den Stream unabhängig über einen kontrollierten Twitch-Testadapter.
3. Zuschauer stellt `buyPerk damage` mit eindeutiger ActionId und aktueller Sitzung über HTTP ein. Der echte Firestore-SDK schreibt die transaktionale Warteschlange gegen einen lokalen gRPC-Testtransport.
4. Der tatsächlich laufende CloudActionClient-Pump ruft den Auftrag selbst ausgehend ab. RemoteRpgExecutor verwendet die vorhandene RpgProgressionService/ViewerPointStore-Logik. Keine Cloud-Kostenberechnung und keine Ersatzengine.
5. Ein Kauf: Guthaben 1.500, Perk damage Level 1. Das Plugin quittiert applied mit derselben ActionId.
6. Der automatisch laufende IntegratedSnapshotWorker wird durch die echte Speicherung geweckt; vorhandene SnapshotDelivery sendet per HTTP an SnapshotPublisher, der echte Firestore-SDK speichert Revision 2.
7. JWT-geschütztes GET /api/v1/me liefert 1.500 Perlen und Perk-Level 1 aus diesem Snapshot, ohne CompletedActions.
8. Wiederholung derselben ActionId nach Neuerzeugung des Executors liefert den vorhandenen Ergebnisbeleg; Guthaben bleibt 1.500.

Der unveränderte Testausgabe-Auszug steht in `E2E_NACHWEIS.txt`, die maschinenlesbare Gesamtausgabe in `Plugin-ActionChannel.trx`. Zusätzlicher Browsernachweis `Live-Aktion.png` zeigt den synthetischen gespeicherten Stand nach bestätigter Aktion bei 320px Panelbreite.

## Sicherheits- und Ausfallprüfungen

- Ungültige Signatur, abgelaufener JWT, falscher Kanal und fehlende Benutzeridentität abgelehnt. Vorhandene Tests prüfen außerdem Rolle, anonyme/unverknüpfte Identität und Trennung der Plugin-/Twitch-Zugänge.
- Browser-UserId/zusätzliche Felder und doppelte JSON-Feldnamen abgelehnt. Kein beliebiger Chatbefehl, keine frei wählbaren Zielkonten, Preise oder Belohnungen.
- Fremder Nutzer kann einen Beleg weder lesen noch eine fremde ActionId übernehmen. Fremdes Item abgelehnt.
- Stream offline, getrenntes Plugin, Verbindung ohne spielbereiten Heartbeat, abgelaufener Heartbeat und falsche Reihenfolge/Zeit/Sitzung blockieren.
- Alte Browser-Sitzung wird nicht auf eine neue Plugin-Sitzung umgehängt. Ein dauerhaft abgewiesener Auftrag bleibt auch nach Wiederverbinden abgewiesen.
- Claim-Fenster endet spätestens mit der aktuellen Plugin-Lease; Abrufdauer wird monoton abgezogen, Speichersperre prüft Zeit-/Live-Frische nochmals beim Commit. Eine wartende, inzwischen abgelaufene Operation schreibt nichts.
- Gleichzeitige identische POSTs, wiederholte Bestätigung und Executor-Neustart führen nicht zu mehrfacher Buchung. Startbeleg ohne Abschluss bleibt unknown statt erneut zu spielen.
- Zu wenig Perlen, fehlendes eigenes Item und aktiver Tauch-Cooldown werden durch die originalen Regeln abgelehnt. Ausrüstung an-/ablegen, Crafting, Sushi-Aktivierung sowie Charakter-/Inventar-/Set-/Perk-Lesen über vorhandene Dienste geprüft.
- Firestore-Queue überlebt Austausch der Store-Instanz und wiederholt echte SDK-Transaktionen nach simuliertem Optimistic-Concurrency-Abbruch.
- Vorhandene Snapshot-Tests prüfen weiterhin Netzverlust, verlorene ACKs, Neustart, Revisionen/Hash, vollständig gespeicherte Daten und unveränderten ursprünglichen JSON-Inhalt.
- Live-Frontend hält dieselbe ActionId über Verlust einer Antwort/Neuladen; unbekannte Ergebnisse blockieren neue automatische Buchungen. Konto-/JWT-Wechsel und verlustfreie Int64-Anzeige geprüft. Keine Mock-Buchung als realer Erfolg.
- Vorhandene Demo-Tests bleiben erhalten, einschließlich Inventar, Itemgrafiken, Sushi, Boss-Demo, Perlen-Minispiele, Wiederverbinden und schmaler Panel-/Mobilansicht.

## Grenzen

Die Prüfungen verwenden lokale ASP.NET-Testhosts, synthetische Twitch-Helper-/Helix-Antworten und den echten Firestore-SDK gegen einen begrenzten gRPC-Transaktions-Testtransport. Sie sind kein Nachweis für reale Twitch-OAuth-Scopes, Cloud-IAM, echte Firestore-Verfügbarkeit oder einen vollständigen Emulator. Der echte Livestream-/Hosted-Test-End-to-End-Test steht nach gesonderter Bereitstellungsfreigabe noch aus.

Bossabschlüsse, Zerlegen mit Remote-Bestätigung, Favorit-/Zerlegesperren sowie gemeinschaftliche/administrative Minispiele sind ausdrücklich nicht freigegeben. Gründe und vorhandene Aufrufstellen stehen in ACTION_CHANNEL.md. Keine fehlenden Regeln ersetzt oder erfunden.

Cloud-Sync und Aktionskanal sind standardmäßig deaktiviert. Bestehendes Cloud-Backend/Publisher und aktuelles Twitch-ZIP wurden nicht verändert. Erforderliche kompatible Serverupdates, IAM-/Allowlist-Konfiguration und Test-Baseline-Abgleich wurden nicht durchgeführt. Kein öffentlicher Release und kein Deployment.

Die ursprüngliche Spielstandsdatei blieb unverändert (SHA-256):
`E178D117F6D78C9ABB914B9DC0281BD12AD57963C234AB48EEFD92103A23430E`.
