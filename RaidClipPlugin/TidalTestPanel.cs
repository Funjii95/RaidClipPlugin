using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using RaidClipPlugin.Models;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed class TidalTestPanel : UserControl
{
    public TidalTestPanel(Color? background = null, Color? inputColor = null, Color? textColor = null)
    {
        Dock = DockStyle.Fill;
        var BackgroundColor = background ?? Color.FromArgb(10, 14, 20);
        var InputColor = inputColor ?? Color.FromArgb(20, 27, 36);
        var TextColor = textColor ?? Color.WhiteSmoke;
        Button NewActionButton(string caption) => new()
        {
            Text = caption, AutoSize = true, Height = 32, FlatStyle = FlatStyle.Flat,
            BackColor = InputColor, ForeColor = TextColor, Padding = new Padding(6, 3, 6, 3)
        };
        var provider = new TidalMusicProvider();
        var cancellation = new CancellationTokenSource();
        var lifetime = cancellation.Token;
        WebView2? browser = null;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12), BackColor = BackgroundColor };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = TextColor,
            Text = "TIDAL zusätzlich zu Spotify · Experimenteller Link-/Player-Test\n\nNur eine getrennte lokale Merkliste, kein automatischer Musikwunsch. Keine Kanalpunkte, Chatmeldungen oder Änderungen an Spotify. Songs im Original-TIDAL-Player manuell starten. Vollständige Titel hängen von TIDAL-Konto, Abo und Embed-Freigabe ab; sonst ggf. nur Vorschauen. Textsuche/API-Anmeldung benötigen eine eigene Entwickler-App."
        }, 0, 0);
        var input = new TextBox { Width = 480, MaxLength = 2048, PlaceholderText = "https://tidal.com/track/…", BackColor = InputColor, ForeColor = TextColor };
        var add = NewActionButton("Link lokal vormerken");
        var reload = NewActionButton("Liste laden");
        var show = NewActionButton("Im TIDAL-Player öffnen");
        var external = NewActionButton("Im Browser öffnen");
        var remove = NewActionButton("Aus Merkliste entfernen");
        var close = NewActionButton("Player schließen");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        actions.Controls.AddRange([input, add, reload, show, external, remove, close]);
        layout.Controls.Add(actions, 0, 1);
        var status = new Label { Dock = DockStyle.Fill, ForeColor = TextColor, Text = "Noch kein TIDAL-Player geladen. Keine Wiedergabe aktiv bestätigt." };
        layout.Controls.Add(status, 0, 2);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AutoGenerateColumns = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false, BackgroundColor = InputColor
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TIDAL-Link (Titel ungeprüft)", DataPropertyName = nameof(SongSearchResult.ExternalUrl), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        layout.Controls.Add(grid, 0, 3);
        var player = new Panel { Dock = DockStyle.Fill, BackColor = InputColor, Padding = new Padding(6) };
        player.Controls.Add(new Label { Dock = DockStyle.Fill, ForeColor = TextColor, Text = "Wähle einen vorgemerkten Link und öffne den TIDAL-Player.\nDer offizielle Player zeigt Titel, Künstler und die verfügbaren Wiedergabeoptionen.\nEine Anmeldung erfolgt dort direkt bei TIDAL, nicht über Spotify oder das Plugin." });
        layout.Controls.Add(player, 0, 4);
        SongSearchResult? Selection() => grid.CurrentRow?.DataBoundItem as SongSearchResult;
        async Task LoadAsync()
        {
            var entries = await provider.GetEntriesAsync(lifetime);
            if (!layout.IsDisposed) grid.DataSource = entries.ToList();
        }
        async Task RunAsync(Func<Task> operation)
        {
            try { await operation(); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception ex) { if (!layout.IsDisposed) status.Text = "TIDAL-Test: " + ex.Message; }
        }
        add.Click += async (_, _) => await RunAsync(async () =>
        {
            var tracks = await provider.SearchTracksAsync(input.Text, lifetime);
            var result = await provider.AddSongRequestAsync(tracks[0], lifetime);
            if (!layout.IsDisposed) status.Text = result.Message;
            await LoadAsync();
        });
        reload.Click += async (_, _) => await RunAsync(LoadAsync);
        remove.Click += async (_, _) => await RunAsync(async () =>
        {
            if (Selection() is not { } selected) { status.Text = "Bitte einen TIDAL-Link auswählen."; return; }
            await provider.RemoveAsync(selected.Id, lifetime);
            await LoadAsync();
            status.Text = "Nur aus der lokalen TIDAL-Merkliste entfernt. Wiedergabe bleibt unverändert.";
        });
        external.Click += (_, _) =>
        {
            if (Selection() is { } selected && TidalMusicProvider.TryResolveTrack(selected.ExternalUrl, out var safe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(safe.ExternalUrl) { UseShellExecute = true });
            else status.Text = "Bitte einen gültigen TIDAL-Link auswählen.";
        };
        show.Click += async (_, _) => await RunAsync(async () =>
        {
            if (Selection() is not { } selected || !TidalMusicProvider.TryResolveTrack(selected.ExternalUrl, out var safe))
            { status.Text = "Bitte einen gültigen TIDAL-Link auswählen."; return; }
            show.Enabled = false;
            try
            {
                if (browser is null)
                {
                    var candidate = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(12, 15, 19) };
                    browser = candidate;
                    foreach (Control placeholder in player.Controls.Cast<Control>().ToArray()) placeholder.Dispose();
                    player.Controls.Add(candidate);
                    var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(TestDataPaths.LocalAppData, "RaidClipPlugin", "TidalWebView2"));
                    if (candidate.IsDisposed || layout.IsDisposed) return;
                    await candidate.EnsureCoreWebView2Async(environment);
                    if (candidate.IsDisposed || layout.IsDisposed) return;
                    candidate.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    candidate.CoreWebView2.DownloadStarting += (_, args) => args.Cancel = true;
                    candidate.CoreWebView2.NavigationStarting += (_, args) =>
                    {
                        if (!TidalMusicProvider.IsTrustedNavigation(args.Uri)) { args.Cancel = true; status.Text = "Navigation außerhalb von TIDAL wurde blockiert."; }
                    };
                    candidate.CoreWebView2.NewWindowRequested += (_, args) =>
                    {
                        args.Handled = true;
                        if (TidalMusicProvider.IsTrustedNavigation(args.Uri)) candidate.CoreWebView2.Navigate(args.Uri);
                    };
                    candidate.NavigationCompleted += (_, args) =>
                    {
                        if (!layout.IsDisposed) status.Text = args.IsSuccess
                            ? "TIDAL-Seite geladen – Wiedergabe nicht bestätigt. Bitte Play im Originalplayer drücken; nach Anmeldung ggf. den ausgewählten Link erneut öffnen."
                            : "TIDAL-Player konnte nicht geladen werden: " + args.WebErrorStatus;
                    };
                }
                if (browser?.CoreWebView2 is null) throw new InvalidOperationException("Player nicht bereit. Player schließen und erneut öffnen; ggf. WebView2-Laufzeit prüfen.");
                status.Text = "Lade offiziellen TIDAL-Player. Kein automatischer Songstart und keine Spotify-Steuerung.";
                browser.CoreWebView2.Navigate(safe.EmbedUrl);
            }
            catch { browser?.Dispose(); browser = null; throw; }
            finally { if (!layout.IsDisposed) show.Enabled = true; }
        });
        close.Click += (_, _) =>
        {
            browser?.Dispose(); browser = null;
            status.Text = "TIDAL-Testplayer geschlossen. Spotify bleibt unverändert.";
        };
        layout.VisibleChanged += async (_, _) => { if (layout.Visible) await RunAsync(LoadAsync); };
        layout.Disposed += (_, _) => { cancellation.Cancel(); browser?.Dispose(); cancellation.Dispose(); };
        Controls.Add(layout);
    }
}

