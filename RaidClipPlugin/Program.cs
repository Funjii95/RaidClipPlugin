using RaidClipPlugin.Services;

namespace RaidClipPlugin;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
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
}
