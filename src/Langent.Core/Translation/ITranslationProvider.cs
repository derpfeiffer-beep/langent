namespace Langent.Core.Translation;

/// <summary>Streams a translation from a provider (SPEC §5.5).</summary>
public interface ITranslationProvider
{
    IAsyncEnumerable<TranslationUpdate> TranslateAsync(TranslationRequest request, CancellationToken ct);
}
