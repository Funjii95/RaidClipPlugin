namespace RaidClipPlugin.Services;

internal static class ApplicationFileRepair
{
    internal static bool IsApplicationFile(string relative)
    {
        var path = relative.Replace('\\', '/');
        if (path.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':'))) return false;
        if (path.Equals("Config/config.template.json", StringComparison.OrdinalIgnoreCase)) return true;
        if (new[] { "Microsoft.Web.WebView2.Core.xml", "Microsoft.Web.WebView2.WinForms.xml", "Microsoft.Web.WebView2.Wpf.xml" }
            .Contains(path, StringComparer.OrdinalIgnoreCase)) return true;
        if (path.Equals("RaidClipPlugin.exe", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("RaidClipPlugin.deps.json", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("RaidClipPlugin.runtimeconfig.json", StringComparison.OrdinalIgnoreCase)) return true;
        return Path.GetExtension(path).ToLowerInvariant() is ".dll" or ".pdb";
    }

    internal static void Apply(string payload, string target, Action<string, string> copy)
    {
        payload = Path.GetFullPath(payload);
        target = Path.GetFullPath(target);
        var dataRoot = Path.GetFullPath(TestDataPaths.Root);
        if (Within(dataRoot, target) || Within(target, dataRoot) || Within(payload, target) || Within(target, payload))
            throw new InvalidOperationException("Programm-, Daten- und Reparaturordner müssen getrennt sein.");
        RejectLinks(payload);
        RejectLinks(target);
        var files = Directory.GetFiles(payload, "*", SearchOption.AllDirectories)
            .Select(source => (Source: source, Relative: Path.GetRelativePath(payload, source))).ToArray();
        if (!files.Any(file => file.Relative.Equals("RaidClipPlugin.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Reparaturpaket enthält keine RaidClipPlugin.exe.");
        foreach (var file in files)
        {
            if (!IsApplicationFile(file.Relative))
                throw new InvalidDataException("Paket enthält eine nicht freigegebene Programmdatei: " + file.Relative);
            RejectLinks(file.Source);
            RejectLinks(Path.Combine(target, file.Relative));
        }

        var backup = Path.Combine(Path.GetDirectoryName(payload)!, "application-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        // Finish the complete backup before replacing the first file.
        foreach (var file in files)
        {
            var existing = Path.Combine(target, file.Relative);
            if (!File.Exists(existing)) continue;
            var saved = Path.Combine(backup, file.Relative);
            Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
            File.Copy(existing, saved);
        }
        var attempted = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var destination = Path.Combine(target, file.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                attempted.Add(file.Relative);
                copy(file.Source, destination);
            }
        }
        catch (Exception failure)
        {
            var errors = new List<Exception>();
            foreach (var relative in attempted.AsEnumerable().Reverse())
            {
                try
                {
                    var saved = Path.Combine(backup, relative);
                    var destination = Path.Combine(target, relative);
                    if (File.Exists(saved)) File.Copy(saved, destination, overwrite: true);
                    else File.Delete(destination);
                }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count > 0)
                throw new AggregateException("Wiederherstellung unvollständig. Programmsicherung: " + backup, [failure, .. errors]);
            throw new IOException("Programmdateien konnten nicht ersetzt werden. Vorherige Dateien wurden wiederhergestellt.", failure);
        }
    }

    private static bool Within(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Verknüpfte Reparaturpfade werden nicht unterstützt.");
    }
}
