namespace Langent.Core.Update;

/// <summary>Pure helpers for the once-per-day update check (SPEC §10).</summary>
public static class UpdateCheck
{
    /// <summary>
    /// True if <paramref name="latestTag"/> (e.g. "v0.2.0") is a higher version than
    /// <paramref name="current"/>. Unparseable input returns false.
    /// </summary>
    public static bool IsNewer(string? latestTag, Version current)
    {
        if (string.IsNullOrWhiteSpace(latestTag))
        {
            return false;
        }

        var trimmed = latestTag.TrimStart('v', 'V').Trim();
        return Version.TryParse(trimmed, out var latest) && latest > current;
    }

    /// <summary>True if no check has happened yet or the last one was on an earlier local day.</summary>
    public static bool ShouldCheckToday(DateTime? lastCheckLocal, DateTime nowLocal)
        => lastCheckLocal is not { } last || last.Date < nowLocal.Date;
}
