using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public sealed class UpdateService
{
    public const string DefaultManifestUrl =
        "https://github.com/Funjii95/RaidClipPlugin/releases/latest/download/update.json";

    private const string AppExecutableName = "RaidClipPlugin.exe";

    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ??
        new Version(1, 2, 1);

    public string CurrentDisplayVersion => FormatVersion(CurrentVersion);

    public async Task<UpdateInfo?> CheckAsync(
        string manifestUrl,
        CancellationToken cancellationToken)
    {
        var manifestUri = RequireHttpsUri(
            manifestUrl,
            "Die GitHub-Adresse für update.json ist ungültig.");

        using var response = await Http.GetAsync(
            manifestUri,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
            stream,
            JsonOptions,
            cancellationToken);

        if (manifest is null ||
            !Version.TryParse(
                manifest.LatestVersion,
                out var availableVersion))
        {
            throw new InvalidOperationException(
                "update.json enthält keine gültige latestVersion.");
        }

        if (availableVersion <= CurrentVersion)
        {
            return null;
        }

        var downloadUri = RequireHttpsUri(
            manifest.DownloadUrl,
            "update.json enthält keine sichere downloadUrl.");
        var extension = Path.GetExtension(downloadUri.AbsolutePath);

        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Auto-Updates müssen als ZIP-Paket bereitgestellt werden. " +
                "Installer-EXE-Dateien dürfen nicht als Update-Payload verwendet werden.");
        }

        var sha256 = (manifest.Sha256 ?? "")
            .Replace(" ", "")
            .Trim();

        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException(
                "update.json enthält keine gültige SHA256-Prüfsumme.");
        }

        return new UpdateInfo(
            availableVersion,
            downloadUri,
            manifest.Changelog?.Trim() ?? "",
            sha256.ToUpperInvariant());
    }

    public async Task<StagedUpdate> DownloadAndStageAsync(
        UpdateInfo update,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var updateRoot = Path.Combine(
            Path.GetTempPath(),
            "RaidClipPlugin",
            "Updates",
            $"{update.Version}-{Guid.NewGuid():N}");
        var payloadDirectory = Path.Combine(updateRoot, "payload");
        Directory.CreateDirectory(payloadDirectory);

        var extension = Path.GetExtension(update.DownloadUrl.AbsolutePath)
            .ToLowerInvariant();
        var downloadPath = Path.Combine(
            updateRoot,
            "download" + extension);

        using var response = await Http.GetAsync(
            update.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using (var input = await response.Content
                         .ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(
                         downloadPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         useAsync: true))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;

            while ((read = await input.ReadAsync(
                       buffer,
                       cancellationToken)) > 0)
            {
                await output.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                received += read;

                if (totalBytes is > 0)
                {
                    progress?.Report((int)Math.Clamp(
                        received * 100 / totalBytes.Value,
                        0,
                        100));
                }
            }
        }

        string actualHash;
        await using (var hashStream = File.OpenRead(downloadPath))
        {
            actualHash = Convert.ToHexString(
                await SHA256.HashDataAsync(
                    hashStream,
                    cancellationToken));
        }

        if (!actualHash.Equals(
                update.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteDirectory(updateRoot);
            throw new InvalidOperationException(
                "Die SHA256-Prüfung ist fehlgeschlagen. " +
                "Das Update wurde verworfen.");
        }

        ExtractZipSafely(downloadPath, payloadDirectory);

        if (!File.Exists(Path.Combine(
                payloadDirectory,
                AppExecutableName)))
        {
            TryDeleteDirectory(updateRoot);
            throw new InvalidOperationException(
                $"Das Update-Paket enthält keine {AppExecutableName} im Hauptordner.");
        }

        progress?.Report(100);
        return new StagedUpdate(
            update.Version,
            payloadDirectory,
            AppExecutableName);
    }

    public static void StartUpdater(StagedUpdate stagedUpdate)
    {
        var currentExecutable = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(currentExecutable) ||
            !File.Exists(currentExecutable) ||
            !Path.GetExtension(currentExecutable)
                .Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(currentExecutable)
                .Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Auto-Update funktioniert nur mit der veröffentlichten RaidClipPlugin.exe.");
        }

        var updaterDirectory = Path.Combine(
            Path.GetTempPath(),
            "RaidClipPlugin",
            "Updater",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updaterDirectory);

        var updaterExecutable = Path.Combine(
            updaterDirectory,
            "RaidClipUpdater.exe");
        File.Copy(currentExecutable, updaterExecutable, overwrite: true);

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterExecutable,
            UseShellExecute = true,
            WorkingDirectory = updaterDirectory
        };
        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(AppContext.BaseDirectory);
        startInfo.ArgumentList.Add(stagedUpdate.PayloadDirectory);
        startInfo.ArgumentList.Add(stagedUpdate.AppExecutableName);

        _ = Process.Start(startInfo) ?? throw new InvalidOperationException(
            "Der separate Updater-Prozess konnte nicht gestartet werden.");
    }

    public static string FormatVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "RaidClipPlugin-AutoUpdater/1.2.6");
        return client;
    }

    private static Uri RequireHttpsUri(string value, string errorMessage)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(errorMessage);
        }

        return uri;
    }

    private static void ExtractZipSafely(
        string zipPath,
        string destinationDirectory)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(
                Path.Combine(destinationRoot, entry.FullName));

            if (!targetPath.StartsWith(
                    destinationRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Das Update-ZIP enthält einen unzulässigen Dateipfad.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class UpdateManifest
    {
        public string LatestVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string? Changelog { get; set; }
        public string? Sha256 { get; set; }
    }
}

public sealed record UpdateInfo(
    Version Version,
    Uri DownloadUrl,
    string Changelog,
    string Sha256)
{
    public string DisplayVersion => UpdateService.FormatVersion(Version);
}

public sealed record StagedUpdate(
    Version Version,
    string PayloadDirectory,
    string AppExecutableName);
