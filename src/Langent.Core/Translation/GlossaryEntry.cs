namespace Langent.Core.Translation;

/// <summary>A glossary mapping (SPEC §5.3). An empty <see cref="Target"/> means "keep unchanged".</summary>
public sealed record GlossaryEntry(string Source, string Target);
