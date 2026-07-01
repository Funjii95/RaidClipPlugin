using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class ClipHistoryService
{
    private const int MaximumEntries = 100;
    private readonly object _sync = new();
    private readonly string _path;
    private readonly List<ClipHistoryEntry> _entries = new();

    public event Action<ClipHistoryEntry>? EntryAdded;

    public ClipHistoryService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RaidClipPlugin");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "clip-history.json");
        Load();
    }

    public IReadOnlyList<ClipHistoryEntry> GetSnapshot()
    {
        lock (_sync)
        {
            return _entries
                .Select(Clone)
                .ToList();
        }
    }

    public void Add(
        string clipId,
        string title,
        string channel,
        string status)
    {
        var entry = new ClipHistoryEntry
        {
            ClipId = clipId,
            Title = title,
            Channel = channel,
            PlayedAt = DateTimeOffset.Now,
            Status = status
        };

        lock (_sync)
        {
            _entries.Insert(0, entry);

            if (_entries.Count > MaximumEntries)
            {
                _entries.RemoveRange(
                    MaximumEntries,
                    _entries.Count - MaximumEntries);
            }

            Save();
        }

        EntryAdded?.Invoke(Clone(entry));
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<ClipHistoryEntry>>(
                File.ReadAllText(_path));

            if (entries is null)
            {
                return;
            }

            _entries.AddRange(entries.Take(MaximumEntries));
        }
        catch
        {
            // Eine beschädigte Historie wird beim nächsten Eintrag ersetzt.
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(
                _path,
                JsonSerializer.Serialize(
                    _entries,
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Historienfehler dürfen die Wiedergabe nicht stoppen.
        }
    }

    private static ClipHistoryEntry Clone(ClipHistoryEntry entry)
    {
        return new ClipHistoryEntry
        {
            ClipId = entry.ClipId,
            Title = entry.Title,
            Channel = entry.Channel,
            PlayedAt = entry.PlayedAt,
            Status = entry.Status
        };
    }
}
