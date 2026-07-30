using BackendArchitect.Databases.Cosmos.RequestUnits;

namespace BackendArchitect.Databases.Cosmos.Indexing;

// A simplified Cosmos indexing policy: which property paths are indexed.
//
// The default in Cosmos is "/*" — EVERY property indexed. That's the opposite of SQL Server, where you
// index nothing until you opt in. So the Cosmos question is not "what should I add?" but
// "what can I remove?".
public sealed class IndexPolicy
{
    private readonly HashSet<string> _indexedPaths;

    private IndexPolicy(string name, IEnumerable<string> indexedPaths)
    {
        Name = name;
        _indexedPaths = new HashSet<string>(indexedPaths, StringComparer.Ordinal);
    }

    public string Name { get; }

    public int IndexedPropertyCount => _indexedPaths.Count;

    public bool IsIndexed(string path) => _indexedPaths.Contains(path);

    /// <summary>The Cosmos default: index every property ("/*"). Fast reads, expensive writes.</summary>
    public static IndexPolicy IndexEverything(IEnumerable<string> allPaths) =>
        new("index everything (default)", allPaths);

    /// <summary>Index nothing. Cheapest writes, but every filter becomes a full scan.</summary>
    public static IndexPolicy IndexNothing() =>
        new("index nothing", []);

    /// <summary>Index exactly the paths you filter/sort on — and nothing else.</summary>
    public static IndexPolicy RightSized(params string[] queriedPaths) =>
        new("right-sized", queriedPaths);
}

// Prices a workload under a given indexing policy, so the trade can be seen:
//   * every indexed property is maintained on EVERY write  -> more indexes = costlier writes
//   * a filter on an UNINDEXED path falls back to a full scan -> fewer indexes = costlier reads
public sealed class WorkloadCost
{
    private readonly double _documentKb;
    private readonly int _documentsInPartition;

    public WorkloadCost(double documentKb, int documentsInPartition)
    {
        _documentKb = documentKb;
        _documentsInPartition = documentsInPartition;
    }

    public double WriteCost(IndexPolicy policy) =>
        RuCost.Write(_documentKb, policy.IndexedPropertyCount);

    /// <summary>An indexed filter examines only what it matches; an unindexed one scans the partition.</summary>
    public double QueryCost(IndexPolicy policy, string filterPath, int matchingDocuments)
    {
        var examined = policy.IsIndexed(filterPath) ? matchingDocuments : _documentsInPartition;
        return RuCost.Query(examined, partitionsTouched: 1);
    }

    public double TotalCost(IndexPolicy policy, int writes, int queries, string filterPath, int matchingDocuments) =>
        writes * WriteCost(policy) + queries * QueryCost(policy, filterPath, matchingDocuments);
}
