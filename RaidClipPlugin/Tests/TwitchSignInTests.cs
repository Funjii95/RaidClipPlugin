using System.Net;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Services;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class TwitchSignInTests
{
    [Fact]
    public async Task Sign_in_dialog_displays_code_and_completes_without_starting_plugin_services()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var visible = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var form = new TwitchSignInForm(async (progress, token) =>
                {
                    progress.Report(new("https://www.twitch.tv/activate", "DENNIS-CODE"));
                    await visible.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                    return new TwitchSession("synthetic", "synthetic", "1002", "dennis");
                }) { Opacity = 0, ShowInTaskbar = false };
                Exception? failure = null;
                using var timer = new System.Windows.Forms.Timer { Interval = 40 };
                var codeWasVisible = false;
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        var code = Assert.IsType<TextBox>(Assert.Single(form.Controls.Find("TwitchDeviceCode", true)));
                        codeWasVisible |= code.Visible && code.Text == "DENNIS-CODE";
                        if (codeWasVisible) visible.TrySetResult();
                    }
                    catch (Exception error) { failure = error; form.Close(); }
                };
                timer.Start();
                Assert.Equal(DialogResult.OK, form.ShowDialog());
                timer.Stop();
                if (failure is not null) throw failure;
                Assert.True(codeWasVisible);
                Assert.Equal("dennis", form.Session!.Login);
                completion.SetResult();
            }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Standalone_login_reports_code_even_when_browser_cannot_start_and_reuses_validated_session()
    {
        var root = Path.Combine(Path.GetTempPath(), "twitch-signin-" + Guid.NewGuid().ToString("N"));
        var handler = new TwitchHandler();
        using var http = new HttpClient(handler);
        TwitchAuthorizationRequest? challenge = null;
        var browserAttempts = 0;
        var service = new AuthenticationService(new AppConfig { Twitch = new() { ClientId = "test-client", ClientSecret = "test-secret" } },
            http, _ => { browserAttempts++; Assert.NotNull(challenge); throw new InvalidOperationException("No default browser"); }, root);
        try
        {
            var session = await service.GetSessionAsync(default, new ImmediateProgress(request => challenge = request));
            Assert.Equal("DENNIS-CODE", challenge!.UserCode);
            Assert.Equal("https://www.twitch.tv/activate", challenge.VerificationUri);
            Assert.Equal("dennis", session.Login);
            Assert.Equal(1, browserAttempts);
            Assert.Equal(new[] { "/oauth2/device", "/oauth2/token", "/oauth2/validate" }, handler.Paths);
            handler.Paths.Clear();
            var restored = await service.GetSessionAsync(default);
            Assert.Equal("dennis", restored.Login);
            Assert.Equal(new[] { "/oauth2/validate" }, handler.Paths);
            Assert.Equal(1, browserAttempts);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Canceling_login_stops_polling_without_creating_a_saved_token()
    {
        var root = Path.Combine(Path.GetTempPath(), "twitch-cancel-" + Guid.NewGuid().ToString("N"));
        using var cancel = new CancellationTokenSource();
        var handler = new TwitchHandler();
        using var http = new HttpClient(handler);
        var service = new AuthenticationService(new AppConfig { Twitch = new() { ClientId = "test-client" } }, http, _ => cancel.Cancel(), root);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSessionAsync(cancel.Token));
            Assert.Equal(new[] { "/oauth2/device" }, handler.Paths);
            Assert.False(File.Exists(Path.Combine(root, "twitch-token.dat")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Rejected_client_is_reported_instead_of_silently_remaining_disconnected()
    {
        var root = Path.Combine(Path.GetTempPath(), "twitch-rejected-" + Guid.NewGuid().ToString("N"));
        var handler = new TwitchHandler { RejectClient = true };
        using var http = new HttpClient(handler);
        var service = new AuthenticationService(new AppConfig(), http, _ => Assert.Fail("Browser must not open"), root);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSessionAsync(default));
            Assert.Contains("400", error.Message);
            Assert.Contains("Twitch-Anmeldung", error.Message);
            Assert.False(File.Exists(Path.Combine(root, "twitch-token.dat")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class ImmediateProgress(Action<TwitchAuthorizationRequest> report) : IProgress<TwitchAuthorizationRequest>
    { public void Report(TwitchAuthorizationRequest value) => report(value); }

    private sealed class TwitchHandler : HttpMessageHandler
    {
        public bool RejectClient { get; init; }
        public List<string> Paths { get; } = [];
        private string[] _scopes = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (path == "/oauth2/device")
            {
                if (RejectClient) return new(HttpStatusCode.BadRequest) { Content = new StringContent("invalid client") };
                var form = await request.Content!.ReadAsStringAsync(token);
                var scopeValue = form.Split('&').Single(field => field.StartsWith("scopes="))[7..];
                _scopes = Uri.UnescapeDataString(scopeValue.Replace('+', ' ')).Split(' ');
                return Json(new { device_code = "synthetic-device", user_code = "DENNIS-CODE", verification_uri = "https://www.twitch.tv/activate", expires_in = 60, interval = 1 });
            }
            if (path == "/oauth2/token") return Json(new { access_token = "synthetic-access", refresh_token = "synthetic-refresh" });
            if (path == "/oauth2/validate") return Json(new { client_id = "test-client", login = "dennis", user_id = "1002", scopes = _scopes, expires_in = 3600 });
            throw new InvalidOperationException("Unexpected non-Twitch request: " + path);
        }
        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };
    }
}
