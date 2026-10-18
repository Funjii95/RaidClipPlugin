# Geänderte Dateien

Separate Arbeitskopie, Branch codex/integrated-cloud-test, Basis v2.1.22 (187e4e520e4bc4f77c9039450118ce58f7cb4071). Kein Push.

## Bestehende Plugin-Dateien

- `RaidClipPlugin/MainForm.EventTriggers.cs`
- `RaidClipPlugin/MainForm.ModernShell.cs`
- `RaidClipPlugin/MainForm.cs`
- `RaidClipPlugin/Program.cs`
- `RaidClipPlugin/RaidClipPlugin.csproj`
- `RaidClipPlugin/Services/AdventureGameService.cs`
- `RaidClipPlugin/Services/AuthenticationService.cs`
- `RaidClipPlugin/Services/ChatExtensionManager.cs`
- `RaidClipPlugin/Services/ChatMinigameService.cs`
- `RaidClipPlugin/Services/ClipHistoryService.cs`
- `RaidClipPlugin/Services/ConfigurationService.cs`
- `RaidClipPlugin/Services/DiscordCredentialStore.cs`
- `RaidClipPlugin/Services/GiveawayStore.cs`
- `RaidClipPlugin/Services/ModerationCenterServices.cs`
- `RaidClipPlugin/Services/MusicRequestStore.cs`
- `RaidClipPlugin/Services/RpgDebugSession.cs`
- `RaidClipPlugin/Services/SpotifyService.cs`
- `RaidClipPlugin/Services/StartupDiagnostics.cs`
- `RaidClipPlugin/Services/TwitchChatWebViewService.cs`
- `RaidClipPlugin/Services/TwitchCredentialStore.cs`
- `RaidClipPlugin/Services/UpdateApplier.cs`
- `RaidClipPlugin/Services/ViewerPointStore.Rpg.cs`
- `RaidClipPlugin/Services/ViewerPointStore.cs`
- `RaidClipPlugin/Tests/DuelServiceTests.cs`

MainForm/ChatMinigameService/AdventureGameService: bestehender Startablauf, neue Live-/Sitzungsprüfung und Sender-Lebenszyklus. ViewerPointStore: vorhandene Speicherung plus Hash-Commit-Intent, Commit-Sperre und Benachrichtigung. Sonstige Dienste mit Datenpfaden: ausschließlich Umleitung auf isoliertes Test-AppData. Program/csproj: eigener Programmname, Instanzsperre und deaktivierter Updateablauf. DuelServiceTests: Synchronisation des asynchronen Testabschlusses.

## Hinzugefügt

