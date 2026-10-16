using System.Drawing.Drawing2D;
using RaidClipPlugin.Services;

namespace RaidClipPlugin.Controls;

internal sealed class WheelPreviewControl : Control
{
    private string[] _entries = Array.Empty<string>();
    private WheelState? _state;
    private Image? _design;
    public WheelPreviewControl()
    { DoubleBuffered = true; ResizeRedraw = true; BackColor = Color.FromArgb(15, 17, 23); ForeColor = Color.White; }

    public void SetDraft(IEnumerable<string> entries)
    { _entries = entries.Select(e => e.Trim()).Where(e => e.Length > 0).Take(100).ToArray(); _state = null; Invalidate(); }
    public void SetState(WheelState state) { _state = state.SpinId.Length > 0 ? state : null; Invalidate(); }
    public void SetDesign(string path)
    {
        Image? next = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            using var stream = new MemoryStream(WheelDesign.Load(path).Bytes);
            using var original = Image.FromStream(stream);
            next = new Bitmap(original);
        }
        var previous = _design; _design = next; previous?.Dispose(); Invalidate();
    }
    protected override void Dispose(bool disposing) { if (disposing) _design?.Dispose(); base.Dispose(disposing); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var entries = _state?.Entries ?? _entries;
        var radius = Math.Min(Width - 36, Height - 60) / 2f;
        if (radius < 12 || entries.Count == 0)
        { TextRenderer.DrawText(e.Graphics, "Felder eingeben für die Vorschau", Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var centerX = Width / 2f; var centerY = (Height + 12) / 2f;
        var step = 360f / entries.Count;
        var angle = 0f;
        if (_state is not null)
        {
            var p = Math.Clamp((DateTimeOffset.UtcNow - _state.StartedAt).TotalMilliseconds / Math.Max(1, (_state.EndsAt - _state.StartedAt).TotalMilliseconds), 0, 1);
            angle = (float)((2160 - (_state.WinnerIndex + 0.5) * step) * (1 - Math.Pow(1 - p, 4)));
        }
        var saved = g.Save(); g.TranslateTransform(centerX, centerY); g.RotateTransform(angle);
        var circle = new RectangleF(-radius, -radius, radius * 2, radius * 2);
        if (_design is not null)
        {
            var clipState = g.Save();
            using var clip = new GraphicsPath(); clip.AddEllipse(circle); g.SetClip(clip);
            var scale = Math.Max(radius * 2 / _design.Width, radius * 2 / _design.Height);
            g.DrawImage(_design, -_design.Width * scale / 2, -_design.Height * scale / 2, _design.Width * scale, _design.Height * scale);
            g.Restore(clipState);
        }
        for (var i = 0; i < entries.Count; i++)
        {
            using var brush = new SolidBrush(Color.FromArgb(_design is null ? 255 : 90, SegmentColor(i)));
            g.FillPie(brush, circle.X, circle.Y, circle.Width, circle.Height, -90 + i * step, step);
            using var edge = new Pen(Color.FromArgb(130, Color.White), 1);
            g.DrawPie(edge, circle.X, circle.Y, circle.Width, circle.Height, -90 + i * step, step);
            if (entries.Count > 40) continue;
            var segment = g.Save(); g.RotateTransform(-90 + (i + .5f) * step);
            var text = entries[i].Length > 23 ? entries[i][..20] + "…" : entries[i];
            using var font = new Font("Segoe UI", Math.Clamp(radius / 22, 8, 13), FontStyle.Bold);
            var bounds = new RectangleF(radius * .2f, -13, radius * .73f, 26);
            using var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(text, font, Brushes.Black, new RectangleF(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height), format);
            g.DrawString(text, font, Brushes.White, bounds, format); g.Restore(segment);
        }
        using var rim = new Pen(Color.FromArgb(210, 215, 230), 3); g.DrawEllipse(rim, circle);
        g.FillEllipse(Brushes.White, -8, -8, 16, 16); g.Restore(saved);
        g.FillPolygon(Brushes.White, new[] { new PointF(centerX - 12, centerY - radius - 12), new PointF(centerX + 12, centerY - radius - 12), new PointF(centerX, centerY - radius + 12) });
    }
    private static Color SegmentColor(int index)
    {
        var h = index * 137.508 % 360 / 60;
        const double c = .507, m = .1365;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch { < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x), < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
}
