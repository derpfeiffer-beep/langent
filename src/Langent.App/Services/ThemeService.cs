using Microsoft.Win32;

namespace Langent.App.Services;

/// <summary>Reads the Windows light/dark preference (SPEC §8.1, <c>AppsUseLightTheme</c>).</summary>
public static class ThemeService
{
    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value ? value != 0 : true;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
