using System.Collections.Concurrent;

namespace BackendArchitect.Concurrency.Locks;

public sealed record Session(string UserId, int SerialNumber);

public enum CacheStrategy
{
    CheckThenAct,      // thread-safe collection, unsafe logic
    GetOrAdd,          // atomic result, factory may still run twice
    LazyGetOrAdd       // atomic result AND the value constructed exactly once
}

public sealed record CacheRace(CacheStrategy Strategy, int Threads, int SessionsConstructed, int DistinctSessionsReturned);

// ConcurrentDictionary is thread-safe. That guarantees ITS OWN internal state - not the logic wrapped
// around it. ContainsKey-then-TryAdd is still check-then-act: every thread sees "missing" in the gap.
//
// The damage is not a crash or a corrupt dictionary. It is that the FACTORY ran several times - and if
// constructing the value opens a connection or reserves a licence seat, the losers are orphaned.
public sealed class SessionCache
{
    private readonly ConcurrentDictionary<string, Session> _checkThenAct = new();
    private readonly ConcurrentDictionary<string, Session> _getOrAdd = new();
    private readonly ConcurrentDictionary<string, Lazy<Session>> _lazy = new();
    private readonly TimeSpan _constructionCost;
    private int _constructed;

    public SessionCache(TimeSpan constructionCost) => _constructionCost = constructionCost;

    public int SessionsConstructed => Volatile.Read(ref _constructed);

    private Session Construct(string userId)
    {
        var serial = Interlocked.Increment(ref _constructed);
        Thread.Sleep(_constructionCost);                 // opening a connection, reserving a seat...
        return new Session(userId, serial);
    }

    /// <summary>Broken: two atomic calls with a gap between them are not one atomic operation.</summary>
    public Session GetOrCreateCheckThenAct(string userId)
    {
        if (!_checkThenAct.ContainsKey(userId))          // CHECK
            _checkThenAct.TryAdd(userId, Construct(userId));   // ACT - by now someone else may have won
        return _checkThenAct[userId];
    }

    /// <summary>
    /// One atomic operation: every caller gets the SAME session. The factory can still run more than
    /// once under contention - only one result is stored.
    /// </summary>
    public Session GetOrCreate(string userId) => _getOrAdd.GetOrAdd(userId, Construct);

    /// <summary>
    /// Lazy defers the work to .Value, and only the winning Lazy is ever evaluated - so an expensive or
    /// side-effecting constructor runs exactly once.
    /// </summary>
    public Session GetOrCreateLazily(string userId) =>
        _lazy.GetOrAdd(userId, id => new Lazy<Session>(() => Construct(id))).Value;

    /// <summary>Fires every thread at the same key simultaneously and reports what each strategy cost.</summary>
    public static CacheRace Race(CacheStrategy strategy, int threadCount, TimeSpan constructionCost)
    {
        var cache = new SessionCache(constructionCost);
        Func<string, Session> resolve = strategy switch
        {
            CacheStrategy.CheckThenAct => cache.GetOrCreateCheckThenAct,
            CacheStrategy.GetOrAdd => cache.GetOrCreate,
            _ => cache.GetOrCreateLazily
        };

        var startLine = new Barrier(threadCount);
        var returned = new Session[threadCount];
        var threads = new Thread[threadCount];

        for (var t = 0; t < threadCount; t++)
        {
            var slot = t;
            threads[slot] = new Thread(() =>
            {
                startLine.SignalAndWait();
                returned[slot] = resolve("user-1");
            });
            threads[slot].Start();
        }

        foreach (var thread in threads)
            thread.Join();

        return new CacheRace(strategy, threadCount, cache.SessionsConstructed, returned.Distinct().Count());
    }
}
