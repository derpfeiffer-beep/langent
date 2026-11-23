using System.Net.Http;
using Langent.Core.Translation;

namespace Langent.App.Services;

/// <summary>Result of a "Verbindung testen" attempt (SPEC §8.3).</summary>
public sealed record ConnectionTestResult(bool Success, string Message);

/// <summary>
/// Tests an API key by running a tiny translation ("Hallo") and reporting success
/// or the German error message (SPEC §8.3).
/// </summary>
public sealed class ConnectionTester
{
    private readonly HttpClient _http;

    public ConnectionTester(HttpClient http) => _http = http;

    public async Task<ConnectionTestResult> TestAsync(string? apiKey, string model, CancellationToken ct)
    {
        var provider = new ClaudeTranslationProvider(_http, () => apiKey);
        var request = new TranslationRequest("Hallo", "en", "de", null, model, Tone.Neutral, [], "");

        try
        {
            await foreach (var _ in provider.TranslateAsync(request, ct).ConfigureAwait(false))
            {
            }

            return new ConnectionTestResult(true, "Verbindung erfolgreich.");
        }
        catch (TranslationException ex)
        {
            return new ConnectionTestResult(false, TranslationErrorText.For(ex));
        }
        catch (OperationCanceledException)
        {
            return new ConnectionTestResult(false, "Test abgebrochen.");
        }
    }
}
