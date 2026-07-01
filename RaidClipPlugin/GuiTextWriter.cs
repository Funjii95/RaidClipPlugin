using System.Text;

namespace RaidClipPlugin;

internal sealed class GuiTextWriter : TextWriter
{
    private readonly Action<string> _writeLine;
    private readonly StringBuilder _buffer = new();
    private readonly object _sync = new();

    public GuiTextWriter(Action<string> writeLine)
    {
        _writeLine = writeLine;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        string? completedLine = null;

        lock (_sync)
        {
            if (value == '\n')
            {
                completedLine = _buffer.ToString();
                _buffer.Clear();
            }
            else if (value != '\r')
            {
                _buffer.Append(value);
            }
        }

        if (completedLine is not null)
        {
            _writeLine(completedLine);
        }
    }

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var character in value)
        {
            Write(character);
        }
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        Write('\n');
    }
}
