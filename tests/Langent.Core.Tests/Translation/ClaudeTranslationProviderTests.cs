using System.Net;
using System.Text;
using Langent.Core.Tests.TestDoubles;
using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Translation;

public sealed class ClaudeTranslationProviderTests
{
    private const string SuccessSse =
        "event: message_start\n" +
        "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":12}}}\n\n" +
        "event: content_block_delta\n" +
        "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"en\\n\"}}\n\n" +
        "event: content_block_delta\n" +
        "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Good morning\"}}\n\n" +
        "event: message_delta\n" +
        "data: {\"type\":\"message_delta\",\"usage\":{\"output_tokens\":5}}\n\n" +
        "event: message_stop\n" +
        "data: {\"type\":\"message_stop\"}\n\n";

    private static TranslationRequest Req(string text = "Guten Morgen", string model = "claude-haiku-5-5")
        => new(text, "en", "de", null, model, Tone.Neutral, [], "");

    private static ClaudeTranslationProvider Provider(FakeHttpMessageHandler handler, string? key = "test-key")
        => new(new HttpClient(handler), () => key, TimeSpan.Zero);

    private static HttpResponseMessage Sse(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    private static HttpResponseMessage StatusResponse(HttpStatusCode code, string body)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<List<TranslationUpdate>> Collect(
        ClaudeTranslationProvider provider, TranslationRequest request, CancellationToken ct = default)
    {
        var updates = new List<TranslationUpdate>();
        await foreach (var u in provider.TranslateAsync(request, ct))
        {
            updates.Add(u);
        }

        return updates;
    }

    [Fact]
    public async Task Streams_language_text_and_usage()
    {
        var handler = new FakeHttpMessageHandler(_ => Sse(SuccessSse));

        var updates = await Collect(Provider(handler), Req());

        var language = Assert.Single(updates.OfType<TranslationUpdate.SourceLanguageDetected>());
        Assert.Equal("en", language.IsoCode);

        var text = string.Concat(updates.OfType<TranslationUpdate.TextDelta>().Select(t => t.Text));
        Assert.Equal("Good morning", text);

        var completed = Assert.Single(updates.OfType<TranslationUpdate.Completed>());
        Assert.Equal("claude-haiku-5-5", completed.Model);
        Assert.Equal(12, completed.InputTokens);
        Assert.Equal(5, completed.OutputTokens);
    }

    [Fact]
    public async Task Sends_expected_headers_and_body_without_temperature()
    {
        var handler = new FakeHttpMessageHandler(_ => Sse(SuccessSse));

        await Collect(Provider(handler), Req(text: "Hallo"));

        var request = handler.Requests[0];
        Assert.Equal("test-key", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());

        var body = handler.RequestBodies[0];
        Assert.Contains("\"stream\":true", body);
        Assert.Contains("\"system\":", body);
        Assert.Contains("<text>\\nHallo\\n</text>", body);
        Assert.Contains("\"max_tokens\":256", body);
        Assert.DoesNotContain("temperature", body);
    }

    [Theory]
    [InlineData(401, "", ErrorKind.InvalidKey)]
    [InlineData(429, "", ErrorKind.RateLimited)]
    [InlineData(400, "{\"error\":{\"message\":\"Your credit balance is too low\"}}", ErrorKind.NoCredit)]
    [InlineData(400, "{\"error\":{\"message\":\"bad request\"}}", ErrorKind.Unknown)]
    public async Task Maps_http_errors(int status, string body, ErrorKind expected)
    {
        var handler = new FakeHttpMessageHandler(_ => StatusResponse((HttpStatusCode)status, body));

        var ex = await Assert.ThrowsAsync<TranslationException>(() => Collect(Provider(handler), Req()));

        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task Network_failure_maps_to_network_error()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("boom"));

        var ex = await Assert.ThrowsAsync<TranslationException>(() => Collect(Provider(handler), Req()));

        Assert.Equal(ErrorKind.Network, ex.Kind);
    }

    [Fact]
    public async Task Missing_key_throws_before_any_http_call()
    {
        var handler = new FakeHttpMessageHandler(_ => Sse(SuccessSse));

        var ex = await Assert.ThrowsAsync<TranslationException>(() => Collect(Provider(handler, key: null), Req()));

        Assert.Equal(ErrorKind.MissingKey, ex.Kind);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Http_500_is_retried_once_then_succeeds()
    {
        var handler = new FakeHttpMessageHandler(
            _ => StatusResponse(HttpStatusCode.InternalServerError, ""),
            _ => Sse(SuccessSse));

        var updates = await Collect(Provider(handler), Req());

        Assert.Equal(2, handler.CallCount);
        Assert.Contains(updates, u => u is TranslationUpdate.Completed);
    }

    [Fact]
    public async Task Early_overloaded_event_is_retried_once_then_succeeds()
    {
        const string overloaded =
            "event: error\ndata: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\"}}\n\n";
        var handler = new FakeHttpMessageHandler(_ => Sse(overloaded), _ => Sse(SuccessSse));

        var updates = await Collect(Provider(handler), Req());

        Assert.Equal(2, handler.CallCount);
        var text = string.Concat(updates.OfType<TranslationUpdate.TextDelta>().Select(t => t.Text));
        Assert.Equal("Good morning", text);
    }

    [Fact]
    public async Task Cancelled_token_surfaces_operation_cancelled()
    {
        var handler = new FakeHttpMessageHandler(_ => Sse(SuccessSse));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Collect(Provider(handler), Req(), cts.Token));
    }
}
