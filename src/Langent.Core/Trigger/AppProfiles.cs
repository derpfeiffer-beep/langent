namespace Langent.Core.Trigger;

/// <summary>
/// Decides whether the foreground application is excluded from the trigger
/// (SPEC §4.1). Matching is case-insensitive and tolerant of a missing
/// <c>.exe</c> suffix, since a process name from the OS usually has no extension.
/// </summary>
public sealed class AppProfiles
{
    /// <summary>Default exclusions from SPEC §4.1.</summary>
    public static readonly IReadOnlyList<string> DefaultExcludedProcesses =
    [
        "WindowsTerminal.exe",
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "conhost.exe",
        "mstsc.exe",
    ];

    private readonly HashSet<string> _excluded;

    public AppProfiles(IEnumerable<string>? excludedProcesses = null)
        => _excluded = new HashSet<string>(
            excludedProcesses ?? DefaultExcludedProcesses,
            StringComparer.OrdinalIgnoreCase);

    public bool IsExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return _excluded.Contains(processName)
            || _excluded.Contains(processName + ".exe");
    }
}
