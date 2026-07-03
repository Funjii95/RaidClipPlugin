using RaidClipPlugin.Services;

namespace RaidClipPlugin;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (UpdateApplier.TryRun(args))
            {
                return;
            }

            ApplicationConfiguration.Initialize();

            var credentialStore = new TwitchCredentialStore();
            if (!credentialStore.HasCredentials)
            {
                using var wizard = new SetupWizardForm(credentialStore);
                if (wizard.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
            }

            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            var logPath = StartupDiagnostics.Write(exception);
            MessageBox.Show(
                "RaidClipPlugin konnte nicht gestartet werden.\n\n" +
                "Der Fehler wurde protokolliert unter:\n" + logPath,
                "RaidClipPlugin – Startfehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
