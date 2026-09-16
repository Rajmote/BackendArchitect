namespace BackendArchitect.Concurrency.Pipelines;

// Example runner: where the bottleneck is, what unbounded costs, and the three bugs that kill pipelines.
public class PipelinesDemo
{
    public void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var oneMs = TimeSpan.FromMilliseconds(1);
        var twenty = TimeSpan.FromMilliseconds(20);
        var five = TimeSpan.FromMilliseconds(5);
        var arrival = TimeSpan.FromMilliseconds(8);
        const int orders = 60;

        // --- the bottleneck, and what scaling it does ---
        StageSpec[] Stages(int priceWorkers) =>
        [
            new("validate", 1, oneMs),
            new("price", priceWorkers, twenty),
            new("store", 1, five)
        ];

        Console.WriteLine($"{orders} orders arriving every 8ms through validate(1ms) -> price(20ms) -> store(5ms):");
        foreach (var priceWorkers in new[] { 1, 2, 4, 8 })
        {
            var run = await OrderPipeline.RunAsync(orders, Stages(priceWorkers), capacity: 32, arrival);
            Console.WriteLine($"  price x{priceWorkers,-2}: {run.OrdersPerSecond,6:F1} orders/sec  " +
                              $"peak queues [{string.Join(", ", run.Stages.Select(s => $"{s.Name} {s.PeakQueueDepth}"))}]");
        }
        Console.WriteLine("  -> throughput is the slowest stage alone; scaling it MOVES the bottleneck, it never removes it");

        // --- bounded vs unbounded when a stage is slow ---
        StageSpec[] slowStore =
        [
            new("validate", 1, oneMs),
            new("price", 1, oneMs),
            new("store", 1, TimeSpan.FromMilliseconds(25))
        ];

        var bounded = await OrderPipeline.RunAsync(orders, slowStore, capacity: 8, arrival);
        var unbounded = await OrderPipeline.RunAsync(orders, slowStore, capacity: null, arrival);

        Console.WriteLine();
        Console.WriteLine("A slow final stage (25ms), and what the queues in front of it do:");
        Console.WriteLine($"  bounded(8) : peak queues [{string.Join(", ", bounded.Stages.Select(s => $"{s.Name} {s.PeakQueueDepth}"))}]");
        Console.WriteLine($"  unbounded  : peak queues [{string.Join(", ", unbounded.Stages.Select(s => $"{s.Name} {s.PeakQueueDepth}"))}]");
        Console.WriteLine("  -> bounded pushes back to the front door; unbounded grows until the process dies");

        // --- bug 1: one bad item kills the worker ---
        var unprotected = await PipelineFaults.UnprotectedLoopAsync(items: 200, workers: 4, poisonEvery: 10);
        var protectedRun = await PipelineFaults.ProtectedLoopAsync(items: 200, workers: 4, poisonEvery: 10);

        Console.WriteLine();
        Console.WriteLine("200 orders, 4 workers, 1 in 10 malformed:");
        foreach (var report in new[] { unprotected, protectedRun })
            Console.WriteLine($"  {report.Scenario,-22}: processed {report.Processed,3}/200, " +
                              $"{report.WorkersSurviving}/4 workers alive, {report.DeadLettered} dead-lettered");
        Console.WriteLine("  -> the try must be INSIDE the loop; outside it, the worker still dies");

        // --- bug 2: premature completion ---
        var premature = await PipelineFaults.PrematureCompletionAsync(items: 40, workers: 4);
        var correct = await PipelineFaults.CompletionAfterAllWorkersAsync(items: 40, workers: 4);

        Console.WriteLine();
        Console.WriteLine("40 items, 4 workers, who calls Complete() on the next channel:");
        foreach (var report in new[] { premature, correct })
            Console.WriteLine($"  {report.Scenario,-28}: delivered {report.Delivered,2}/40, " +
                              $"{report.ChannelClosedFailures} worker(s) hit ChannelClosedException");
        Console.WriteLine("  -> Complete() is a statement about the STAGE; only its owner may say it");

        // --- bug 3: ordering ---
        var unpartitioned = await PipelineFaults.UnpartitionedAsync(keys: 12, eventsPerKey: 6, workers: 4);
        var partitioned = await PipelineFaults.PartitionedByKeyAsync(keys: 12, eventsPerKey: 6, workers: 4);

        Console.WriteLine();
        Console.WriteLine("12 orders x 6 events each, 4 workers:");
        foreach (var report in new[] { unpartitioned, partitioned })
            Console.WriteLine($"  {report.Scenario,-22}: {report.KeysOutOfOrder,2} of {report.KeysChecked} orders saw their events out of sequence");
        Console.WriteLine("  -> ordering holds WITHIN a key when every event for that key lands on the same worker");
    }
}
