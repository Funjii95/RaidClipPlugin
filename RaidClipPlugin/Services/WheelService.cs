using System.Security.Cryptography;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public sealed record WheelState(string SpinId, IReadOnlyList<string> Entries, int WinnerIndex,
    DateTimeOffset StartedAt, DateTimeOffset EndsAt, bool HasDesign = false);

public sealed class WheelService
{
    private readonly object _sync = new();
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<int, int> _pick;
    private WheelState _state = new("", Array.Empty<string>(), -1, default, default);
    private WheelDesign? _design;
    internal WheelDesign? Design { get { lock (_sync) return _design; } }
    public WheelService(Func<DateTimeOffset>? now = null, Func<int, int>? pick = null)
    { _now = now ?? (() => DateTimeOffset.UtcNow); _pick = pick ?? (count => RandomNumberGenerator.GetInt32(count)); }
    public WheelState State { get { lock (_sync) return _state; } }
    public bool IsSpinning => State.EndsAt > _now();
    public WheelState Spin(WheelConfig config)
    {
        config.Validate();
        lock (_sync)
        {
            if (_state.EndsAt > _now()) throw new InvalidOperationException("Das Glücksrad dreht sich bereits.");
            var entries = Array.AsReadOnly(config.Entries.Select(x => x.Trim()).ToArray());
            var selected = _pick(entries.Count);
            if (selected < 0 || selected >= entries.Count) throw new InvalidOperationException("Ungültige Glücksrad-Auswahl.");
            var now = _now();
            var design = string.IsNullOrWhiteSpace(config.DesignImagePath) ? null : WheelDesign.Load(config.DesignImagePath);
            _design = design;
            return _state = new(Guid.NewGuid().ToString("N"), entries, selected, now, now.AddSeconds(config.SpinSeconds), design is not null);
        }
    }
    public void Reset()
    {
        lock (_sync)
        {
            if (_state.EndsAt > _now()) throw new InvalidOperationException("Bitte das Ende der Drehung abwarten.");
            _state = new("", Array.Empty<string>(), -1, default, default);
            _design = null;
        }
    }
}

internal sealed record WheelDesign(byte[] Bytes, string ContentType)
{
    public static WheelDesign Load(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > 10000000)
            throw new InvalidOperationException("Glücksrad-Design: Bitte eine vorhandene PNG-/JPG-Datei bis 10 MB auswählen.");
        var bytes = File.ReadAllBytes(file.FullName);
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!png && !jpeg) throw new InvalidOperationException("Als Glücksrad-Design sind nur PNG und JPG erlaubt.");
        using var stream = new MemoryStream(bytes);
        using var bitmap = Image.FromStream(stream);
        if (bitmap.Width > 8192 || bitmap.Height > 8192) throw new InvalidOperationException("Glücksrad-Design: maximal 8192 × 8192 Pixel.");
        return new(bytes, png ? "image/png" : "image/jpeg");
    }
}
