namespace BackendArchitect.Distributed.Theory;

public enum PartitionBehaviour
{
    ConsistentAndPartitionTolerant,   // CP - refuse the write unless a majority can confirm it
    AvailableAndPartitionTolerant     // AP - accept it locally and reconcile later
}

public sealed record PartitionOutcome(
    string Behaviour,
    int WritesAccepted,
    int WritesRefused,
    int DivergentKeysAfterHeal);

// CAP, with the slogan corrected.
//
// "Pick two" is misleading: P is NOT a choice, because networks partition whether you like it or not.
// The only choice is between C and A, and only DURING a partition - when the network is healthy you
// get both.
//
// It is also per-OPERATION, not per-system. The same shop is AP for "add to cart" and CP for "take
// payment". A quorum needs a majority, which is why the minority side of a 3/2 split cannot accept a
// CP write: two majorities of the same set must overlap, so two conflicting decisions can never both
// be accepted.
public sealed class QuorumCluster
{
    private readonly Dictionary<string, string>[] _nodes;
    private readonly int _majoritySide;
    private readonly int _minoritySide;

    public QuorumCluster(int nodeCount, int minoritySize)
    {
        _nodes = [.. Enumerable.Range(0, nodeCount).Select(_ => new Dictionary<string, string>())];
        _minoritySide = minoritySize;
        _majoritySide = nodeCount - minoritySize;
    }

    public int Quorum => _nodes.Length / 2 + 1;

    private IEnumerable<Dictionary<string, string>> Side(bool minority) =>
        minority ? _nodes.Take(_minoritySide) : _nodes.Skip(_minoritySide);

    /// <summary>Returns false when the write was refused for want of a quorum.</summary>
    public bool Write(string key, string value, bool fromMinoritySide, PartitionBehaviour behaviour)
    {
        var reachable = fromMinoritySide ? _minoritySide : _majoritySide;

        if (behaviour == PartitionBehaviour.ConsistentAndPartitionTolerant && reachable < Quorum)
            return false;                                   // cannot confirm it is safe, so refuse

        foreach (var node in Side(fromMinoritySide))
            node[key] = value;

        return true;
    }

    /// <summary>
    /// Keys that need a human or a merge policy once the split heals. A key only ONE side has is not a
    /// conflict - it simply replicates across. A conflict is both sides holding DIFFERENT values, with
    /// no way to tell which one the world should believe.
    /// </summary>
    public int DivergentKeys()
    {
        var minority = _nodes[0];
        var majority = _nodes[^1];

        return minority.Keys.Intersect(majority.Keys).Count(key => minority[key] != majority[key]);
    }

    public static PartitionOutcome RunSplitBrain(PartitionBehaviour behaviour, int keys)
    {
        var cluster = new QuorumCluster(nodeCount: 5, minoritySize: 2);
        var accepted = 0;
        var refused = 0;

        // Both sides are alive and serving. The same keys are written on each, with different values.
        for (var i = 0; i < keys; i++)
        {
            if (cluster.Write($"key-{i}", "from-majority", fromMinoritySide: false, behaviour))
                accepted++;
            else
                refused++;

            if (cluster.Write($"key-{i}", "from-minority", fromMinoritySide: true, behaviour))
                accepted++;
            else
                refused++;
        }

        return new PartitionOutcome(
            behaviour == PartitionBehaviour.ConsistentAndPartitionTolerant ? "CP (refuse)" : "AP (accept)",
            accepted,
            refused,
            cluster.DivergentKeys());
    }
}
