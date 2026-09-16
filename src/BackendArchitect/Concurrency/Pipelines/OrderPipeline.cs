using System.Diagnostics;
using System.Threading.Channels;

namespace BackendArchitect.Concurrency.Pipelines;

public sealed record StageSpec(string Name, int Workers, TimeSpan Latency);

public sealed record StageStat(string Name, int Workers, int PeakQueueDepth);

public sealed record PipelineResult(int Processed, long ElapsedMs, IReadOnlyList<StageStat> Stages)
{
    public double OrdersPerSecond => ElapsedMs == 0 ? 0 : Processed * 1000.0 / ElapsedMs;

    /// <summary>The stage with the deepest queue in front of it is the constraint.</summary>
    public StageStat Bottleneck => Stages.MaxBy(stage => stage.PeakQueueDepth)!;
}

// A pipeline is stages connected by queues. Each stage is a `while (await reader.ReadAsync())` loop;
// a "worker" is one running copy of that loop, and several workers share one channel.
//
// Two numbers describe the whole thing:
//   LATENCY    = the sum of every stage's per-item cost  (how long ONE item takes end to end)
//   THROUGHPUT = the slowest stage alone                 (how many items finish per second)
//
// The queue that keeps filling sits immediately BEFORE the slow stage - which is why queue depth is
// the metric that finds a bottleneck without a debugger.
public static class OrderPipeline
{
    public static async Task<PipelineResult> RunAsync(
        int orderCount,
        StageSpec[] stages,
        int? capacity,                       // null = unbounded, and nothing ever pushes back
        TimeSpan arrivalInterval = default,  // how fast orders show up at the front door
        CancellationToken cancellationToken = default)
    {
        var channels = new Channel<int>[stages.Length];
        for (var i = 0; i < stages.Length; i++)
            channels[i] = capacity is null
                ? Channel.CreateUnbounded<int>()
                : Channel.CreateBounded<int>(capacity.Value);

        var written = new int[stages.Length];
        var read = new int[stages.Length];
        var peak = new int[stages.Length];
        var processed = 0;

        async Task PublishAsync(int stage, int order)
        {
            await channels[stage].Writer.WriteAsync(order, cancellationToken);
            var depth = Interlocked.Increment(ref written[stage]) - Volatile.Read(ref read[stage]);
            InterlockedMax(ref peak[stage], depth);
        }

        var clock = Stopwatch.StartNew();

        // Orders arrive at a rate, the way requests do. A producer running flat out would simply fill
        // the FIRST queue and hide which stage is actually the constraint.
        var producer = Task.Run(async () =>
        {
            for (var order = 0; order < orderCount; order++)
            {
                if (arrivalInterval > TimeSpan.Zero)
                    await Task.Delay(arrivalInterval, cancellationToken);

                await PublishAsync(0, order);
            }
        }, cancellationToken);

        var stageWorkers = new Task[stages.Length][];
        for (var s = 0; s < stages.Length; s++)
        {
            var stage = s;
            stageWorkers[stage] = Enumerable.Range(0, stages[stage].Workers).Select(_ => Task.Run(async () =>
            {
                await foreach (var order in channels[stage].Reader.ReadAllAsync(cancellationToken))
                {
                    Interlocked.Increment(ref read[stage]);
                    await Task.Delay(stages[stage].Latency, cancellationToken);

                    if (stage + 1 < stages.Length)
                        await PublishAsync(stage + 1, order);
                    else
                        Interlocked.Increment(ref processed);
                }
            }, cancellationToken)).ToArray();
        }

        // Completion cascades ONE STAGE AT A TIME, and only once every worker of that stage is done.
        // A single worker calling Complete() would strand its siblings' in-flight writes.
        await producer;
        channels[0].Writer.Complete();

        for (var s = 0; s < stages.Length; s++)
        {
            await Task.WhenAll(stageWorkers[s]);        // faults surface here instead of vanishing
            if (s + 1 < stages.Length)
                channels[s + 1].Writer.Complete();
        }

        var stats = stages.Select((spec, i) => new StageStat(spec.Name, spec.Workers, peak[i])).ToArray();
        return new PipelineResult(processed, clock.ElapsedMilliseconds, stats);
    }

    // Compare-and-swap retry loop: read the current max, and swap only if it is still what we read.
    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, current) == current)
                return;
    }
}
