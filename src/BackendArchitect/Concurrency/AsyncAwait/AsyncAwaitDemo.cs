namespace BackendArchitect.Concurrency.AsyncAwait;

// Example runner: what async actually buys you, measured — wall-clock for sequential vs concurrent,
// and thread consumption for blocking vs truly async.
public class AsyncAwaitDemo
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(50);
    private const int Calls = 20;

    public void Run() => RunAsync().GetAwaiter().GetResult();   // sync entry point for the demo runner

    private static async Task RunAsync()
    {
        // --- sequential vs concurrent ---
        var sequentialIo = new FakeIoService(Latency);
        var concurrentIo = new FakeIoService(Latency);

        var sequentialMs = await AsyncPatterns.TimeAsync(() => AsyncPatterns.SequentialAsync(sequentialIo, 5));
        var concurrentMs = await AsyncPatterns.TimeAsync(() => AsyncPatterns.ConcurrentAsync(concurrentIo, 5));

        Console.WriteLine($"5 independent 50ms calls:");
        Console.WriteLine($"  awaited one by one : {sequentialMs,4} ms   <- the durations add up");
        Console.WriteLine($"  started then WhenAll: {concurrentMs,4} ms   <- all in flight at once");

        // --- thread cost: blocking vs truly async ---
        var blockingIo = new FakeIoService(Latency);
        var asyncIo = new FakeIoService(Latency);

        var blockingMs = await AsyncPatterns.TimeAsync(() => AsyncPatterns.BlockingOnPoolThreadsAsync(blockingIo, Calls));
        var asyncMs = await AsyncPatterns.TimeAsync(() => AsyncPatterns.TrulyAsync(asyncIo, Calls));

        Console.WriteLine();
        Console.WriteLine($"{Calls} concurrent calls — how many pool threads did each style consume?");
        Console.WriteLine($"  Task.Run + Thread.Sleep : {blockingIo.DistinctThreadsUsed,2} threads, {blockingMs,4} ms  <- async over sync");
        Console.WriteLine($"  real async I/O          : {asyncIo.DistinctThreadsUsed,2} threads, {asyncMs,4} ms  <- no thread waits");
        Console.WriteLine("  -> same work, but blocking ties up a thread per call; async does not");

        // --- the thread can change across an await ---
        var (before, after) = await AsyncPatterns.ThreadAcrossAwaitAsync();
        Console.WriteLine();
        Console.WriteLine($"Thread before await: #{before}, after await: #{after} " +
                          $"({(before == after ? "same this time — not guaranteed" : "different — perfectly normal")})");
    }
}
