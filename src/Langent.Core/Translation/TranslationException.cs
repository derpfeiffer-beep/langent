namespace Langent.Core.Translation;

/// <summary>Error classification for translation failures (SPEC §5.6).</summary>
public enum ErrorKind
{
    MissingKey,
    InvalidKey,
    NoCredit,
    RateLimited,
    Overloaded,
    Network,
    Unknown,
}

/// <summary>
/// Thrown by <see cref="ITranslationProvider"/> on failure. The German user-facing
/// messages (SPEC §5.6) are built from <see cref="Kind"/> in the UI layer.
/// </summary>
public sealed class TranslationException : Exception
{
    public TranslationException(ErrorKind kind, int? httpStatus = null, string? message = null)
        : base(message ?? kind.ToString())
    {
        Kind = kind;
        HttpStatus = httpStatus;
    }

    public ErrorKind Kind { get; }

    /// <summary>HTTP status code when the failure came from an HTTP response.</summary>
    public int? HttpStatus { get; }
}
