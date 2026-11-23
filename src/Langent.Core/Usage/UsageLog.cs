using System.Globalization;
using Langent.Core.Infrastructure;

namespace Langent.Core.Usage;

/// <summary>One recorded translation (SPEC §7). Never contains any text content.</summary>
public sealed record UsageEntry(
    DateTime TimestampUtc,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    int CharsIn,
    string SourceLang,
    string TargetLang,
    bool Cancelled);

/// <summary>Aggregated usage over a period.</summary>
public sealed record UsageTotals(int Count, int InputTokens, int OutputTokens, decimal CostUsd)
{
    public static readonly UsageTotals Empty = new(0, 0, 0, 0m);
}

/// <summary>
/// Reads and appends <c>usage.csv</c> (SPEC §7): UTF-8, semicolon-separated, header
/// row, dot decimal separator. Loaded once, then kept in memory and appended to.
/// Malformed lines are skipped; a missing file means empty totals. Never logs text.
/// Totals are grouped by local time.
/// </summary>
public sealed class UsageLog
{
    public const string FileName = "usage.csv";

    public const string Header =
        "timestamp_utc;model;input_tokens;output_tokens;cost_usd;chars_in;source_lang;target_lang;cancelled";

    private const string NewLine = "\n";

    private readonly IFileSystem _fileSystem;
    private readonly string _path;
    private readonly List<UsageEntry> _entries = [];

    public UsageLog(IFileSystem fileSystem, string path)
    {
        _fileSystem = fileSystem;
        _path = path;
        Load();
    }

    public IReadOnlyList<UsageEntry> Entries => _entries;

    /// <summary>Raised after an entry is appended (lets the tray refresh without polling).</summary>
    public event Action? Changed;

    public void Append(UsageEntry entry)
    {
        if (!_fileSystem.FileExists(_path))
        {
            _fileSystem.WriteAllText(_path, Header + NewLine);
        }

        _fileSystem.AppendAllText(_path, Format(entry) + NewLine);
        _entries.Add(entry);
        Changed?.Invoke();
    }

    /// <summary>Totals for the local calendar day of <paramref name="localDay"/>.</summary>
    public UsageTotals TotalsForDay(DateTime localDay)
        => Sum(e => e.TimestampUtc.ToLocalTime().Date == localDay.Date);

    /// <summary>Totals for the local calendar month of <paramref name="localMonth"/>.</summary>
    public UsageTotals TotalsForMonth(DateTime localMonth)
        => Sum(e =>
        {
            var local = e.TimestampUtc.ToLocalTime();
            return local.Year == localMonth.Year && local.Month == localMonth.Month;
        });

    private UsageTotals Sum(Func<UsageEntry, bool> predicate)
    {
        var count = 0;
        var input = 0;
        var output = 0;
        var cost = 0m;

        foreach (var e in _entries)
        {
            if (!predicate(e))
            {
                continue;
            }

            count++;
            input += e.InputTokens;
            output += e.OutputTokens;
            cost += e.CostUsd;
        }

        return new UsageTotals(count, input, output, cost);
    }

    private void Load()
    {
        if (!_fileSystem.FileExists(_path))
        {
            return;
        }

        var content = _fileSystem.ReadAllText(_path);
        var lines = content.Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line == Header)
            {
                continue;
            }

            if (TryParse(line, out var entry))
            {
                _entries.Add(entry);
            }
        }
    }

    private static bool TryParse(string line, out UsageEntry entry)
    {
        entry = null!;
        var parts = line.Split(';');
        if (parts.Length != 9)
        {
            return false;
        }

        if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var input)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var output)
            || !decimal.TryParse(parts[4], NumberStyles.Number, CultureInfo.InvariantCulture, out var cost)
            || !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chars)
            || !TryParseBool(parts[8], out var cancelled))
        {
            return false;
        }

        entry = new UsageEntry(
            ts.ToUniversalTime(),
            parts[1],
            input,
            output,
            cost,
            chars,
            parts[6],
            parts[7],
            cancelled);
        return true;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true" or "1":
                result = true;
                return true;
            case "false" or "0":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static string Format(UsageEntry e)
    {
        var timestamp = e.TimestampUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var cost = e.CostUsd.ToString("0.######", CultureInfo.InvariantCulture);
        var cancelled = e.Cancelled ? "true" : "false";

        return string.Join(';',
            timestamp,
            e.Model,
            e.InputTokens.ToString(CultureInfo.InvariantCulture),
            e.OutputTokens.ToString(CultureInfo.InvariantCulture),
            cost,
            e.CharsIn.ToString(CultureInfo.InvariantCulture),
            e.SourceLang,
            e.TargetLang,
            cancelled);
    }
}
