namespace BackendArchitect.Databases.Cosmos.PartitionKeys;

// One JSON document, as it would live in a Cosmos container.
public sealed record OrderDoc(string Id, string CustomerId, string Status, string Product);

// A simplified model of a Cosmos container, to make partition keys concrete (an analogy, not the real
// engine). It reproduces the two things that actually matter:
//
//   * LOGICAL partition  — all documents sharing one partition-key VALUE. Cosmos limits each to 20 GB,
//                          so a key whose values grow without bound is a broken design.
//   * PHYSICAL partition — a machine. Cosmos HASHES the partition-key value to choose one, and many
//                          logical partitions share a physical one.
//
// We count "partitions touched" as the cost signal — the Cosmos equivalent of logical reads: a point
// read or single-partition query touches ONE, a cross-partition query fans out to ALL of them.
public sealed class PartitionedContainer
{
    private readonly int _physicalCount;
    private readonly Func<OrderDoc, string> _partitionKey;
    private readonly List<OrderDoc>[] _physical;
    private readonly Dictionary<string, int> _logicalSizes = new(StringComparer.Ordinal);
    private int _itemCount;

    public PartitionedContainer(int physicalPartitions, Func<OrderDoc, string> partitionKeySelector)
    {
        _physicalCount = physicalPartitions;
        _partitionKey = partitionKeySelector;
        _physical = Enumerable.Range(0, physicalPartitions).Select(_ => new List<OrderDoc>()).ToArray();
    }

    /// <summary>How many partitions the last operations had to look in — the cost signal.</summary>
    public int PartitionsTouched { get; private set; }

    public void ResetCost() => PartitionsTouched = 0;

    public void Add(OrderDoc doc)
    {
        var key = _partitionKey(doc);
        _physical[PhysicalIndexOf(key)].Add(doc);
        _logicalSizes[key] = _logicalSizes.GetValueOrDefault(key) + 1;
        _itemCount++;
    }

    // Cosmos hashes the partition-key VALUE to pick a machine. We use a stable hash (FNV-1a) because
    // .NET's string.GetHashCode() is randomised per process, which would make results non-repeatable.
    private int PhysicalIndexOf(string partitionKey) =>
        (int)(StableHash(partitionKey) % (uint)_physicalCount);

    private static uint StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    // ✅ POINT READ — partition key + id = the full address. Touches exactly one partition, returns
    // exactly one document (or null).
    public OrderDoc? PointRead(string partitionKey, string id)
    {
        PartitionsTouched++;
        return _physical[PhysicalIndexOf(partitionKey)]
            .FirstOrDefault(d => _partitionKey(d) == partitionKey && d.Id == id);
    }

    // ✅ SINGLE-PARTITION QUERY — you know the partition key but not the id. Touches one partition,
    // returns a LIST.
    public IReadOnlyList<OrderDoc> QueryWithinPartition(string partitionKey)
    {
        PartitionsTouched++;
        return _physical[PhysicalIndexOf(partitionKey)]
            .Where(d => _partitionKey(d) == partitionKey)
            .ToList();
    }

    // ❌ CROSS-PARTITION QUERY — the filter isn't the partition key, so every machine must be asked.
    public IReadOnlyList<OrderDoc> CrossPartitionQuery(Func<OrderDoc, bool> predicate)
    {
        PartitionsTouched += _physicalCount;
        return _physical.SelectMany(p => p).Where(predicate).ToList();
    }

    // --- distribution stats: how well did this partition key spread the data? ---

    public IReadOnlyList<int> ItemsPerPhysicalPartition => _physical.Select(p => p.Count).ToList();

    public int DistinctLogicalPartitions => _logicalSizes.Count;

    public int LargestLogicalPartition => _logicalSizes.Count == 0 ? 0 : _logicalSizes.Values.Max();

    /// <summary>Share of all data sitting in the single biggest logical partition. Near 1.0 = hot.</summary>
    public double LargestLogicalShare =>
        _itemCount == 0 ? 0 : (double)LargestLogicalPartition / _itemCount;
}
