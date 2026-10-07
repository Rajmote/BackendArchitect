namespace BackendArchitect.Distributed.Theory;

public enum ReadStrategy
{
    FromReplica,        // eventual - fastest, and it may not have your write yet
    ReadYourWrites      // wait until the replica has caught up to the version you wrote
}

public sealed record ReadAfterWriteOutcome(string Strategy, int Reads, int StaleReads);

public sealed record ConvergenceOutcome(string Scenario, int LagAtEnd, bool Converged);

// "It's eventually consistent, it'll catch up in a few milliseconds."
//
// The formal guarantee is weaker than that sentence implies:
//
//     IF WRITES STOP, the replicas will converge.
//
// There is no time bound anywhere in it, and it is conditional on writes stopping - which in a live
// system they never do. "A few milliseconds" is an observation about a healthy day being quoted as a
// guarantee.
public sealed class ReplicatedStore
{
    private readonly Dictionary<string, string> _leader = [];
    private readonly Dictionary<string, string> _replica = [];
    private readonly Queue<(string Key, string Value, long Version)> _inFlight = new();
    private readonly int _replicationDelayTicks;

    private long _leaderVersion;
    private long _replicaVersion;

    public ReplicatedStore(int replicationDelayTicks) => _replicationDelayTicks = replicationDelayTicks;

    public long Lag => _leaderVersion - _replicaVersion;

    public long Write(string key, string value)
    {
        _leader[key] = value;
        _inFlight.Enqueue((key, value, ++_leaderVersion));
        Tick();
        return _leaderVersion;
    }

    /// <summary>One unit of replication progress. Writes arrive faster than this when load is high.</summary>
    public void Tick()
    {
        while (_inFlight.Count > _replicationDelayTicks)
        {
            var (key, value, version) = _inFlight.Dequeue();
            _replica[key] = value;
            _replicaVersion = version;
        }
    }

    public void DrainUntilCaughtUp()
    {
        while (_inFlight.Count > 0)
        {
            var (key, value, version) = _inFlight.Dequeue();
            _replica[key] = value;
            _replicaVersion = version;
        }
    }

    public string? Read(string key, ReadStrategy strategy, long writtenVersion = 0)
    {
        if (strategy == ReadStrategy.FromReplica)
            return _replica.GetValueOrDefault(key);

        // Read-your-writes: the session token says which version you must see before reading. Cosmos
        // Session consistency is exactly this, and it breaks behind a load balancer if the token is
        // not flowed with the request.
        while (_replicaVersion < writtenVersion)
            DrainUntilCaughtUp();

        return _replica.GetValueOrDefault(key);
    }

    /// <summary>Write, then immediately read the thing you just wrote - the commonest real bug.</summary>
    public static ReadAfterWriteOutcome MeasureReadAfterWrite(ReadStrategy strategy, int writes, int delayTicks)
    {
        var store = new ReplicatedStore(delayTicks);
        var stale = 0;

        for (var i = 0; i < writes; i++)
        {
            var key = $"order-{i}";
            var version = store.Write(key, "created");

            if (store.Read(key, strategy, version) is null)
                stale++;
        }

        return new ReadAfterWriteOutcome(
            strategy == ReadStrategy.FromReplica ? "read from replica" : "read-your-writes",
            writes,
            stale);
    }

    /// <summary>
    /// Convergence is conditional on writes stopping. While they continue, the lag never reaches zero
    /// however long you wait.
    /// </summary>
    public static ConvergenceOutcome MeasureConvergence(bool writesEverStop, int writes, int delayTicks)
    {
        var store = new ReplicatedStore(delayTicks);

        for (var i = 0; i < writes; i++)
            store.Write($"order-{i}", "created");

        if (writesEverStop)
            store.DrainUntilCaughtUp();

        return new ConvergenceOutcome(
            writesEverStop ? "writes stop" : "writes never stop",
            (int)store.Lag,
            store.Lag == 0);
    }
}
