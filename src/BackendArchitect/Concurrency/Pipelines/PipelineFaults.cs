using System.Collections.Concurrent;
using System.Threading.Channels;

namespace BackendArchitect.Concurrency.Pipelines;

public sealed record FaultReport(string Scenario, int Processed, int WorkersSurviving, int DeadLettered);

public sealed record CompletionReport(string Scenario, int Delivered, int ChannelClosedFailures);

public sealed record OrderingReport(string Scenario, int KeysChecked, int KeysOutOfOrder);

// The three bugs that kill pipelines in production. Each is paired with its fix, and both are measured
// rather than asserted in prose.
public static class PipelineFaults
{
    // ── 1. One bad item kills the worker ──────────────────────────────────────────────────────────
    //
    // The try/catch OUTSIDE the loop still catches the exception - and the worker still dies, because
    // the loop was its whole life. Throughput then degrades in steps as workers drop out one by one,
    // and nothing is ever logged.

    public static Task<FaultReport> UnprotectedLoopAsync(int items, int workers, int poisonEvery) =>
        RunWorkersAsync("try outside the loop", items, workers, poisonEvery, protectEachItem: false);

    public static Task<FaultReport> ProtectedLoopAsync(int items, int workers, int poisonEvery) =>
        RunWorkersAsync("try inside the loop", items, workers, poisonEvery, protectEachItem: true);

    private static async Task<FaultReport> RunWorkersAsync(
        string scenario, int items, int workers, int poisonEvery, bool protectEachItem)
    {
        var source = Channel.CreateUnbounded<int>();
        for (var i = 0; i < items; i++)
            await source.Writer.WriteAsync(i);
        source.Writer.Complete();

        var processed = 0;
        var deadLettered = 0;

        async Task<bool> WorkerAsync()
        {
            try
            {
                await foreach (var item in source.Reader.ReadAllAsync())
                {
                    if (!protectEachItem)
                    {
                        Process(item, poisonEvery);
                        Interlocked.Increment(ref processed);
                        continue;
                    }

                    try
                    {
                        Process(item, poisonEvery);
                        Interlocked.Increment(ref processed);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(ref deadLettered);   // park it, keep going
                    }
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;                                      // the worker is gone
            }
        }

        var results = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => WorkerAsync()));

