# Verbindliche Prüfung vor Cloud-Bereitstellungen

Vor jeder neuen Revision und vor jeder Traffic-Umschaltung muss Sync/scripts/Test-DeploymentConfiguration.ps1 geladen und Test-RpgDeploymentConfiguration mit den tatsächlich geplanten vollständigen Backend-/Publisher-Serviceobjekten aufgerufen werden. Prüfung im Arbeitsspeicher; keine Secrets oder vollständigen Konfigurationen in ungeschützte Dateien oder Logs schreiben.

```powershell
. ./Sync/scripts/Test-DeploymentConfiguration.ps1
Test-RpgDeploymentConfiguration -Backend $plannedBackend -Publisher $plannedPublisher
# Erst nach Erfolg die geplanten Revisionen bereitstellen bzw. Traffic umschalten.
```

Pflicht: Actions__Enabled, Actions__PrivacyDeletionEnabled, Publisher__WritesEnabled und Publisher__PrivacyDeletionEnabled jeweils true; identischer Kanal, Projekt, Datenbank, Profil-Collection und automatische Aufnahme. Fehlende Felder gelten als Fehler. Bei einem Fehler nicht deployen und keine Löschaufträge entfernen. Alte Revisionen und geschützte Konfigurationssicherung für Rollback erhalten.

Die Windows-Release-Pipeline führt sieben synthetische Konfigurationstests aus. Die Prüfung ersetzt keine Authentifizierungs-/IAM- und Laufzeitprüfung. Eine Bereitstellung außerhalb dieses Ablaufs kann nicht durch das Skript verhindert werden.

Plugin: Fehlgeschlagene Löschungen werden pro Konto mit 5/10/20/40/60 Sekunden Abstand erneut versucht. Erfolgreiche unabhängige Konten bleiben bereit. Lokale Löschsperren, Quellenprüfung, vollständige Snapshots, Revisionsschutz und persistente Warteschlange bleiben maßgeblich. Globale Vertrags-, Authentifizierungs- und Datenkonflikte werden nicht als Erfolg behandelt.
