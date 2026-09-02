namespace BackendArchitect.Concurrency.Streams;

// Example runner: batch vs stream, ValueTask's synchronous path, and bounded vs unbounded channels.
public class StreamsDemo
{
    public void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        // --- Task<List<T>> vs IAsyncEnumerable<T> ---
        var perRow = TimeSpan.FromMilliseconds(5);
        const int rows = 40;

        var buffering = new OrderReader(rows, perRow);
        var streaming = new OrderReader(rows, perRow);

        var bufferedFirstMs = await OrderReader.TimeToFirstRowWhenBufferedAsync(buffering);
        var streamedFirstMs = await OrderReader.TimeToFirstRowWhenStreamedAsync(streaming);

        Console.WriteLine($"{rows} rows at {perRow.TotalMilliseconds}ms each — when can you act on row 1?");
        Console.WriteLine($"  Task<List<Order>>      : {bufferedFirstMs,4} ms, peak {buffering.PeakRowsInMemory,2} rows in memory");
        Console.WriteLine($"  IAsyncEnumerable<Order>: {streamedFirstMs,4} ms, peak {streaming.PeakRowsInMemory,2} row  in memory");
        Console.WriteLine("  -> both are async; only one of them buffers the whole result set");

        // --- ValueTask on a cache-heavy path ---
        var lookup = new CachedCustomerLookup(TimeSpan.FromMilliseconds(20));
        for (var i = 0; i < 100; i++)
            await lookup.GetAsync(id: i % 5);        // 5 misses, then 95 hits

        Console.WriteLine();
        Console.WriteLine("ValueTask on a 95%-cache-hit path (100 calls):");
        Console.WriteLine($"  completed synchronously (no allocation): {lookup.SynchronousCompletions,3}");
        Console.WriteLine($"  went to the database (real async)      : {lookup.AsynchronousCompletions,3}");

        // --- bounded vs unbounded ---
        const int items = 60;
        var unbounded = await WorkQueue.RunAsync(items, consumerCount: 1,
            consumerLatency: TimeSpan.FromMilliseconds(2), capacity: null);
        var bounded = await WorkQueue.RunAsync(items, consumerCount: 1,
            consumerLatency: TimeSpan.FromMilliseconds(2), capacity: 5);

        Console.WriteLine();
        Console.WriteLine($"Fast producer, slow consumer ({items} items):");
        Console.WriteLine($"  unbounded channel: peak backlog {unbounded.PeakQueueDepth,2} items, producer waited {unbounded.ProducerWaitedMs,4} ms");
        Console.WriteLine($"  bounded(5)        : peak backlog {bounded.PeakQueueDepth,2} items, producer waited {bounded.ProducerWaitedMs,4} ms  <- backpressure");
        Console.WriteLine("  -> unbounded grows until RAM runs out; bounded throttles the producer instead");

        // --- multi-consumer fan-out ---
        var perConsumer = await WorkQueue.FanOutAsync(itemCount: 30, consumerCount: 3);
        Console.WriteLine();
        Console.WriteLine("One channel, 3 consumers (what IAsyncEnumerable cannot do):");
        Console.WriteLine($"  items handled per consumer: {string.Join(", ", perConsumer.OrderBy(p => p.Key).Select(p => $"#{p.Key}={p.Value}"))}");
        Console.WriteLine($"  total = {perConsumer.Values.Sum()} of 30, each item handled exactly once");
    }
}
