using BackendArchitect.Concurrency.Streams;

namespace BackendArchitect.Tests.Concurrency;

// Concurrency · Task / ValueTask / IAsyncEnumerable / Channel — pick by SHAPE: how many values,
// when they arrive, and who is pulling.
public class StreamsTests
{
    private static readonly TimeSpan PerRow = TimeSpan.FromMilliseconds(5);
    private const int Rows = 40;

    // --- batch vs stream ---

    [Fact]
    public async Task Buffering_HoldsTheWholeResultSetInMemory()
    {
        var reader = new OrderReader(Rows, PerRow);

        var all = await reader.BufferAllAsync();

        Assert.Equal(Rows, all.Count);
        Assert.Equal(Rows, reader.PeakRowsInMemory);   // every row alive at once
    }

    [Fact]
    public async Task Streaming_KeepsOnlyOneRowAlive()
    {
        var reader = new OrderReader(Rows, PerRow);
        var seen = 0;

        await foreach (var _ in reader.StreamAsync())
            seen++;

        Assert.Equal(Rows, seen);                      // same data...
        Assert.Equal(1, reader.PeakRowsInMemory);      // ...at constant memory
    }

    [Fact]
    public async Task Streaming_DeliversTheFirstRowFarSooner()
    {
        var bufferedMs = await OrderReader.TimeToFirstRowWhenBufferedAsync(new OrderReader(Rows, PerRow));
        var streamedMs = await OrderReader.TimeToFirstRowWhenStreamedAsync(new OrderReader(Rows, PerRow));

        Assert.True(streamedMs < bufferedMs / 2,
            $"streaming should reach row 1 much sooner; streamed {streamedMs}ms vs buffered {bufferedMs}ms");
    }

    [Fact]
    public async Task Streaming_IsLazy_SoStoppingEarlyDoesLessWork()
    {
        var reader = new OrderReader(Rows, PerRow);

        await foreach (var _ in reader.StreamAsync())
            break;                                     // take one row and walk away

        Assert.Equal(1, reader.PeakRowsInMemory);      // the remaining 39 were never produced
    }

    // --- ValueTask ---

    [Fact]
    public async Task ValueTask_CompletesSynchronously_OnACacheHit()
    {
        var lookup = new CachedCustomerLookup(TimeSpan.FromMilliseconds(10));

        await lookup.GetAsync(1);                      // miss -> real async
        var second = lookup.GetAsync(1);               // hit  -> already complete

        Assert.True(second.IsCompleted);               // no state machine, no allocation
        Assert.Equal(1, lookup.SynchronousCompletions);
        Assert.Equal(1, lookup.AsynchronousCompletions);
        await second;                                  // awaited exactly once, per the rules
    }

    [Fact]
    public async Task ValueTask_AvoidsTheAsyncPath_ForMostCallsOnACacheHeavyWorkload()
    {
        var lookup = new CachedCustomerLookup(TimeSpan.FromMilliseconds(5));

        for (var i = 0; i < 100; i++)
            await lookup.GetAsync(i % 5);              // 5 distinct ids

        Assert.Equal(5, lookup.AsynchronousCompletions);
        Assert.Equal(95, lookup.SynchronousCompletions);
    }

    // --- channels ---

    [Fact]
    public async Task AnUnboundedChannel_LetsTheBacklogGrowUnchecked()
    {
        var result = await WorkQueue.RunAsync(itemCount: 60, consumerCount: 1,
            consumerLatency: TimeSpan.FromMilliseconds(2), capacity: null);

        Assert.Equal(60, result.Consumed);
        Assert.True(result.PeakQueueDepth > 20,
            $"a fast producer should pile up; peak was {result.PeakQueueDepth}");
    }

    [Fact]
    public async Task ABoundedChannel_CapsTheBacklog_AndThrottlesTheProducer()
    {
        const int capacity = 5;

        var result = await WorkQueue.RunAsync(itemCount: 60, consumerCount: 1,
            consumerLatency: TimeSpan.FromMilliseconds(2), capacity: capacity);

        Assert.Equal(60, result.Consumed);                 // nothing lost
        Assert.True(result.PeakQueueDepth <= capacity + 2,
            $"the backlog must stay near the capacity; peak was {result.PeakQueueDepth}");
        Assert.True(result.ProducerWaitedMs > 0, "the producer should have been throttled");
    }

    [Fact]
    public async Task BoundedBeatsUnbounded_OnMemory_AndThatIsTheWholePoint()
    {
        var unbounded = await WorkQueue.RunAsync(60, 1, TimeSpan.FromMilliseconds(2), capacity: null);
        var bounded = await WorkQueue.RunAsync(60, 1, TimeSpan.FromMilliseconds(2), capacity: 5);

        Assert.True(bounded.PeakQueueDepth < unbounded.PeakQueueDepth);
        Assert.True(bounded.ProducerWaitedMs > unbounded.ProducerWaitedMs);   // the trade: time for memory
    }

    [Fact]
    public async Task AChannel_FansOutToSeveralConsumers_EachItemHandledExactlyOnce()
    {
        const int items = 30;

        var perConsumer = await WorkQueue.FanOutAsync(items, consumerCount: 3);

        Assert.Equal(items, perConsumer.Values.Sum());   // nothing duplicated, nothing dropped
        Assert.True(perConsumer.Count >= 1);
    }

    [Fact]
    public async Task NothingIsLost_HoweverManyConsumersShareTheChannel()
    {
        foreach (var consumers in new[] { 1, 2, 4 })
        {
            var result = await WorkQueue.RunAsync(itemCount: 40, consumerCount: consumers,
                consumerLatency: TimeSpan.FromMilliseconds(1), capacity: 8);

            Assert.Equal(40, result.Produced);
            Assert.Equal(40, result.Consumed);
        }
    }
}
