using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public sealed class ChatExtensionManager
{
    public static readonly Uri SevenTvPackageUri = new(
        "https://github.com/SevenTV/Extension/releases/download/v3.1.6/7tv-webextension-mv3.zip");
    public static readonly Uri BetterTtvPackageUri = new(
        "https://github.com/night/betterttv/releases/download/7.7.20/betterttv.tar.gz");
    private const string BetterTtvSha256 =
        "8b3ec7ff27ccb950ef2107e6ea1394af1fc4df3abd3ae23807dd0c8117dc3af6";
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim Sync = new(1, 1);
    private static readonly string ExtensionRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RaidClipPlugin", "ChatExtensions");

    public async Task<string> ApplyAsync(CoreWebView2Profile profile,
        LiveChatConfig config, CancellationToken cancellationToken)
    {
        await Sync.WaitAsync(cancellationToken);
        try
        {
            var sevenTv = await ApplyExtensionAsync(profile, new ExtensionPackage(
                "7TV", "7tv", "v3.1.6", SevenTvPackageUri, PackageKind.Zip, null),
                config.EnableOfficialSevenTvExtension, cancellationToken);
            var betterTtv = await ApplyExtensionAsync(profile, new ExtensionPackage(
                "BetterTTV", "betterttv", "7.7.20", BetterTtvPackageUri,
                PackageKind.TarGzip, BetterTtvSha256),
                config.EnableOfficialBttvExtension, cancellationToken);
            return $"7TV: {sevenTv} · BTTV: {betterTtv} · Animationen: aktiv";
        }
        finally
        {
            Sync.Release();
        }
    }

    private static async Task<string> ApplyExtensionAsync(
        CoreWebView2Profile profile, ExtensionPackage package, bool enabled,
        CancellationToken cancellationToken)
    {
        try
        {
            var packageRoot = GetPackageRoot(package);
            var idFile = Path.Combine(packageRoot, ".extension-id");
            var extensionId = File.Exists(idFile)
                ? (await File.ReadAllTextAsync(idFile, cancellationToken)).Trim()
                : "";
            var installed = await profile.GetBrowserExtensionsAsync();
            var extension = installed.FirstOrDefault(item =>
                extensionId.Length > 0 && item.Id.Equals(extensionId,
                    StringComparison.OrdinalIgnoreCase));

            if (!enabled)
            {
                if (extension is not null && extension.IsEnabled)
                    await extension.EnableAsync(false);
                return "aus";
            }

            if (extension is not null)
            {
                if (!extension.IsEnabled) await extension.EnableAsync(true);
                return "aktiv";
            }

            var manifestFolder = await EnsurePackageAsync(package,
                cancellationToken);
            extension = await profile.AddBrowserExtensionAsync(manifestFolder);
            Directory.CreateDirectory(packageRoot);
            await File.WriteAllTextAsync(idFile, extension.Id, cancellationToken);
            return "aktiv";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            Console.WriteLine($"{package.DisplayName}-Extension konnte nicht aktiviert werden: " +
                exception.Message);
            return "Fehler";
        }
    }

    private static async Task<string> EnsurePackageAsync(ExtensionPackage package,
        CancellationToken cancellationToken)
    {
        var packageRoot = GetPackageRoot(package);
        var existing = FindManifestFolder(packageRoot);
        if (existing is not null) return existing;

        SafeDeletePackageFolder(packageRoot);
        Directory.CreateDirectory(packageRoot);
        var downloadPath = Path.Combine(Path.GetTempPath(),
            $"raidclip-{package.Key}-{Guid.NewGuid():N}" +
            (package.Kind == PackageKind.Zip ? ".zip" : ".tar.gz"));
        try
        {
            await using (var target = new FileStream(downloadPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, true))
            await using (var source = await Client.GetStreamAsync(package.DownloadUri,
                cancellationToken))
                await source.CopyToAsync(target, cancellationToken);

            if (package.Sha256 is not null)
            {
                await using var input = File.OpenRead(downloadPath);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(input,
                    cancellationToken)).ToLowerInvariant();
                if (!actual.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"Prüfsumme des {package.DisplayName}-Pakets stimmt nicht.");
            }

            if (package.Kind == PackageKind.Zip)
                ZipFile.ExtractToDirectory(downloadPath, packageRoot, true);
            else
            {
                await using var input = File.OpenRead(downloadPath);
                await using var gzip = new GZipStream(input, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzip, packageRoot, overwriteFiles: true);
            }

            return FindManifestFolder(packageRoot) ?? throw new InvalidDataException(
                $"Das {package.DisplayName}-Paket enthält keine manifest.json.");
        }
        catch
        {
            SafeDeletePackageFolder(packageRoot);
            throw;
        }
        finally
        {
            try { if (File.Exists(downloadPath)) File.Delete(downloadPath); }
            catch { }
        }
    }

    private static string? FindManifestFolder(string root)
    {
        if (!Directory.Exists(root)) return null;
        var manifest = Directory.EnumerateFiles(root, "manifest.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path.Count(character =>
                character == Path.DirectorySeparatorChar))
            .FirstOrDefault();
        return manifest is null ? null : Path.GetDirectoryName(manifest);
    }

    private static string GetPackageRoot(ExtensionPackage package) =>
        Path.Combine(ExtensionRoot, package.Key, package.Version);

    private static void SafeDeletePackageFolder(string path)
    {
        var root = Path.GetFullPath(ExtensionRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ungültiger Extension-Speicherpfad.");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RaidClipPlugin/1.6.4");
        return client;
    }

    private enum PackageKind { Zip, TarGzip }
    private sealed record ExtensionPackage(string DisplayName, string Key,
        string Version, Uri DownloadUri, PackageKind Kind, string? Sha256);
}
