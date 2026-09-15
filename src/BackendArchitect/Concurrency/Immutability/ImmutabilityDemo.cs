namespace BackendArchitect.Concurrency.Immutability;

// Example runner: five things that look immutable and aren't, then copy-on-write replacing the lock.
public class ImmutabilityDemo
{
    public void Run()
    {
        var traps = new[]
        {
            MutabilityTraps.ReadonlyFieldIsNotImmutable(),
            MutabilityTraps.RecordWithMutableListIsNotImmutable(),
            MutabilityTraps.ReadOnlyViewIsNotASnapshot(),
            MutabilityTraps.ReadOnlyViewCanBeCastBack(),
            MutabilityTraps.WithIsAShallowCopy(),
            MutabilityTraps.DeepImmutabilityHoldsTheLine()
        };

        Console.WriteLine("Does the 'immutable' value survive an attempt to change it?");
        foreach (var trap in traps)
            Console.WriteLine($"  {trap.Trap,-28}: {trap.Before,-22} -> {trap.After,-32} {(trap.Leaked ? "LEAKED" : "held")}");
        Console.WriteLine("  -> readonly/init/record/with all protect the ARROW; only the target's type protects the target");

        // Value equality is what makes a record a good key - and what makes a mutable one dangerous.
        var movedKey = MutabilityTraps.MutableKeyIsLostInADictionary();
        var listKey = MutabilityTraps.ListPropertyDoesNotMoveTheKey();

        Console.WriteLine();
        Console.WriteLine("A record used as a dictionary key, then mutated:");
        Console.WriteLine($"  {movedKey.Trap,-28}: {movedKey.Before} -> {movedKey.After}  {(movedKey.Leaked ? "ENTRY LOST" : "still reachable")}");
        Console.WriteLine($"  {listKey.Trap,-28}: {listKey.Before} -> {listKey.After}  {(listKey.Leaked ? "ENTRY LOST" : "still reachable")}");
        Console.WriteLine("  -> a record hashes its fields with EqualityComparer<T>.Default — reference-based for List<T>");

        // --- copy-on-write vs the lock ---
        const int readers = 8;
        const int readsEach = 500_000;
        const int writes = 200;

        var measurements = new[]
        {
            FeatureFlagBenchmark.Measure(new LockedFeatureFlags(), "lock + Dictionary", readers, readsEach, writes),
            FeatureFlagBenchmark.Measure(new CopyOnWriteFeatureFlags(), "ImmutableDictionary", readers, readsEach, writes),
            FeatureFlagBenchmark.Measure(new FrozenFeatureFlags(), "FrozenDictionary", readers, readsEach, writes)
        };

        Console.WriteLine();
        Console.WriteLine($"{readers} reader threads x {readsEach:N0} reads, while one writer publishes {writes} updates:");
        foreach (var run in measurements)
            Console.WriteLine($"  {run.Strategy,-20}: {run.ReaderElapsedMs,5} ms for {run.ReadsServed:N0} reads");
        Console.WriteLine("  -> the locked readers queue behind each other; the immutable readers never block at all");
    }
}
