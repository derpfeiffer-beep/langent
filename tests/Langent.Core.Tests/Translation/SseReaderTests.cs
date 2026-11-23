using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Translation;

public sealed class SseReaderTests
{
    [Fact]
    public void Parses_several_events_in_one_chunk_and_drops_ping()
    {
        var reader = new SseReader();

        var events = reader.Feed(
            "event: message_start\ndata: {\"a\":1}\n\n" +
            "event: ping\ndata: {}\n\n" +
            "data: hello\n\n");

        Assert.Equal(2, events.Count);
        Assert.Equal("message_start", events[0].Event);
        Assert.Equal("{\"a\":1}", events[0].Data);
        Assert.Null(events[1].Event);
        Assert.Equal("hello", events[1].Data);
    }

    [Fact]
    public void Handles_one_event_split_across_two_chunks()
    {
        var reader = new SseReader();

        var first = reader.Feed("event: message_delta\ndata: {\"x\"");
        Assert.Empty(first);

        var second = reader.Feed(":5}\n\n");
        Assert.Single(second);
        Assert.Equal("message_delta", second[0].Event);
        Assert.Equal("{\"x\":5}", second[0].Data);
    }

    [Fact]
    public void Ping_event_is_ignored()
    {
        var reader = new SseReader();

        var events = reader.Feed("event: ping\ndata: {}\n\n");

        Assert.Empty(events);
    }

    [Fact]
    public void Error_event_is_surfaced()
    {
        var reader = new SseReader();

        var events = reader.Feed("event: error\ndata: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\"}}\n\n");

        Assert.Single(events);
        Assert.Equal("error", events[0].Event);
        Assert.Contains("overloaded_error", events[0].Data);
    }
}
