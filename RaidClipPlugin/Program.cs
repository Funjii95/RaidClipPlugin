using RaidClipPlugin.Services;

namespace RaidClipPlugin;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
#if ISOLATED_ACTIONS_TEST
        // The downloadable test EXE can never select the normal plugin data directory.
        Environment.SetEnvironmentVariable("FUNJII_RPG_TEST_DATA_ROOT",Path.Combine(AppContext.BaseDirectory,"TestData"));
#endif
        try
        {
            var smoke=args.Length==1 && args[0]=="--ui-smoke";
            var administrationTest=args.Length==1 && args[0]=="--administration-test";
            if (!TestDataPaths.IsTestBuild && UpdateApplier.TryRun(args)) return;
            if (TestDataPaths.IsTestBuild && args.Length != 0 && !smoke && !administrationTest)
                throw new InvalidOperationException("Updater arguments are disabled for isolated execution.");
            if ((smoke || administrationTest) && !TestDataPaths.IsTestBuild)
                throw new InvalidOperationException("UI smoke tests require an isolated data root.");
            Directory.CreateDirectory(TestDataPaths.Root);
            using var instance=new FileStream(Path.Combine(TestDataPaths.Root,"instance.lock"),FileMode.OpenOrCreate,FileAccess.Write,FileShare.None);

            ApplicationConfiguration.Initialize();
            if(administrationTest){using var form=new MainForm();form.EnableAdministrationTestOnly();Application.Run(form);return;}
            if(smoke)
            {
                using var form=new MainForm {Opacity=0,ShowInTaskbar=false};
                form.Shown+=async(_,_)=>{await Task.Delay(200);form.CloseSmoke();};
                Application.Run(form);
                Console.WriteLine("Integrated MainForm startup passed; no Twitch connection or gameplay started.");
                return;
            }

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
            Environment.ExitCode=1;
            if(args.Length==1 && args[0]=="--ui-smoke")return;
            MessageBox.Show(
                "RaidClipPlugin konnte nicht gestartet werden.\n\n" +
                "Der Fehler wurde protokolliert unter:\n" + logPath,
                "RaidClipPlugin – Startfehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
