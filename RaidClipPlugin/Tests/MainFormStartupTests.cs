using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class MainFormStartupTests
{
    [Fact]
    public void MainWindowCanBeConstructedOnStaThread()
    {
        Exception? startupFailure = null;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm();
                Assert.NotNull(form);
            }
            catch (Exception exception)
            {
                startupFailure = exception;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(20)),
            "Das Hauptfenster konnte nicht innerhalb von 20 Sekunden aufgebaut werden.");
        Assert.Null(startupFailure);
    }
}
