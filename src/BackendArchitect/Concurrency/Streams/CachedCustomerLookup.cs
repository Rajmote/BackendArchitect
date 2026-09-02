namespace BackendArchitect.Concurrency.Streams;

public sealed record CustomerRecord(int Id, string Name);

// ValueTask<T>: for a method whose answer is USUALLY already available.
//
// A Task<T> is a heap allocation on every call. When 95% of calls are cache hits, that's a million
// allocations a second wrapping answers you already had. A ValueTask returning a ready value allocates
// nothing.
//
// ⚠️ The rules (a ValueTask may be backed by a POOLED object that gets recycled):
//    * await it exactly ONCE - awaiting twice can hand you someone else's result
//    * no .Result / .Wait(), no Task.WhenAll, don't store it - call .AsTask() first if you need those
// Default to Task; use ValueTask only on a hot path where you have MEASURED the allocations.
public sealed class CachedCustomerLookup
{
    private readonly Dictionary<int, CustomerRecord> _cache = new();
    private readonly TimeSpan _databaseLatency;

    public CachedCustomerLookup(TimeSpan databaseLatency) => _databaseLatency = databaseLatency;

    /// <summary>Calls answered from cache, i.e. completed synchronously with no allocation.</summary>
    public int SynchronousCompletions { get; private set; }

    /// <summary>Calls that had to go to the database and therefore really were asynchronous.</summary>
    public int AsynchronousCompletions { get; private set; }

    public ValueTask<CustomerRecord> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(id, out var cached))
        {
            SynchronousCompletions++;
            return new ValueTask<CustomerRecord>(cached);   // already complete: nothing allocated
        }

        AsynchronousCompletions++;
        return new ValueTask<CustomerRecord>(FetchAsync(id, cancellationToken));
    }

    private async Task<CustomerRecord> FetchAsync(int id, CancellationToken cancellationToken)
    {
        await Task.Delay(_databaseLatency, cancellationToken);
        var record = new CustomerRecord(id, $"Customer{id}");
        _cache[id] = record;
        return record;
    }
}
