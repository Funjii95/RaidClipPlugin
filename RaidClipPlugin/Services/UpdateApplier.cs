using System.Diagnostics;

namespace RaidClipPlugin.Services;

internal static class UpdateApplier
{
    private const string UpdateArgument = "--apply-update";

    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 ||
            !args[0].Equals(
                UpdateArgument,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Run(args);
        return true;
    }

    private static void Run(string[] args)
    {
        string? targetExecutable = null;

        try
        {
            if (args.Length != 5 ||
                !int.TryParse(args[1], out var processId))
            {
                throw new InvalidOperationException(
                    "Der Updater wurde mit ungültigen Parametern gestartet.");
            }

            var targetDirectory = Path.GetFullPath(args[2]);
            var payloadDirectory = Path.GetFullPath(args[3]);
            var executableName = Path.GetFileName(args[4]);
            targetExecutable = Path.Combine(
                targetDirectory,
                executableName);

            WriteLog(
                $"Update gestartet. Warte auf Prozess {processId}.");
            WaitForMainProcess(processId);

            WriteLog("Hauptanwendung beendet. Ersetze Dateien.");
            CopyPayload(payloadDirectory, targetDirectory);

            WriteLog("Dateien ersetzt. Starte RaidClipPlugin neu.");
            StartApplication(targetExecutable);
            TryDeleteStagingDirectory(payloadDirectory);
            WriteLog("Update erfolgreich abgeschlossen.");
        }
        catch (Exception exception)
        {
            WriteLog("Update fehlgeschlagen: " + exception);

            if (!string.IsNullOrWhiteSpace(targetExecutable) &&
                File.Exists(targetExecutable))
            {
                try
                {
                    StartApplication(targetExecutable);
                    WriteLog(
                        "Die bisherige Anwendung wurde nach dem Fehler neu gestartet.");
                }
                catch (Exception restartException)
                {
                    WriteLog(
                        "Neustart nach Update-Fehler fehlgeschlagen: " +
                        restartException);
                }
            }
        }
    }

    private static void WaitForMainProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(60000))
            {
                throw new TimeoutException(
                    "Die laufende RaidClipPlugin.exe wurde nicht rechtzeitig beendet.");
            }
        }
        catch (ArgumentException)
        {
            // Der Hauptprozess ist bereits beendet.
        }
    }

    private static void CopyPayload(
        string payloadDirectory,
        string targetDirectory)
    {
        if (!Directory.Exists(payloadDirectory))
        {
            throw new DirectoryNotFoundException(
                "Der vorbereitete Update-Ordner wurde nicht gefunden.");
        }

        Directory.CreateDirectory(targetDirectory);
        var sourceRoot = payloadDirectory
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var targetRoot = Path.GetFullPath(targetDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        foreach (var sourcePath in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(
                sourceRoot,
                sourcePath);
            var targetPath = Path.GetFullPath(
                Path.Combine(targetRoot, relativePath));

            if (!targetPath.StartsWith(
                    targetRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Das Update enthält einen unzulässigen Zielpfad.");
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(targetPath)!);
            CopyWithRetry(sourcePath, targetPath);
        }
    }

    private static void CopyWithRetry(
        string sourcePath,
        string targetPath)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (IOException exception)
            {
                lastException = exception;
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException exception)
            {
                lastException = exception;
                Thread.Sleep(500);
            }
        }

        throw new IOException(
            $"Die Datei '{Path.GetFileName(targetPath)}' konnte nicht ersetzt werden.",
            lastException);
    }

    private static void StartApplication(string executablePath)
    {
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException(
            "RaidClipPlugin konnte nach dem Update nicht neu gestartet werden.");
    }

    private static void TryDeleteStagingDirectory(
        string payloadDirectory)
    {
        try
        {
            var updateDirectory = Directory.GetParent(payloadDirectory)?.FullName;
            if (!string.IsNullOrWhiteSpace(updateDirectory) &&
                Directory.Exists(updateDirectory))
            {
                Directory.Delete(updateDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "RaidClipPlugin",
                "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(
                logDirectory,
                $"updater-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(
                logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}" +
                Environment.NewLine);
        }
        catch
        {
        }
    }
}