- `.gitignore`
- `Build-CloudIntegrationTest.ps1`
- `CLOUD_INTEGRATION_TEST.md`
- `RaidClipPlugin/MainForm.CloudIntegration.cs`
- `RaidClipPlugin/Services/IntegratedSnapshotWorker.cs`
- `RaidClipPlugin/Services/SaveCommitMarker.cs`
- `RaidClipPlugin/Services/StreamAdmission.cs`
- `RaidClipPlugin/Services/ViewerPointStore.Cloud.cs`
- `RaidClipPlugin/TestDataPaths.cs`
- `RaidClipPlugin/Tests/CloudIntegrationTests.cs`
- `RaidClipPlugin/Tests/StreamAdmissionTests.cs`
- `Sync/Directory.Build.props`
- `Sync/src/Backend/Backend.csproj`
- `Sync/src/Backend/BackendSettings.cs`
- `Sync/src/Backend/Players.cs`
- `Sync/src/Backend/PluginAuthentication.cs`
- `Sync/src/Backend/Program.cs`
- `Sync/src/Backend/packages.lock.json`
- `Sync/src/SnapshotDelivery/HttpSnapshotHeadReader.cs`
- `Sync/src/SnapshotDelivery/HttpSnapshotSender.cs`
- `Sync/src/SnapshotDelivery/OutboxDeliveryWorker.cs`
- `Sync/src/SnapshotDelivery/OutboxDispatcher.cs`
- `Sync/src/SnapshotDelivery/SnapshotDelivery.csproj`
- `Sync/src/SnapshotDelivery/packages.lock.json`
- `Sync/src/SnapshotFirestore/FirestoreSnapshotSink.cs`
- `Sync/src/SnapshotFirestore/SnapshotFirestore.csproj`
- `Sync/src/SnapshotFirestore/packages.lock.json`
- `Sync/src/SnapshotProtocol/FullRpgFields.cs`
- `Sync/src/SnapshotProtocol/ISnapshotSink.cs`
- `Sync/src/SnapshotProtocol/LocalOutboxStore.cs`
- `Sync/src/SnapshotProtocol/LocalSnapshotStore.cs`
- `Sync/src/SnapshotProtocol/SnapshotAcknowledgement.cs`
- `Sync/src/SnapshotProtocol/SnapshotHead.cs`
- `Sync/src/SnapshotProtocol/SnapshotMessage.cs`
- `Sync/src/SnapshotProtocol/SnapshotProtocol.csproj`
- `Sync/src/SnapshotProtocol/packages.lock.json`
- `Sync/src/SnapshotPublisher/Program.cs`
- `Sync/src/SnapshotPublisher/PublisherSettings.cs`
- `Sync/src/SnapshotPublisher/SnapshotPublisher.csproj`
- `Sync/src/SnapshotPublisher/packages.lock.json`
- `Sync/tests/Backend.Tests/ApiTests.cs`
- `Sync/tests/Backend.Tests/Backend.Tests.csproj`
- `Sync/tests/Backend.Tests/DeliveryTests.cs`
- `Sync/tests/Backend.Tests/FirestorePublisherTests.cs`
- `Sync/tests/Backend.Tests/FirestoreTransportTests.cs`
- `Sync/tests/Backend.Tests/FullProjectionTests.cs`
- `Sync/tests/Backend.Tests/LocalOutboxTests.cs`
- `Sync/tests/Backend.Tests/ProfilePublisherScopeTests.cs`
- `Sync/tests/Backend.Tests/PublisherConfigurationTests.cs`
- `Sync/tests/Backend.Tests/PublisherTests.cs`
- `Sync/tests/Backend.Tests/SnapshotHeadTests.cs`
- `Sync/tests/Backend.Tests/SnapshotProtocolTests.cs`
- `Sync/tests/Backend.Tests/TransactionalFirestoreFixture.cs`
- `Sync/tests/Backend.Tests/packages.lock.json`
- `TESTERGEBNISSE.md`
- `cloud-sync.example.json`
- `GEAENDERTE_DATEIEN.md` (diese Liste)

## Wiederverwendeter Snapshot-Code

Die Quellen unter Sync stammen aus dem bereits vorhandenen Backend-Arbeitsstand 57f4c02112b1417d12ba97698e1111aede044a8b. Es wurden keine alten Arbeitskopien verändert. Die Bibliotheken wurden mitkopiert, damit der Testbuild eigenständig kompilierbar bleibt.

Gezielte Erweiterungen gegenüber dieser Quelle:

- SnapshotProtocol/SnapshotMessage.cs und FullRpgFields.cs: vollständige tatsächliche RPG-Item-/Buff-Metadaten und dezimale Regeneration, Projektionskennung 2; Transportvertragsversion bleibt 1.
- SnapshotProtocol/LocalOutboxStore.cs: exakter Hash-/Revisions-Baseline-Anschluss an vorhandenen Cloud-Head.
- SnapshotDelivery/HttpSnapshotHeadReader.cs: authentifizierter Vollständigkeitsvertrag.
- SnapshotPublisher/Program.cs: geschützter capabilities-Endpunkt.
- Backend/Players.cs: dezimaler Health-Wert kompatibel zur vorhandenen Speicherung.
- Backend.Tests/FullProjectionTests.cs: drei neue Vertragstests; die 177 vorhandenen Tests bleiben erhalten.

Alle anderen kopierten Snapshot-, Firestore- und Backend-Dateien sind Wiederverwendung bestehender Implementierungen. Produktiver Server und ursprüngliche Plugin-Installation wurden nicht verändert.
