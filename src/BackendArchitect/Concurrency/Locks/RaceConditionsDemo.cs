namespace BackendArchitect.Concurrency.Locks;

// Example runner: lost updates, the cost of each fix, check-then-act on a thread-safe collection,
// and a deadlock caused purely by lock ordering.
public class RaceConditionsDemo
{
    public void Run()
    {
        const int threads = 8;
        const int perThread = 200_000;

        // --- 1. count++ is not atomic ---
        var unsynchronized = Counter.Unsynchronized(threads, perThread);
        var locked = Counter.WithLock(threads, perThread);
        var atomic = Counter.WithInterlocked(threads, perThread);

        Console.WriteLine($"{threads} threads x {perThread:N0} increments — expected {unsynchronized.Expected:N0}:");
        foreach (var run in new[] { unsynchronized, locked, atomic })
            Console.WriteLine($"  {run.Strategy,-19}: {run.Actual,9:N0}  {(run.Correct ? "correct" : $"LOST {run.LostUpdates:N0}")}  ({run.ElapsedMs} ms)");
        Console.WriteLine("  -> a lost update is permanent; both fixes are correct, Interlocked is the cheap one");

        // --- 2. thread-safe collection, unsafe logic ---
        var cost = TimeSpan.FromMilliseconds(20);
        Console.WriteLine();
        Console.WriteLine($"{threads} threads asking one ConcurrentDictionary for the same session:");
        foreach (var strategy in Enum.GetValues<CacheStrategy>())
        {
            var race = SessionCache.Race(strategy, threads, cost);
            Console.WriteLine($"  {strategy,-13}: constructed {race.SessionsConstructed}, callers got {race.DistinctSessionsReturned} distinct session(s)");
        }
        Console.WriteLine("  -> every caller gets the same session, but the FACTORY ran several times — orphaned work");

        // --- 3. deadlock is an ordering bug, not a locking bug ---
        var unordered = AccountTransfer.RunOpposingTransfers(orderLocks: false);
        var ordered = AccountTransfer.RunOpposingTransfers(orderLocks: true);

        Console.WriteLine();
        Console.WriteLine("Transfer(alice→bob) and Transfer(bob→alice) at the same instant:");
        Console.WriteLine($"  lock (from) then lock (to)      : {unordered.Completed} completed, {unordered.Deadlocked} deadlocked, £{unordered.TotalMoney:N0} still in the bank");
        Console.WriteLine($"  lock in ascending account id    : {ordered.Completed} completed, {ordered.Deadlocked} deadlocked, £{ordered.TotalMoney:N0} still in the bank");
        Console.WriteLine("  -> consistent means consistent in the OBJECTS, not the parameter names");
    }
}
