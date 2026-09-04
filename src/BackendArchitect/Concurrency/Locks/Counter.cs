using System.Diagnostics;

namespace BackendArchitect.Concurrency.Locks;

public sealed record CounterRun(string Strategy, int Expected, int Actual, long ElapsedMs)
{
    public int LostUpdates => Expected - Actual;
    public bool Correct => Actual == Expected;
}

// The smallest possible race: count++ from many threads at once.
//
// count++ is NOT one operation. The CPU does three:
//     1. READ   count into a register
//     2. MODIFY add 1
//     3. WRITE  back to memory
// Two threads that READ the same value both WRITE the same result, so one increment vanishes.
// A lost update is permanent - nothing comes back for it later.
public static class Counter
{
    // A field on a heap object, so it can be passed by ref to Interlocked.
    private sealed class Box { public int Value; }

    /// <summary>No synchronization: correct-looking code, wrong answer, different every run.</summary>
    public static CounterRun Unsynchronized(int threadCount, int incrementsPerThread)
    {
        var box = new Box();
        var elapsed = RunTogether(threadCount, () =>
        {
            for (var i = 0; i < incrementsPerThread; i++)
                box.Value++;                                  // read -> modify -> write, unprotected
        });

        return new CounterRun("no synchronization", threadCount * incrementsPerThread, box.Value, elapsed);
    }

    /// <summary>Correct, but every thread that loses the race is parked by the OS and woken later.</summary>
    public static CounterRun WithLock(int threadCount, int incrementsPerThread)
    {
        var box = new Box();
        var gate = new Lock();                                // .NET 9+; before that, a plain object
        var elapsed = RunTogether(threadCount, () =>
        {
            for (var i = 0; i < incrementsPerThread; i++)
                lock (gate)
                    box.Value++;
        });

        return new CounterRun("lock", threadCount * incrementsPerThread, box.Value, elapsed);
    }

    /// <summary>Correct AND cheap: one atomic CPU instruction, no blocking, no context switch.</summary>
    public static CounterRun WithInterlocked(int threadCount, int incrementsPerThread)
    {
        var box = new Box();
        var elapsed = RunTogether(threadCount, () =>
        {
            for (var i = 0; i < incrementsPerThread; i++)
                Interlocked.Increment(ref box.Value);
        });

        return new CounterRun("Interlocked", threadCount * incrementsPerThread, box.Value, elapsed);
    }

    // Real threads plus a Barrier, so every thread starts hammering at the same instant. Task.Run would
    // let the pool stagger them, which quietly hides the race.
    private static long RunTogether(int threadCount, Action work)
    {
        var startLine = new Barrier(threadCount);
        var threads = new Thread[threadCount];
        var clock = Stopwatch.StartNew();

        for (var t = 0; t < threadCount; t++)
        {
            threads[t] = new Thread(() =>
            {
                startLine.SignalAndWait();
                work();
            });
            threads[t].Start();
        }

        foreach (var thread in threads)
            thread.Join();

        return clock.ElapsedMilliseconds;
    }
}