        return new FaultReport(scenario, processed, results.Count(alive => alive), deadLettered);
    }

    private static void Process(int item, int poisonEvery)
    {
        if (item % poisonEvery == 0 && item > 0)
            throw new InvalidOperationException($"order {item} is malformed");
    }

    // ── 2. A worker completing the next channel on its own ────────────────────────────────────────
    //
    // Complete() is a statement about the whole STAGE. When one worker says it, the siblings still
    // holding items get ChannelClosedException and their work is lost - at shutdown, under load, which
    // is where tests are thinnest.

    public static Task<CompletionReport> PrematureCompletionAsync(int items, int workers) =>
        RunCompletionAsync("worker completes the channel", items, workers, completePerWorker: true);

    public static Task<CompletionReport> CompletionAfterAllWorkersAsync(int items, int workers) =>
        RunCompletionAsync("stage owner completes it", items, workers, completePerWorker: false);

    private static async Task<CompletionReport> RunCompletionAsync(
        string scenario, int items, int workers, bool completePerWorker)
    {
        var input = Channel.CreateUnbounded<int>();
        var output = Channel.CreateUnbounded<int>();

        for (var i = 0; i < items; i++)
            await input.Writer.WriteAsync(i);
        input.Writer.Complete();

        var closedFailures = 0;
        var slowWorkersHoldingItems = new TaskCompletionSource();
        var holding = 0;

        // Worker 0 is fast and drains the queue; the slow workers are each still holding an item when
        // it finishes. Without that rendezvous worker 0 would take everything before they even read,
        // and the bug would not reproduce.
        async Task WorkerAsync(int id)
        {
            var reachedFirstItem = false;

            try
            {
                await foreach (var item in input.Reader.ReadAllAsync())
                {
                    if (id == 0)
                    {
                        if (!reachedFirstItem)
                        {
                            reachedFirstItem = true;
                            await slowWorkersHoldingItems.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        }
                    }
                    else
                    {
                        if (!reachedFirstItem)
                        {
                            reachedFirstItem = true;
                            if (Interlocked.Increment(ref holding) == workers - 1)
                                slowWorkersHoldingItems.TrySetResult();
                        }

                        await Task.Delay(50);
                    }

                    await output.Writer.WriteAsync(item);
                }

                if (completePerWorker)
                    output.Writer.Complete();
            }
            catch (ChannelClosedException)
            {
                Interlocked.Increment(ref closedFailures);
            }
        }

        var running = Enumerable.Range(0, workers).Select(WorkerAsync).ToArray();
        await Task.WhenAll(running);

        if (!completePerWorker)
            output.Writer.Complete();                 // the stage owner, once, after everyone is done

        var delivered = 0;
        await foreach (var _ in output.Reader.ReadAllAsync())
            delivered++;

        return new CompletionReport(scenario, delivered, closedFailures);
    }

    // ── 3. Parallel workers destroy per-key ordering ──────────────────────────────────────────────
    //
    // Ordering holds WITHIN a key and parallelism holds ACROSS keys - as long as every message for a
    // key lands on the same worker.

    public static Task<OrderingReport> UnpartitionedAsync(int keys, int eventsPerKey, int workers) =>
        RunOrderingAsync("one queue, N workers", keys, eventsPerKey, workers, partition: false);

    public static Task<OrderingReport> PartitionedByKeyAsync(int keys, int eventsPerKey, int workers) =>
        RunOrderingAsync("partitioned by key", keys, eventsPerKey, workers, partition: true);

    private static async Task<OrderingReport> RunOrderingAsync(
        string scenario, int keys, int eventsPerKey, int workers, bool partition)
    {
        var lanes = Enumerable.Range(0, partition ? workers : 1)
            .Select(_ => Channel.CreateUnbounded<(int Key, int Sequence)>())
            .ToArray();

        var observed = new ConcurrentDictionary<int, ConcurrentQueue<int>>();

        var consumers = Enumerable.Range(0, workers).Select(id => Task.Run(async () =>
        {
            var lane = lanes[partition ? id : 0];
            await foreach (var (key, sequence) in lane.Reader.ReadAllAsync())
            {
                // Uneven work is what actually reorders events - identical costs would hide the bug.
                await Task.Delay(sequence % 3 == 0 ? 6 : 1);
                observed.GetOrAdd(key, _ => new ConcurrentQueue<int>()).Enqueue(sequence);
            }
        })).ToArray();

        // Key-major: a key's events go in back to back, so parallel workers grab them simultaneously.
        // Spreading them out would let each one finish before the next arrived and hide the reordering.
        for (var key = 0; key < keys; key++)
            for (var sequence = 0; sequence < eventsPerKey; sequence++)
            {
                var lane = partition ? StableLane(key, workers) : 0;
                await lanes[lane].Writer.WriteAsync((key, sequence));
            }

        foreach (var lane in lanes)
            lane.Writer.Complete();
        await Task.WhenAll(consumers);

        var outOfOrder = observed.Count(entry => !IsAscending(entry.Value));
        return new OrderingReport(scenario, observed.Count, outOfOrder);
    }

    // FNV-1a: stable across processes and restarts, unlike string.GetHashCode() which .NET randomizes
    // per process - the same trap as choosing a Cosmos partition key.
    private static int StableLane(int key, int workers)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var b in BitConverter.GetBytes(key))
        {
            hash ^= b;
            hash *= prime;
        }

        return (int)(hash % (uint)workers);
    }

    private static bool IsAscending(IEnumerable<int> sequence)
    {
        var previous = -1;
        foreach (var value in sequence)
        {
            if (value <= previous)
                return false;
            previous = value;
        }

        return true;
    }
}
