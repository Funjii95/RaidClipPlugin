# Geänderte Dateien – grafische Backend-Anbindung

Basis: 0844c6e. Separate Testkopie; die ursprüngliche Installation blieb unverändert.

Die Spielfreigabe hängt ausschließlich am authentifiziert verbundenen und spielbereiten Plugin mit frischem Heartbeat. Keine Twitch-Live-Pflicht.

Details und Zuordnung zu Tests: GRAPHICAL_BACKEND_INTEGRATION.md.

- .gitignore
- ACTION_CHANNEL.md
- Build-ActionChannelTest.ps1
- CLOUD_INTEGRATION_TEST.md
- GRAPHICAL_BACKEND_INTEGRATION.md
- RaidClipPlugin/MainForm.CloudIntegration.cs
- RaidClipPlugin/MainForm.cs
- RaidClipPlugin/Services/RemoteRpgExecutor.cs
- RaidClipPlugin/Services/StreamAdmission.cs
- RaidClipPlugin/Tests/RemoteActionTests.cs
- RaidClipPlugin/Tests/StreamAdmissionTests.cs
- Sync/src/Backend/ActionRelay.cs
- Sync/src/Backend/BackendSettings.cs
- Sync/src/Backend/Program.cs
- Sync/src/SnapshotDelivery/packages.lock.json
- Sync/src/SnapshotProtocol/packages.lock.json
- Sync/tests/Backend.Tests/ActionRelayTests.cs
- TESTERGEBNISSE.md
- TESTERGEBNISSE_ACTIONS.md
- TESTERGEBNISSE_GRAPHICAL.md
- twitch-extension/MINIGAMES-INTEGRATION.md
- twitch-extension/README.md
- twitch-extension/TEST-RESULTS.md
- twitch-extension/package.json
- twitch-extension/scripts/serve.mjs
- twitch-extension/src/GraphicalApp.tsx
- twitch-extension/src/demo-entry.tsx
- twitch-extension/src/live/LivePanel.tsx
- twitch-extension/src/live/client.ts
- twitch-extension/src/main.tsx
- twitch-extension/src/presentation.ts
- twitch-extension/src/remote/BackendRpgDataSource.ts
- twitch-extension/src/remote/model.ts
- twitch-extension/src/types.ts
- twitch-extension/src/vite-env.d.ts
- twitch-extension/tests/hosting.test.ts
- twitch-extension/tests/item-art.spec.ts
- twitch-extension/tests/live.spec.ts
- twitch-extension/tests/minigames.spec.ts
- twitch-extension/tests/remote-fixtures.ts
- twitch-extension/tests/remote.test.ts
- twitch-extension/tests/ui.spec.ts
- GEAENDERTE_DATEIEN_GRAPHICAL.md
