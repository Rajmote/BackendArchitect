namespace BackendArchitect.Databases.Cosmos.Consistency;

// Cosmos's five consistency levels, strongest to weakest.
public enum CosmosConsistency
{
    Strong,
    BoundedStaleness,
    Session,
    ConsistentPrefix,
    Eventual,
}

// A simplified model of one write region plus a lagging read replica, to show what each consistency
// level actually returns (an analogy, not the real replication protocol).
//
//   * the primary holds every committed write, in order
//   * the replica has caught up on only the first N of them  -> that gap IS the staleness
//
// Reads are then served differently depending on the level the caller asked for.
public sealed class ReplicatedStore
{
    private readonly List<string> _committed = [];   // the primary's ordered history
    private int _replicaCaughtUpTo;                  // how many writes the replica has

    /// <summary>Versions the replica is behind the primary.</summary>
    public int ReplicationLag => _committed.Count - _replicaCaughtUpTo;

    public string? LatestOnPrimary => _committed.Count == 0 ? null : _committed[^1];

    public string? LatestOnReplica => _replicaCaughtUpTo == 0 ? null : _committed[_replicaCaughtUpTo - 1];

    /// <summary>Commit a write on the primary. The replica does not see it until it catches up.</summary>
    public int Write(string value)
    {
        _committed.Add(value);
        return _committed.Count;   // acts as the session token: "my write was version N"
    }

    /// <summary>The replica catches up by one write.</summary>
    public void Replicate(int writes = 1) =>
        _replicaCaughtUpTo = Math.Min(_committed.Count, _replicaCaughtUpTo + writes);

    /// <summary>
    /// Read under a given consistency level.
    /// <paramref name="sessionToken"/> is the version the caller last wrote (Session only).
    /// <paramref name="maxStaleness"/> is the K-version bound (Bounded Staleness only).
    /// </summary>
    public string? Read(CosmosConsistency level, int sessionToken = 0, int maxStaleness = 1)
    {
        if (_committed.Count == 0)
            return null;

        var visibleVersion = level switch
        {
            // Always the newest committed write — the read waits for the replica if needed.
            CosmosConsistency.Strong => _committed.Count,

            // Never more than `maxStaleness` versions behind; the read waits if the lag exceeds it.
            CosmosConsistency.BoundedStaleness => Math.Max(_replicaCaughtUpTo, _committed.Count - maxStaleness),

            // At least everything THIS caller wrote ("read your own writes"), otherwise replica state.
            CosmosConsistency.Session => Math.Max(_replicaCaughtUpTo, sessionToken),

            // Stale, but always an unbroken prefix of the history — never skips a write.
            CosmosConsistency.ConsistentPrefix => _replicaCaughtUpTo,

            // Whatever the replica happens to have.
            CosmosConsistency.Eventual => _replicaCaughtUpTo,

            _ => _replicaCaughtUpTo,
        };

        return visibleVersion == 0 ? null : _committed[visibleVersion - 1];
    }

    /// <summary>Strong and Bounded Staleness charge roughly double for reads.</summary>
    public static double ReadCostMultiplier(CosmosConsistency level) =>
        level is CosmosConsistency.Strong or CosmosConsistency.BoundedStaleness ? 2.0 : 1.0;
}
