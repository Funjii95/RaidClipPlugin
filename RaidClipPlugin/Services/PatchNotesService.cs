using System.Text.RegularExpressions;

namespace RaidClipPlugin.Services;

public static class PatchNotesService
{
    private static readonly Regex Header = new(@"^\s*#{0,6}\s*(?<version>\d+\.\d+\.\d+)(?:\s|$)", RegexOptions.Multiline);
    public static string BuildHistory(string legacy)
    {
        var latestLegacy = Header.Matches(legacy).Select(m => Version.Parse(m.Groups["version"].Value))
            .DefaultIfEmpty(new Version(0, 0, 0)).Max()!;
        var assembly = typeof(PatchNotesService).Assembly;
        const string prefix = "RaidClipPlugin.ReleaseNotes.";
        var notes = new List<(Version Version, string Text)>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix) && n.EndsWith(".md")))
        {
            var versionText = name[prefix.Length..^3];
            if (!Version.TryParse(versionText, out var version) || version <= latestLegacy) continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var body = reader.ReadToEnd().Trim();
            if (body.Length > 0) notes.Add((version, body));
        }
        // Local, embedded notes: no network request on opening Settings, no duplicate legacy entries.
        return string.Join(Environment.NewLine + Environment.NewLine,
            notes.OrderByDescending(n => n.Version).Select(n => n.Text).Append(legacy.Trim()));
    }
}
