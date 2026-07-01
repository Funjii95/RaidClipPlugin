namespace RaidClipPlugin.Services;

public sealed class FileLogService
{
    private readonly object _sync = new();
    private readonly string _logDirectory;

    public FileLogService()
    {
        _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public void WriteLine(string message)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(
                    _logDirectory,
                    $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(
                    path,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging darf das Plugin nicht stoppen.
        }
    }
}
