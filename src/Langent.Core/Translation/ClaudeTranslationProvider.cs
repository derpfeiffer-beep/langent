using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Langent.Core.Translation;

/// <summary>
/// Streams translations from the Claude Messages API (SPEC §5). Uses one shared
/// <see cref="HttpClient"/> (timeout owned by the caller). No Anthropic SDK.
/// </summary>
public sealed class ClaudeTranslationProvider : ITranslationProvider
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions BodyOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyProvider;
    private readonly TimeSpan _overloadedRetryDelay;

    public ClaudeTranslationProvider(HttpClient http, Func<string?> apiKeyProvider, TimeSpan? overloadedRetryDelay = null)
    {
        _http = http;
        _apiKeyProvider = apiKeyProvider;
        _overloadedRetryDelay = overloadedRetryDelay ?? TimeSpan.FromSeconds(2);
    }

    public async IAsyncEnumerable<TranslationUpdate> TranslateAsync(
        TranslationRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var apiKey = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TranslationException(ErrorKind.MissingKey);
        }

        var systemPrompt = BuildSystemPrompt(request);
        var userContent = $"<text>\n{request.Text}\n</text>";
        var maxTokens = Math.Min(8192, Math.Max(256, request.Text.Length));
        var bodyJson = SerializeBody(request.Model, maxTokens, systemPrompt, userContent);

        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http
                    .SendAsync(CreateRequest(apiKey!, bodyJson), HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                throw new TranslationException(ErrorKind.Network);
            }

            var retry = false;

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var kind = await MapStatusAsync(response, ct).ConfigureAwait(false);
                    if (kind == ErrorKind.Overloaded && attempt == 0)
                    {
                        retry = true;
                    }
                    else
                    {
                        throw new TranslationException(kind, (int)response.StatusCode);
                    }
                }
                else
                {
                    Stream stream;
                    try
                    {
                        stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        throw new TranslationException(ErrorKind.Network);
                    }

                    await using (var e = ReadUpdatesAsync(stream, request.Model, ct).GetAsyncEnumerator(ct))
                    {
                        var started = false;
                        while (true)
                        {
                            TranslationUpdate current = null!;
                            var moved = false;
                            try
                            {
                                moved = await e.MoveNextAsync().ConfigureAwait(false);
                                if (moved)
                                {
                                    current = e.Current;
                                }
                            }
                            catch (TranslationException tex)
                                when (tex.Kind == ErrorKind.Overloaded && !started && attempt == 0)
                            {
                                retry = true;
                                break;
                            }

                            if (!moved)
                            {
                                break;
                            }

                            started = true;
                            yield return current;
                        }
                    }

                    if (!retry)
                    {
                        yield break;
                    }
                }
            }

            // Only reached when an overloaded response/early event triggered a retry.
            await Task.Delay(_overloadedRetryDelay, ct).ConfigureAwait(false);
        }
    }

    private static string BuildSystemPrompt(TranslationRequest request)
    {
        var target = Languages.EnglishNameOrCode(request.TargetLanguage);
        var secondary = Languages.EnglishNameOrCode(request.SecondaryLanguage);
        var source = string.IsNullOrWhiteSpace(request.SourceLanguageOverride)
            ? null
            : Languages.EnglishNameOrCode(request.SourceLanguageOverride);

        return PromptBuilder.BuildSystemPrompt(
            target, secondary, source, request.Tone, request.Glossary, request.CustomInstructions);
    }

    private static string SerializeBody(string model, int maxTokens, string systemPrompt, string userContent)
    {
        var body = new
        {
            model,
            max_tokens = maxTokens,
            stream = true,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userContent },
            },
        };

        return JsonSerializer.Serialize(body, BodyOptions);
    }

    private static HttpRequestMessage CreateRequest(string apiKey, string bodyJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static async Task<ErrorKind> MapStatusAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;

        var body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Body is best-effort for classification only.
        }

        return status switch
        {
            401 => ErrorKind.InvalidKey,
            429 => ErrorKind.RateLimited,
            500 or 529 => ErrorKind.Overloaded,
            400 => body.Contains("credit", StringComparison.OrdinalIgnoreCase)
                ? ErrorKind.NoCredit
                : ErrorKind.Unknown,
            _ => body.Contains("overloaded", StringComparison.OrdinalIgnoreCase)
                ? ErrorKind.Overloaded
                : ErrorKind.Unknown,
        };
    }

    private static async IAsyncEnumerable<TranslationUpdate> ReadUpdatesAsync(
        Stream stream,
        string model,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sse = new SseReader();
        var parser = new LanguageLineParser();
        var buffer = new char[4096];
        var input = 0;
        var output = 0;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            foreach (var ev in sse.Feed(new string(buffer, 0, read)))
            {
                switch (ev.Event)
                {
                    case "message_start":
                        input = ParseInputTokens(ev.Data) ?? input;
                        break;
                    case "content_block_delta":
                        var text = ParseTextDelta(ev.Data);
                        if (text is not null)
                        {
                            foreach (var update in parser.Push(text))
                            {
                                yield return update;
                            }
                        }

                        break;
                    case "message_delta":
                        output = ParseOutputTokens(ev.Data) ?? output;
                        break;
                    case "message_stop":
                        foreach (var update in parser.Finish())
                        {
                            yield return update;
                        }

                        yield return new TranslationUpdate.Completed(model, input, output);
                        yield break;
                    case "error":
                        throw MapErrorEvent(ev.Data);
                }
            }
        }

        // Stream ended without an explicit message_stop.
        foreach (var ev in sse.Flush())
        {
            if (ev.Event == "error")
            {
                throw MapErrorEvent(ev.Data);
            }
        }

        foreach (var update in parser.Finish())
        {
            yield return update;
        }

        yield return new TranslationUpdate.Completed(model, input, output);
    }

    private static int? ParseInputTokens(string data)
        => TryJson(data, doc =>
            doc.RootElement.TryGetProperty("message", out var message)
            && message.TryGetProperty("usage", out var usage)
            && usage.TryGetProperty("input_tokens", out var tokens)
                ? tokens.GetInt32()
                : (int?)null);

    private static int? ParseOutputTokens(string data)
        => TryJson(data, doc =>
            doc.RootElement.TryGetProperty("usage", out var usage)
            && usage.TryGetProperty("output_tokens", out var tokens)
                ? tokens.GetInt32()
                : (int?)null);

    private static string? ParseTextDelta(string data)
        => TryJson(data, doc =>
        {
            if (doc.RootElement.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("type", out var type)
                && type.GetString() == "text_delta"
                && delta.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }

            return null;
        });

    private static TranslationException MapErrorEvent(string data)
    {
        var errorType = TryJson(data, doc =>
            doc.RootElement.TryGetProperty("error", out var error)
            && error.TryGetProperty("type", out var type)
                ? type.GetString()
                : null);

        return errorType == "overloaded_error"
            ? new TranslationException(ErrorKind.Overloaded)
            : new TranslationException(ErrorKind.Unknown);
    }

    private static T? TryJson<T>(string data, Func<JsonDocument, T?> read)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            return read(doc);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
