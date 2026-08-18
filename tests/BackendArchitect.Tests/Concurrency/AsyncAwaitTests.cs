using BackendArchitect.Concurrency.AsyncAwait;

namespace BackendArchitect.Tests.Concurrency;

// Concurrency · async/await — what async actually buys: fewer threads held, and overlapping I/O.
// Timing assertions use generous margins; the thread-count assertions are the robust ones.
public class AsyncAwaitTests
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task StartingCallsBeforeAwaiting_IsFasterThanAwaitingOneByOne()
    {
        const int calls = 5;
        var sequentialIo = new FakeIoService(Latency);
        var concurrentIo = new FakeIoService(Latency);

        var sequentialMs = await AsyncPatterns.TimeAsync(() => AsyncPatterns.SequentialAsync(sequentialIo, calls));
        var concurrentMs = await AsyncPatterns.TimeAsync(() => AsyncPatterns.ConcurrentAsync(concurrentIo, calls));

        Assert.True(concurrentMs < sequentialMs,
            $"concurrent ({concurrentMs}ms) should beat sequential ({sequentialMs}ms)");
    }

    [Fact]
    public async Task Sequential_TakesRoughlyTheSumOfTheLatencies()
    {
        const int calls = 4;
        var io = new FakeIoService(Latency);

        var elapsed = await AsyncPatterns.TimeAsync(() => AsyncPatterns.SequentialAsync(io, calls));

        Assert.True(elapsed >= calls * Latency.TotalMilliseconds * 0.8,
            $"4 x 50ms awaited one by one should take ~200ms; took {elapsed}ms");
    }

    [Fact]
    public async Task BothStyles_ProduceTheSameResults()
    {
        var sequential = await AsyncPatterns.SequentialAsync(new FakeIoService(Latency), 3);
        var concurrent = await AsyncPatterns.ConcurrentAsync(new FakeIoService(Latency), 3);

        Assert.Equal(sequential, concurrent);   // WhenAll preserves the order of the task array
    }

    [Fact]
    public async Task RealAsyncIo_ServesManyCalls_WithFarFewerThreadsThanCalls()
    {
        const int calls = 20;
        var io = new FakeIoService(Latency);

        await AsyncPatterns.TrulyAsync(io, calls);

        Assert.Equal(calls, io.CallsMade);
        Assert.True(io.DistinctThreadsUsed < calls,
            $"async should not need a thread per call; used {io.DistinctThreadsUsed} for {calls}");
    }

    [Fact]
    public async Task BlockingOnPoolThreads_ConsumesMoreThreadsThanRealAsync()
    {
        const int calls = 20;
        var blockingIo = new FakeIoService(Latency);
        var asyncIo = new FakeIoService(Latency);

        await AsyncPatterns.BlockingOnPoolThreadsAsync(blockingIo, calls);
        await AsyncPatterns.TrulyAsync(asyncIo, calls);

        Assert.True(blockingIo.DistinctThreadsUsed > asyncIo.DistinctThreadsUsed,
            $"async over sync ({blockingIo.DistinctThreadsUsed} threads) must cost more than " +
            $"real async ({asyncIo.DistinctThreadsUsed})");
    }

    [Fact]
    public async Task AwaitingDoesNotGuaranteeTheSameThreadAfterwards()
    {
        var (before, after) = await AsyncPatterns.ThreadAcrossAwaitAsync();

        // Both outcomes are legal — the point is that you must not DEPEND on them matching.
        Assert.True(before > 0 && after > 0);
    }

    [Fact]
    public async Task ACancellationToken_StopsAsyncWorkPromptly()
    {
        var io = new FakeIoService(TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();

        var call = io.AsyncCall("slow", cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => call);
    }
}
