using System.Windows;
using System.Windows.Media;
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

    /// <summary>Populates the Bg/Panel/Fg/Muted/Border/Accent brushes for the current theme.</summary>
    public static void ApplyBrushes(ResourceDictionary resources)
    {
        var light = IsLightTheme();
        Set(resources, "Bg", light ? "#F3F3F3" : "#1E1E1E");
        Set(resources, "Panel", light ? "#FFFFFF" : "#2A2A2A");
        Set(resources, "Fg", light ? "#1A1A1A" : "#EAEAEA");
        Set(resources, "Muted", light ? "#666666" : "#9A9A9A");
        Set(resources, "Border", light ? "#CCCCCC" : "#3A3A3A");
        Set(resources, "Accent", light ? "#2D7DD2" : "#4C9AF0");
    }

    private static void Set(ResourceDictionary resources, string key, string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        resources[key] = brush;
    }
}
