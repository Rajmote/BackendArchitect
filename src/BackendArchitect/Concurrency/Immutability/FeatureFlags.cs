using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;

namespace BackendArchitect.Concurrency.Immutability;

public interface IFeatureFlags
{
    bool IsEnabled(string flag);
    void Set(string flag, bool value);
}

// Read on every request, written a handful of times a day. The classic copy-on-write shape.

/// <summary>§4.3 thinking: guard the mutable map. Correct - and every read serialises on one lock.</summary>
public sealed class LockedFeatureFlags : IFeatureFlags
{
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Lock _gate = new();

    public bool IsEnabled(string flag)
    {
        lock (_gate)
            return _flags.TryGetValue(flag, out var enabled) && enabled;
    }

    public void Set(string flag, bool value)
    {
        lock (_gate)
            _flags[flag] = value;
    }
}

/// <summary>
/// §4.4 thinking: readers take NO lock. They grab the current reference and read a snapshot that can
/// never change; a writer publishing a new version cannot disturb a read already in flight.
/// </summary>
public sealed class CopyOnWriteFeatureFlags : IFeatureFlags
{
    private ImmutableDictionary<string, bool> _flags = ImmutableDictionary<string, bool>.Empty;

    public bool IsEnabled(string flag) =>
        Volatile.Read(ref _flags).TryGetValue(flag, out var enabled) && enabled;

    public void Set(string flag, bool value) =>
        ImmutableInterlocked.Update(ref _flags, current => current.SetItem(flag, value));
}

/// <summary>
/// Built once, read millions of times: FrozenDictionary spends real time on construction to buy the
/// fastest possible lookup. Writers serialise and rebuild; readers still never block.
/// </summary>
public sealed class FrozenFeatureFlags : IFeatureFlags
{
    private FrozenDictionary<string, bool> _flags = FrozenDictionary<string, bool>.Empty;
    private readonly Lock _writeGate = new();

    public bool IsEnabled(string flag) =>
        Volatile.Read(ref _flags).TryGetValue(flag, out var enabled) && enabled;

    public void Set(string flag, bool value)
    {
        lock (_writeGate)                       // writers only - readers never touch this
        {
            var next = Volatile.Read(ref _flags).ToDictionary(pair => pair.Key, pair => pair.Value);
            next[flag] = value;
            Volatile.Write(ref _flags, next.ToFrozenDictionary());
        }
    }
}

public sealed record FlagBenchmark(string Strategy, long ReaderElapsedMs, long ReadsServed, long EnabledObserved);

public static class FeatureFlagBenchmark
{
    /// <summary>
    /// Hammers one flag store with many readers while a single writer keeps publishing updates, and
    /// reports how long the readers took. Correctness is the point; the timing is the lesson.
    /// </summary>
    public static FlagBenchmark Measure(IFeatureFlags flags, string strategy, int readerCount, int readsPerReader, int writes)
    {
        for (var i = 0; i < 20; i++)
            flags.Set($"flag-{i}", i % 2 == 0);

        var startLine = new Barrier(readerCount + 1);
        var stopWriting = new CancellationTokenSource();
        var enabledSeen = 0L;

        var readers = new Thread[readerCount];
        for (var r = 0; r < readerCount; r++)
        {
            readers[r] = new Thread(() =>
            {
                startLine.SignalAndWait();
                var seen = 0L;
                for (var i = 0; i < readsPerReader; i++)
                    if (flags.IsEnabled($"flag-{i % 20}"))
                        seen++;
                Interlocked.Add(ref enabledSeen, seen);
            });
            readers[r].Start();
        }

        var writer = new Thread(() =>
        {
            for (var w = 0; w < writes && !stopWriting.IsCancellationRequested; w++)
            {
                flags.Set($"flag-{w % 20}", w % 3 == 0);
                Thread.Sleep(1);
            }
        });

        startLine.SignalAndWait();
        var clock = Stopwatch.StartNew();
        writer.Start();

        foreach (var reader in readers)
            reader.Join();

        var elapsed = clock.ElapsedMilliseconds;
        stopWriting.Cancel();
        writer.Join();

        // Every read returned a real answer and nothing threw - the flag store stayed consistent while
        // being rewritten underneath it.
        return new FlagBenchmark(strategy, elapsed, (long)readerCount * readsPerReader, Volatile.Read(ref enabledSeen));
    }
}
