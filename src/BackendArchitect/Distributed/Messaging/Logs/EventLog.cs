namespace BackendArchitect.Distributed.Messaging.Logs;

public sealed record LogRecord(int Partition, long Offset, string Key, string Body, long AppendedAtTick);

// A log is a diary: reading does not consume. Nothing is removed on read, so several consumer groups
// read the same records independently, each keeping its own position.
//
//   partition 0: [ A ][ D ][ G ]      ordered WITHIN a partition, and nowhere else
//   partition 1: [ B ][ E ][ H ]
//   partition 2: [ C ][ F ][ I ]
//                  0    1    2        <- offsets, per partition
//
// An offset is ONE NUMBER per (group, partition) meaning "everything before here is done". That single
// fact explains the whole model: why a partition belongs to exactly one consumer in a group, why a
// poison message blocks the partition rather than itself, and why commit ORDER decides the delivery
// guarantee.
public sealed class EventLog
{
    private readonly List<LogRecord>[] _partitions;
    private readonly long _retentionTicks;

    private long _tick;

    public EventLog(int partitionCount, long retentionTicks = long.MaxValue)
    {
        _partitions = [.. Enumerable.Range(0, partitionCount).Select(_ => new List<LogRecord>())];
        _retentionTicks = retentionTicks;
    }

    public int PartitionCount => _partitions.Length;

    public void Append(string key, string body)
    {
        var partition = PartitionFor(key);
        var offset = _partitions[partition].Count == 0 ? 0 : _partitions[partition][^1].Offset + 1;
        _partitions[partition].Add(new LogRecord(partition, offset, key, body, _tick));
    }

    /// <summary>Advancing time is what makes retention delete things, read or not.</summary>
    public void Advance(long ticks)
    {
        _tick += ticks;
        if (_retentionTicks == long.MaxValue)
            return;

        foreach (var partition in _partitions)
            partition.RemoveAll(record => _tick - record.AppendedAtTick > _retentionTicks);
    }

    public IReadOnlyList<LogRecord> Read(int partition, long fromOffset) =>
        [.. _partitions[partition].Where(record => record.Offset >= fromOffset)];

    public long LatestOffset(int partition) =>
        _partitions[partition].Count == 0 ? 0 : _partitions[partition][^1].Offset + 1;

    /// <summary>The oldest surviving offset. Above a consumer's position, it has lost data.</summary>
    public long EarliestOffset(int partition) =>
        _partitions[partition].Count == 0 ? LatestOffset(partition) : _partitions[partition][0].Offset;

    public int RecordCount => _partitions.Sum(partition => partition.Count);

    // FNV-1a: stable across processes and restarts, unlike string.GetHashCode() which .NET randomizes
    // per process. Same requirement as a Cosmos partition key and a pipeline lane.
    private int PartitionFor(string key)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var c in key)
        {
            hash ^= c;
            hash *= prime;
        }

        return (int)(hash % (uint)_partitions.Length);
    }
}
