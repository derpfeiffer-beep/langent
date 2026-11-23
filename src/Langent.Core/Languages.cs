namespace Langent.Core;

/// <summary>One supported language (SPEC §6): ISO 639-1 code, German and English name.</summary>
public sealed record Language(string Code, string GermanName, string EnglishName);

/// <summary>The fixed language list (SPEC §6).</summary>
public static class Languages
{
    public static IReadOnlyList<Language> All { get; } =
    [
        new("de", "Deutsch", "German"),
        new("en", "Englisch", "English"),
        new("fr", "Französisch", "French"),
        new("es", "Spanisch", "Spanish"),
        new("it", "Italienisch", "Italian"),
        new("nl", "Niederländisch", "Dutch"),
        new("pt", "Portugiesisch", "Portuguese"),
        new("pl", "Polnisch", "Polish"),
        new("cs", "Tschechisch", "Czech"),
        new("sk", "Slowakisch", "Slovak"),
        new("hu", "Ungarisch", "Hungarian"),
        new("ro", "Rumänisch", "Romanian"),
        new("sv", "Schwedisch", "Swedish"),
        new("da", "Dänisch", "Danish"),
        new("fi", "Finnisch", "Finnish"),
        new("no", "Norwegisch", "Norwegian"),
        new("tr", "Türkisch", "Turkish"),
        new("el", "Griechisch", "Greek"),
        new("ru", "Russisch", "Russian"),
        new("uk", "Ukrainisch", "Ukrainian"),
        new("zh", "Chinesisch", "Chinese"),
        new("ja", "Japanisch", "Japanese"),
        new("ko", "Koreanisch", "Korean"),
        new("ar", "Arabisch", "Arabic"),
    ];

    private static readonly IReadOnlyDictionary<string, Language> ByCode =
        All.ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string code, out Language language)
        => ByCode.TryGetValue(code, out language!);

    /// <summary>Returns the language for <paramref name="code"/>, or null if unknown.</summary>
    public static Language? Find(string? code)
        => code is not null && ByCode.TryGetValue(code, out var l) ? l : null;

    /// <summary>English name for a code, or the code itself if unknown (for prompt building).</summary>
    public static string EnglishNameOrCode(string code)
        => Find(code)?.EnglishName ?? code;
}
