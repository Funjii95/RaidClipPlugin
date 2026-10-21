using System.Text.Json;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private bool _maintenanceBusy;
    private bool _connectionTestBusy;

    private Task ResetPointDataAsync() => RunExclusiveRpgChangeAsync(ResetPointDataCoreAsync);
    private Task ImportMinigameDataAsync() => RunExclusiveRpgChangeAsync(ImportMinigameDataCoreAsync);

    private async Task RunExclusiveRpgChangeAsync(Func<Task> action)
    {
        if (_accountLoginBusy || _dataAdministrationBusy || _maintenanceBusy || _connectionTestBusy || _updateBusy || _shutdown is not null || _stoppingTasks.Any(task => !task.IsCompleted))
        { AppendLog("Plugin vor der RPG-Verwaltung vollständig stoppen und laufende Vorgänge beenden."); return; }
        _dataAdministrationBusy = true;
        try { await action(); }
        finally { _dataAdministrationBusy = false; }
    }

    private Control BuildMaintenancePanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(20) };
        var info = new Label { AutoSize = true, MaximumSize = new Size(800, 0), Text =
            "Wartung nur bei vollständig gestopptem Plugin. Gespeicherte Einstellungen, RPG-Spielstände, " +
            "Discord-/Spotify-Zugänge und Cloud-Sync bleiben erhalten. Nicht gespeicherte Eingaben vorab speichern. " +
            "Für getrennte Nutzer auf demselben PC getrennte Windows-Konten verwenden." };
        var reload = NewActionButton("Neu initialisieren (Einstellungen neu laden)");
        var credentials = NewActionButton("Twitch Client ID / Secret neu einrichten");
        var repair = NewActionButton("Programmdateien reparieren / neu installieren");
        var status = new Label { AutoSize = true, MaximumSize = new Size(800, 0), Text =
            "Reparatur installiert die aktuelle oder eine neuere offizielle Version erneut. " +
            "Kein Löschen von Nutzerdaten. RPG-Zurücksetzungen befinden sich weiterhin in der gesonderten Spielerverwaltung." };
        panel.Controls.AddRange([info, reload, credentials, repair, status]);
        async Task Safe(Func<Task> action)
        {
            if (_accountLoginBusy || _maintenanceBusy || _dataAdministrationBusy || _updateBusy) return;
            // Start/connection tests disable Start before their first await. Do not race them,
            // or the background tasks that Stop has not yet successfully joined.
            if (_shutdown is not null || !_startButton.Enabled || _administrationTestOnly ||
                _stoppingTasks.Any(task => !task.IsCompleted))
            { status.Text = "Plugin zuerst vollständig stoppen und laufende Verbindungsprüfungen beenden."; return; }
            _maintenanceBusy = true;
            _startButton.Enabled = false;
            _testConnectionsButton.Enabled = false;
            SetUpdateControlsEnabled(false);
            foreach (var button in new[] { reload, credentials, repair }) button.Enabled = false;
            try { await action(); }
            catch (Exception error) { status.Text = "Wartung nicht abgeschlossen: " + error.Message; }
            finally
            {
                _maintenanceBusy = false;
                if (!IsDisposed)
                {
                    _startButton.Enabled = !_administrationTestOnly;
                    _testConnectionsButton.Enabled = true;
                    SetUpdateControlsEnabled(true);
                    foreach (var button in new[] { reload, credentials, repair }) button.Enabled = true;
                }
            }
        }
        reload.Click += async (_, _) => await Safe(() =>
        {
            if (MessageBox.Show(this, "Gespeicherte Einstellungen neu laden? Nicht gespeicherte Eingaben werden verworfen.",
                "Neu initialisieren", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return Task.CompletedTask;
            LoadSettingsIntoControls();
            UpdateAccountIdentity(null);
            ResetServiceIndicators();
            status.Text = "Einstellungen neu geladen. Plugin kann wieder gestartet werden. Alle gespeicherten Daten bleiben erhalten.";
            return Task.CompletedTask;
        });
        credentials.Click += async (_, _) => await Safe(() =>
        {
            using var wizard = new SetupWizardForm(new TwitchCredentialStore(), replaceExisting: true);
            if (wizard.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
            UpdateAccountIdentity(null);
            status.Text = "Twitch-Anwendung gespeichert. Beim nächsten Start oder Verbindungstest erneut bei Twitch anmelden. " +
                "Den gewünschten Kanal unter Allgemein prüfen. Cloud-Zuordnung wird nicht automatisch übertragen. " +
                "Verschlüsselte Sicherung der vorherigen Twitch-Zugänge: " + wizard.MaintenanceBackupPath;
            return Task.CompletedTask;
        });
        repair.Click += async (_, _) => await Safe(async () =>
        {
            if (TestDataPaths.IsTestBuild) throw new InvalidOperationException("Reparatur ist in isolierten Testversionen gesperrt.");
            var update = await _updateService.CheckAsync(UpdateService.DefaultManifestUrl, CancellationToken.None, allowCurrentVersion: true)
                ?? throw new InvalidOperationException("Keine aktuelle Reparaturversion verfügbar; ein Downgrade wird nicht ausgeführt.");
            if (MessageBox.Show(this, $"Version {update.DisplayVersion} erneut installieren?\n\nProgrammdateien werden gesichert und ersetzt. " +
                "Nutzerdaten, RPG-Spielstände und Cloud-Konfiguration bleiben erhalten. Die Anwendung wird neu gestartet.",
                "Programm reparieren", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            status.Text = "Reparaturpaket wird heruntergeladen und geprüft …";
            var staged = await _updateService.DownloadAndStageAsync(update, null, CancellationToken.None);
            UpdateService.StartUpdater(staged);
            _explicitExitRequested = true;
            _maintenanceBusy = false;
            Application.Exit();
        });
        return panel;
    }

    private IEnumerable<string> ConfiguredCloudChannels()
    {
        foreach (var name in new[] { "cloud-sync.json", "action-channel.json" })
        {
            var path = Path.Combine(TestDataPaths.Root, name);
            if (!File.Exists(path)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var properties = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
            if (properties.TryGetValue("Enabled", out var enabled) && enabled.GetBoolean())
            {
                if (!properties.TryGetValue("ChannelId", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
                    throw new InvalidDataException("Cloud-Kanal-ID fehlt. Cloud-Konfiguration zuerst prüfen.");
                yield return id.GetString()!;
            }
        }
    }

    private void EnsureCloudChannelMatches(string broadcasterId)
    {
        if (ConfiguredCloudChannels().Any(channel => channel != broadcasterId))
            throw new InvalidOperationException("Der Twitch-Kanal passt nicht zur vorhandenen Cloud-Zuordnung. " +
                "Der Start wurde zum Schutz der vorhandenen RPG-Daten abgebrochen. Cloud-Zugang für diesen Kanal separat einrichten.");
    }

    private async Task RequireRpgAdministrationScopeAsync()
    {
        if (_maintenanceBusy || _connectionTestBusy || _shutdown is not null || _stoppingTasks.Any(task => !task.IsCompleted))
            throw new InvalidOperationException("Plugin vor der RPG-Verwaltung vollständig stoppen.");
        var channels = ConfiguredCloudChannels().Distinct().ToArray();
        if (channels.Length == 0) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var session = await new AuthenticationService(_configurationService.LoadForEditing()).GetSessionAsync(timeout.Token);
        CloudMaintenancePolicy.RequireOwner(channels, session.UserId);
    }
}
