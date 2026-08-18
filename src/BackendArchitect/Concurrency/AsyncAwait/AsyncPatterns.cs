using System.Diagnostics;

namespace BackendArchitect.Concurrency.AsyncAwait;

// The patterns and anti-patterns of async/await, written so each one can be measured.
public static class AsyncPatterns
{
    // ---------- sequential vs concurrent ----------

    /// <summary>❌ Awaits each call before starting the next: the durations ADD UP.</summary>
    public static async Task<string[]> SequentialAsync(FakeIoService io, int calls)
    {
        var results = new string[calls];
        for (var i = 0; i < calls; i++)
            results[i] = await io.AsyncCall($"request-{i}");   // waits before starting the next

        return results;
    }

    /// <summary>
    /// ✅ Starts every call first, then awaits them together. Calling an async method STARTS it;
    /// `await` is only where you stop and wait.
    /// </summary>
    public static async Task<string[]> ConcurrentAsync(FakeIoService io, int calls)
    {
        var tasks = new Task<string>[calls];
        for (var i = 0; i < calls; i++)
            tasks[i] = io.AsyncCall($"request-{i}");           // started, not awaited

        return await Task.WhenAll(tasks);
    }

    // ---------- how many threads each style costs ----------

    /// <summary>
    /// ❌ "async over sync": Task.Run does not remove the blocking, it RELOCATES it onto a pool
    /// thread — so a thread is still wasted per call, plus queueing overhead.
    /// </summary>
    public static Task<string[]> BlockingOnPoolThreadsAsync(FakeIoService io, int calls) =>
        Task.WhenAll(Enumerable.Range(0, calls)
            .Select(i => Task.Run(() => io.BlockingCall($"request-{i}"))));

    /// <summary>✅ Real async I/O: no thread waits, so a handful of threads serve many calls.</summary>
    public static Task<string[]> TrulyAsync(FakeIoService io, int calls) =>
        Task.WhenAll(Enumerable.Range(0, calls).Select(i => io.AsyncCall($"request-{i}")));

    // ---------- measurement helper ----------

    public static async Task<long> TimeAsync(Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Shows that execution can resume on a DIFFERENT thread after an await — which is why anything
    /// thread-affine (ThreadStatic, thread-name logging) is unsafe across one.
    /// </summary>
    public static async Task<(int Before, int After)> ThreadAcrossAwaitAsync()
    {
        var before = Environment.CurrentManagedThreadId;
        await Task.Delay(20).ConfigureAwait(false);
        return (before, Environment.CurrentManagedThreadId);
    }
}
