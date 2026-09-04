using BackendArchitect.Concurrency.Locks;

namespace BackendArchitect.Tests.Concurrency;

public class LocksTests
{
    private const int Threads = 8;
    private const int PerThread = 200_000;
    private static readonly TimeSpan ConstructionCost = TimeSpan.FromMilliseconds(20);

    [Fact]
    public void Unsynchronized_increments_lose_updates()
    {
        // A race is by definition non-deterministic, so allow a few attempts before calling it green.
        var lost = 0;
        for (var attempt = 0; attempt < 5 && lost == 0; attempt++)
            lost = Counter.Unsynchronized(Threads, PerThread).LostUpdates;

        Assert.True(lost > 0, "count++ from 8 threads should lose updates");
    }

    [Fact]
    public void Unsynchronized_increments_never_overcount()
    {
        var run = Counter.Unsynchronized(Threads, PerThread);

        Assert.True(run.Actual <= run.Expected);
    }

    [Fact]
    public void Lock_makes_the_counter_exact()
    {
        var run = Counter.WithLock(Threads, PerThread);

        Assert.Equal(run.Expected, run.Actual);
    }

    [Fact]
    public void Interlocked_makes_the_counter_exact()
    {
        var run = Counter.WithInterlocked(Threads, PerThread);

        Assert.Equal(run.Expected, run.Actual);
    }

    [Fact]
    public void Check_then_act_constructs_the_session_more_than_once()
    {
        var race = SessionCache.Race(CacheStrategy.CheckThenAct, Threads, ConstructionCost);

        Assert.True(race.SessionsConstructed > 1, "every thread saw 'missing' in the gap");
    }

    [Fact]
    public void Check_then_act_still_hands_every_caller_the_same_session()
    {
        var race = SessionCache.Race(CacheStrategy.CheckThenAct, Threads, ConstructionCost);

        // The dictionary is fine - which is exactly why the bug is invisible from the outside.
        Assert.Equal(1, race.DistinctSessionsReturned);
    }

    [Fact]
    public void GetOrAdd_hands_every_caller_the_same_session()
    {
        var race = SessionCache.Race(CacheStrategy.GetOrAdd, Threads, ConstructionCost);

        Assert.Equal(1, race.DistinctSessionsReturned);
    }

    [Fact]
    public void Lazy_get_or_add_constructs_exactly_once()
    {
        var race = SessionCache.Race(CacheStrategy.LazyGetOrAdd, Threads, ConstructionCost);

        Assert.Equal(1, race.SessionsConstructed);
        Assert.Equal(1, race.DistinctSessionsReturned);
    }

    [Fact]
    public void Opposing_transfers_deadlock_when_the_lock_order_differs()
    {
        var report = AccountTransfer.RunOpposingTransfers(orderLocks: false);

        Assert.Equal(2, report.Deadlocked);
        Assert.Equal(0, report.Completed);
    }

    [Fact]
    public void Consistent_lock_order_lets_both_transfers_through()
    {
        var report = AccountTransfer.RunOpposingTransfers(orderLocks: true);

        Assert.Equal(2, report.Completed);
        Assert.Equal(0, report.Deadlocked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_money_is_lost_either_way(bool orderLocks)
    {
        // A deadlock costs availability, not correctness - the balances are still consistent.
        var report = AccountTransfer.RunOpposingTransfers(orderLocks);

        Assert.Equal(2_000m, report.TotalMoney);
    }
}
