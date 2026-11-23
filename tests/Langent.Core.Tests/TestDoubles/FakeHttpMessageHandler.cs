namespace Langent.Core.Tests.TestDoubles;

/// <summary>
/// Test <see cref="HttpMessageHandler"/>: returns queued responses (the last one
/// repeats), records requests and their bodies, and honours cancellation.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders;

    public FakeHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders)
        => _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responders);

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        var responder = _responders.Count > 1 ? _responders.Dequeue() : _responders.Peek();
        return responder(request);
    }
}
