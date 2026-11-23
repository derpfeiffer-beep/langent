namespace Langent.Core.Translation;

/// <summary>A translation request (SPEC §5.5). Languages are ISO codes.</summary>
public sealed record TranslationRequest(
    string Text,
    string TargetLanguage,
    string SecondaryLanguage,
    string? SourceLanguageOverride,
    string Model,
    Tone Tone,
    IReadOnlyList<GlossaryEntry> Glossary,
    string CustomInstructions);
