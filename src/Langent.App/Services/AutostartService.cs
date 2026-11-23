using Microsoft.Win32;

namespace Langent.App.Services;

/// <summary>
/// Manages the HKCU Run entry for autostart (SPEC §9). The value <c>Langent</c>
/// holds the quoted exe path; the path is corrected at startup if the exe moved.
/// </summary>
public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Langent";

    public static void Apply(bool enabled, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, Quote(exePath));
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // Autostart is best-effort.
        }
    }

    /// <summary>At startup: if autostart is on but the registered path is stale, fix it.</summary>
    public static void EnsureCurrentPath(bool enabled, string exePath)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is string current && current != Quote(exePath))
            {
                key.SetValue(ValueName, Quote(exePath));
            }
        }
        catch (Exception)
        {
            // Best-effort.
        }
    }

    private static string Quote(string path) => $"\"{path}\"";
}
