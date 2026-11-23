namespace Langent.Core.Translation;

/// <summary>Streaming translation events (SPEC §5.5).</summary>
public abstract record TranslationUpdate
{
    public sealed record SourceLanguageDetected(string? IsoCode) : TranslationUpdate;

    public sealed record TextDelta(string Text) : TranslationUpdate;

    public sealed record Completed(string Model, int InputTokens, int OutputTokens) : TranslationUpdate;
}
