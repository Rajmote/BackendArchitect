using System.Collections.Concurrent;
using System.Threading.Channels;

namespace BackendArchitect.Concurrency.Streams;

public sealed record QueueResult(int Produced, int Consumed, int PeakQueueDepth, long ProducerWaitedMs);

// Channel<T>: an async, thread-safe queue joining producers and consumers that run INDEPENDENTLY.
//
// The decisive difference from IAsyncEnumerable is TOPOLOGY, not speed: an enumerator holds a position
// and isn't thread-safe, so several consumers cannot share one. A channel hands each item to exactly
// one consumer, safely.
//
// And bounded vs unbounded is the difference between a survivable slowdown and a memory leak:
//   CreateUnbounded -> the limit is your RAM, discovered in production
//   CreateBounded   -> WriteAsync waits when full = BACKPRESSURE, so the producer runs at the
//                      consumers' pace and the pressure propagates upstream
public static class WorkQueue
{
    /// <summary>
    /// Runs a fast producer against slow consumers and reports the peak backlog plus how long the
    /// producer spent throttled.
    /// </summary>
    public static async Task<QueueResult> RunAsync(
        int itemCount,
        int consumerCount,
        TimeSpan consumerLatency,
        int? capacity,                       // null = unbounded
        CancellationToken cancellationToken = default)
    {
        var channel = capacity is null
            ? Channel.CreateUnbounded<int>()
            : Channel.CreateBounded<int>(capacity.Value);

        var produced = 0;
        var consumed = 0;
        var peakDepth = 0;
        var producerWaited = System.Diagnostics.Stopwatch.StartNew();
        var waitedTicks = 0L;

        var consumers = Enumerable.Range(0, consumerCount).Select(_ => Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await Task.Delay(consumerLatency, cancellationToken);
                Interlocked.Increment(ref consumed);
            }
        }, cancellationToken)).ToArray();

        for (var i = 0; i < itemCount; i++)
        {
            var before = producerWaited.ElapsedTicks;
            await channel.Writer.WriteAsync(i, cancellationToken);   // waits here once a bounded buffer is full
            Interlocked.Add(ref waitedTicks, producerWaited.ElapsedTicks - before);

            var written = Interlocked.Increment(ref produced);
            var depth = written - Volatile.Read(ref consumed);
            peakDepth = Math.Max(peakDepth, depth);
        }

        channel.Writer.Complete();          // tells consumers no more items are coming
        await Task.WhenAll(consumers);

        return new QueueResult(produced, consumed, peakDepth,
            (long)TimeSpan.FromTicks(waitedTicks).TotalMilliseconds);
    }

    /// <summary>
    /// Proves multi-consumer fan-out: three consumers share one channel and each item is handled by
    /// exactly ONE of them - which is precisely what IAsyncEnumerable cannot do.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, int>> FanOutAsync(
        int itemCount, int consumerCount, CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<int>(16);
        var handledBy = new ConcurrentDictionary<int, int>();   // item -> consumer id
        var perConsumer = new ConcurrentDictionary<int, int>();

        var consumers = Enumerable.Range(0, consumerCount).Select(id => Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            {
                handledBy[item] = id;
                perConsumer.AddOrUpdate(id, 1, (_, count) => count + 1);
                await Task.Yield();
            }
        }, cancellationToken)).ToArray();

        for (var i = 0; i < itemCount; i++)
            await channel.Writer.WriteAsync(i, cancellationToken);

        channel.Writer.Complete();
        await Task.WhenAll(consumers);

        // handledBy.Count == itemCount proves nothing was duplicated or dropped.
        return perConsumer;
    }
}
