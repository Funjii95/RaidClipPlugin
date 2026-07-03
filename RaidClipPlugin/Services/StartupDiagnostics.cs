using System.Text;

namespace RaidClipPlugin.Services;

public static class StartupDiagnostics
{
    public static string Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin",
            "logs");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"startup-{DateTime.Now:yyyy-MM-dd}.log");
        var entry = new StringBuilder()
            .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Start fehlgeschlagen")
            .AppendLine(exception.ToString())
            .AppendLine(new string('-', 80))
            .ToString();
        File.AppendAllText(path, entry, Encoding.UTF8);
        return path;
    }
}
