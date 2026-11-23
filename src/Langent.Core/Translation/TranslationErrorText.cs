namespace Langent.Core.Translation;

/// <summary>German user-facing messages for translation errors (SPEC §5.6).</summary>
public static class TranslationErrorText
{
    public static string For(TranslationException exception)
        => For(exception.Kind, exception.HttpStatus);

    public static string For(ErrorKind kind, int? httpStatus = null) => kind switch
    {
        ErrorKind.MissingKey => "Kein API-Schlüssel hinterlegt. Bitte in den Einstellungen eintragen.",
        ErrorKind.InvalidKey => "API-Schlüssel ungültig.",
        ErrorKind.NoCredit => "API-Guthaben aufgebraucht.",
        ErrorKind.RateLimited => "Zu viele Anfragen, bitte kurz warten.",
        ErrorKind.Overloaded => "Claude ist gerade überlastet.",
        ErrorKind.Network => "Keine Verbindung zur Claude API.",
        _ => httpStatus is { } status
            ? $"Übersetzung fehlgeschlagen ({status})."
            : "Übersetzung fehlgeschlagen.",
    };
}
