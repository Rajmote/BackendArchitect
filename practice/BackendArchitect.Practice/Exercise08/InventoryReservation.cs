namespace BackendArchitect.Practice.Exercise08;

public sealed record ReservationResult(bool Succeeded, int? FencingToken, string? Error);

public enum NodeSide
{
    Majority,
    Minority
}

// Exercise 08 — see practice/Exercise08-DistributedTheory.md.
//
// A 5-node inventory service during a 3|2 network partition. Reserving stock is a CP operation: it is
// better to refuse a reservation than to sell the same unit twice.
//
// Three things have to hold:
//   1. only a side that can reach a quorum (3 of 5) may accept a reservation
//   2. a retried reservation carrying the same request id must NOT reserve twice - the caller cannot
//      tell "never arrived" from "done, response lost"
//   3. the fencing token increases monotonically, and a write carrying a stale one is rejected
public sealed class InventoryService
{
    private readonly int _nodeCount;
    private readonly int _minoritySize;

    public InventoryService(int nodeCount, int minoritySize, int stock)
    {
        _nodeCount = nodeCount;
        _minoritySize = minoritySize;
        Stock = stock;
    }

    public int Stock { get; private set; }

    /// <summary>3 for a 5-node cluster.</summary>
    public int Quorum =>
        throw new NotImplementedException("Exercise 08: see practice/Exercise08-DistributedTheory.md");

    /// <summary>
    /// Reserves one unit. Refuses when the side cannot reach a quorum. A repeat of the same
    /// <paramref name="requestId"/> returns the ORIGINAL result rather than reserving again.
    /// </summary>
    public ReservationResult Reserve(string requestId, NodeSide side) =>
        throw new NotImplementedException("Exercise 08: see practice/Exercise08-DistributedTheory.md");

    /// <summary>
    /// Applies a reservation to storage. Rejects any token lower than the highest already seen - a
    /// paused holder waking up late must not be able to write.
    /// </summary>
    public bool Commit(int fencingToken) =>
        throw new NotImplementedException("Exercise 08: see practice/Exercise08-DistributedTheory.md");
}
