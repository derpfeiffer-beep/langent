using System.Text;

namespace Langent.Core.Translation;

/// <summary>One parsed Server-Sent Event (SPEC §5.4).</summary>
public sealed record SseEvent(string? Event, string Data);

/// <summary>
/// Incremental SSE parser (SPEC §5.4). Feed raw chunks as they arrive; complete
/// events are returned as they are dispatched by a blank line. A partial line is
/// buffered across chunks. <c>ping</c> events are dropped. Multi-line <c>data:</c>
/// fields are joined with <c>\n</c>.
/// </summary>
public sealed class SseReader
{
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _dataLines = [];
    private string? _eventType;

    /// <summary>Feeds a chunk and returns any events that completed within it.</summary>
    public IReadOnlyList<SseEvent> Feed(string chunk)
    {
        _buffer.Append(chunk);
        var results = new List<SseEvent>();

        int newlineIndex;
        while ((newlineIndex = IndexOf('\n')) >= 0)
        {
            var line = _buffer.ToString(0, newlineIndex);
            _buffer.Remove(0, newlineIndex + 1);

            if (line.EndsWith('\r'))
            {
                line = line[..^1];
            }

            ProcessLine(line, results);
        }

        return results;
    }

    /// <summary>Dispatches any event still buffered at end of stream (no final blank line).</summary>
    public IReadOnlyList<SseEvent> Flush()
    {
        var results = new List<SseEvent>();

        if (_buffer.Length > 0)
        {
            var line = _buffer.ToString();
            _buffer.Clear();
            if (line.EndsWith('\r'))
            {
                line = line[..^1];
            }

            ProcessLine(line, results);
        }

        Dispatch(results);
        return results;
    }

    private void ProcessLine(string line, List<SseEvent> results)
    {
        if (line.Length == 0)
        {
            Dispatch(results);
            return;
        }

        if (line[0] == ':')
        {
            return; // comment
        }

        var colon = line.IndexOf(':');
        var field = colon < 0 ? line : line[..colon];
        var value = colon < 0 ? string.Empty : line[(colon + 1)..];
        if (value.StartsWith(' '))
        {
            value = value[1..];
        }

        switch (field)
        {
            case "event":
                _eventType = value;
                break;
            case "data":
                _dataLines.Add(value);
                break;
            // "id", "retry" and unknown fields are ignored.
        }
    }

    private void Dispatch(List<SseEvent> results)
    {
        if (_dataLines.Count == 0 && _eventType is null)
        {
            return;
        }

        if (_eventType != "ping")
        {
            results.Add(new SseEvent(_eventType, string.Join('\n', _dataLines)));
        }

        _eventType = null;
        _dataLines.Clear();
    }

    private int IndexOf(char c)
    {
        for (var i = 0; i < _buffer.Length; i++)
        {
            if (_buffer[i] == c)
            {
                return i;
            }
        }

        return -1;
    }
}
