using Langent.Core.Tests.TestDoubles;
using Langent.Core.Usage;
using Xunit;

namespace Langent.Core.Tests.Usage;

public sealed class UsageLogTests
{
    private const string Path = "usage.csv";

    private static UsageEntry Entry(DateTime utc, decimal cost = 0.0001m, int input = 100, int output = 20)
        => new(utc, "claude-haiku-5-5", input, output, cost, 50, "de", "en", Cancelled: false);

    [Fact]
    public void Append_then_reload_round_trips()
    {
        var fs = new InMemoryFileSystem();
        var log = new UsageLog(fs, Path);

        var utc = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);
        log.Append(Entry(utc, cost: 0.000123m, input: 123, output: 45));

        var reloaded = new UsageLog(fs, Path);

        Assert.Single(reloaded.Entries);
        var e = reloaded.Entries[0];
        Assert.Equal("claude-haiku-5-5", e.Model);
        Assert.Equal(123, e.InputTokens);
        Assert.Equal(45, e.OutputTokens);
        Assert.Equal(0.000123m, e.CostUsd);
        Assert.StartsWith(UsageLog.Header, fs.Files[Path]);
    }

    [Fact]
    public void Totals_split_today_and_month_across_a_month_boundary()
    {
        var fs = new InMemoryFileSystem();
        var log = new UsageLog(fs, Path);

        var march = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var april = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);
        log.Append(Entry(march, cost: 1.0m));
        log.Append(Entry(april, cost: 2.0m));

        var aprilLocal = april.ToLocalTime();

        var month = log.TotalsForMonth(aprilLocal);
        Assert.Equal(1, month.Count);
        Assert.Equal(2.0m, month.CostUsd);

        var day = log.TotalsForDay(aprilLocal);
        Assert.Equal(1, day.Count);
        Assert.Equal(2.0m, day.CostUsd);
    }

    [Fact]
    public void Missing_file_means_empty_totals()
    {
        var fs = new InMemoryFileSystem();
        var log = new UsageLog(fs, Path);

        Assert.Empty(log.Entries);
        Assert.Equal(UsageTotals.Empty, log.TotalsForMonth(DateTime.Now));
    }

    [Fact]
    public void Append_raises_changed()
    {
        var fs = new InMemoryFileSystem();
        var log = new UsageLog(fs, Path);
        var raised = 0;
        log.Changed += () => raised++;

        log.Append(Entry(new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Malformed_lines_are_skipped()
    {
        var fs = new InMemoryFileSystem();
        fs.Files[Path] =
            UsageLog.Header + "\n" +
            "2026-04-15T12:00:00Z;claude-haiku-5-5;100;20;0.00006;50;de;en;false\n" +
            "this;is;garbage\n" +
            "2026-04-16T12:00:00Z;claude-sonnet-5-5;200;40;0.0012;80;en;de;true\n";

        var log = new UsageLog(fs, Path);

        Assert.Equal(2, log.Entries.Count);
        Assert.True(log.Entries[1].Cancelled);
    }
}
