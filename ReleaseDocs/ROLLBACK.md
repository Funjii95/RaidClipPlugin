# Rollback-Anleitung – keine Spielstände zurücksetzen

Diese Anleitung dient dem Abbruch eines freigegebenen Tests. Sie ist keine jetzt ausgeführte Änderung. Alte Revisionen, Images, ZIPs und Spielstände bleiben erhalten.

## Vorbereitete Rückkehrziele

| Dienst | Bisherige funktionsfähige Revision | Bisheriges Image |
|---|---|---|
| funjii-otter-rpg-backend-test | `funjii-otter-rpg-backend-test-00003-kv5` | `europe-west3-docker.pkg.dev/funjii-otter-rpg/otter-rpg-test/backend@sha256:988b2c4a002c620a8228028003d775c9a544cdfb5364cb857e48daf5a45334bd` |
| funjii-otter-rpg-publisher-test | `funjii-otter-rpg-publisher-test-00010-zgx` | `europe-west3-docker.pkg.dev/funjii-otter-rpg/otter-rpg-test/publisher:16d76ebd-c420-4587-a1f1-573c3613e2dc` |

Vor jeder Ausführung erneut mit dem tatsächlichen aktuellen Bestand vergleichen. Bei fremden Änderungen keine veraltete Baseline blind überschreiben.

## Bei einem Fehler im getaggten Staging

1. Keine neuen Spielaktionen senden. Isoliertes Testplugin stoppen bzw. Aktionskanal deaktivieren. Bereits angenommene ActionIds und lokalen Ergebnisbelege aufbewahren; unklare Ergebnisse nur unter derselben ID prüfen.
2. Alte normale Revisionen bleiben nach Plan ohnehin auf 100 %. Bei Bedarf explizit dort festschreiben und ausschließlich den neu angelegten rc1-Tag entfernen. Beispiel (vom berechtigten Betreiber, nach Bestandsprüfung):

```powershell
gcloud run services update-traffic funjii-otter-rpg-backend-test --project funjii-otter-rpg --region europe-west3 --to-revisions funjii-otter-rpg-backend-test-00003-kv5=100 --remove-tags rc1
gcloud run services update-traffic funjii-otter-rpg-publisher-test --project funjii-otter-rpg --region europe-west3 --to-revisions funjii-otter-rpg-publisher-test-00010-zgx=100 --remove-tags rc1
```

3. Health und Authentifizierung auf den normalen URLs prüfen. Entfernte Tags verhindern weiteren Zugriff auf die Testrevision über diese Tag-URL. Keine neue Revision oder Image-Version löschen.
4. Falls die Staging-Zugriffe zurückgenommen werden sollen, ausschließlich die neu angelegten Rollen-/Secret-Bindungen der neuen Laufzeitidentität entfernen, nach Prüfung der verwendeten Ressourcen. Bestehende Reader-/Publisherrechte, andere Mitglieder und alte Secret-Versionen nicht verändern oder löschen. Auch diese Rechteänderung ist bewusst auszuführen, kein pauschales IAM-Reset.
5. Neue synthetische Testbelege und Profile vorerst erhalten. Kein automatisches Aufräumen, kein Rückschreiben alter Snapshot-Revisionen und kein Zurücksetzen der sechs geschützten Profile. Benötigte spätere Bereinigung gesondert planen und freigeben.

## Lokale Testversion

Die normale RaidClipPlugin-Installation wurde nicht ersetzt. Zum Rückkehrbetrieb die neue getrennte Testanwendung schließen und die unveränderte bisherige Version starten. Isolierte Testordner und Outbox-/Commit-/Receipt-Dateien als zusammenhängenden Satz aufbewahren. Keine Dateien aus verschiedenen Sicherungen mischen.

## Twitch

Aktuell wurde kein neuer Upload vorgenommen. Wenn später ein freigegebener Upload fehlschlägt, die 0.0.1 wieder in Local Test zurücknehmen und das vorher gesicherte passende ZIP sowie die vorherigen Hosting-Einstellungen wiederherstellen; danach erneut geschlossen testen. Den Mobil-Schalter nur zur zuvor tatsächlich gesicherten Version passend setzen. Keine neue oder alte Extension löschen, keinen öffentlichen Release als Rollback-Werkzeug verwenden.

Ein Routing-Rollback macht erfolgreich gebuchte Spielaktionen nicht rückgängig. Sie bleiben autoritativ im Plugin und dürfen nicht durch einen alten Cloud-Snapshot überschrieben werden. Unklare Aktionen werden abgeglichen, nicht erneut gebucht.
