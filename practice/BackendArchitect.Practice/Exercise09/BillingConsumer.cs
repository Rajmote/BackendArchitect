namespace BackendArchitect.Practice.Exercise09;

public sealed record LogRecord(int Partition, long Offset, string Key, string Body);

public sealed record ConsumeReport(int Processed, int DeadLettered, int DuplicatesSuppressed, bool PartitionBlocked);

/// <summary>A minimal append-only log: reading does not consume, and offsets are per partition.</summary>
public sealed class OrderLog
{
    private readonly List<LogRecord>[] _partitions;

    public OrderLog(int partitionCount) =>
        _partitions = [.. Enumerable.Range(0, partitionCount).Select(_ => new List<LogRecord>())];

    public int PartitionCount => _partitions.Length;

    public void Append(int partition, string key, string body) =>
        _partitions[partition].Add(new LogRecord(partition, _partitions[partition].Count, key, body));

    public IReadOnlyList<LogRecord> Read(int partition, long fromOffset) =>
        [.. _partitions[partition].Where(record => record.Offset >= fromOffset)];

    public long LatestOffset(int partition) => _partitions[partition].Count;
}

// Exercise 09 — see practice/Exercise09-Messaging.md.
//
// The billing consumer for an `orders` topic. A log gives you no DeliveryCount, no automatic parking
// and no dead-letter queue, so everything a queue did for free you build here:
//
//   * process THEN commit, so a crash redelivers rather than loses
//   * suppress duplicates, because redelivery is normal (crashes, rebalances)
//   * retry transient failures; dead-letter permanent ones AND commit past them
//
// `charge` returns true on success, throws InvalidOperationException for a PERMANENT failure, and
// throws TimeoutException for a TRANSIENT one.
public sealed class BillingConsumer
{
    private readonly OrderLog _log;
    private readonly string _group;
    private readonly int _maxAttempts;
    private readonly Func<LogRecord, bool> _charge;

    public BillingConsumer(OrderLog log, string group, int maxAttempts, Func<LogRecord, bool> charge)
    {
        _log = log;
        _group = group;
        _maxAttempts = maxAttempts;
        _charge = charge;
    }

    /// <summary>Reads from the committed offset to the end of the partition.</summary>
    public ConsumeReport ConsumeAll(int partition) =>
        throw new NotImplementedException("Exercise 09: see practice/Exercise09-Messaging.md");

    /// <summary>Where this group will resume from. Starts at 0.</summary>
    public long CommittedOffset(int partition) =>
        throw new NotImplementedException("Exercise 09: see practice/Exercise09-Messaging.md");

    /// <summary>Rewinds this group's position - a replay, which must not re-apply side effects.</summary>
    public void SeekToBeginning(int partition) =>
        throw new NotImplementedException("Exercise 09: see practice/Exercise09-Messaging.md");

    /// <summary>Records a human has to look at. A log has no DLQ, so this is yours.</summary>
    public IReadOnlyList<LogRecord> DeadLetters =>
        throw new NotImplementedException("Exercise 09: see practice/Exercise09-Messaging.md");

    /// <summary>Side effects actually applied - redeliveries must not increase it.</summary>
    public int ChargesApplied =>
        throw new NotImplementedException("Exercise 09: see practice/Exercise09-Messaging.md");
}
