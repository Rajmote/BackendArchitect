namespace BackendArchitect.Practice.Exercise10;

public sealed record PlaceOrder(int OrderId, string Customer, decimal Total);

public sealed record OrderSummary(int OrderId, string Customer, decimal Total, long Version);

public sealed record ProjectorHealth(long Published, long Projected, long Lag, bool Stalled);

// Exercise 10 — see practice/Exercise10-Cqrs.md.
//
// A level-3 slice: a write store, a stream of events, a projector you control, and a read store.
// Everything the pattern costs you shows up here:
//
//   * the order a user just placed is not in the read store yet
//   * the projector is a consumer group, so it must be idempotent and it can stall
//   * a stalled projector leaves the read store UP, FAST and WRONG - so health has to be measured
//     as freshness, not as "did it respond"
public sealed class OrderSystem
{
    /// <summary>Writes to the write store and publishes an event. Returns what was written.</summary>
    public OrderSummary Handle(PlaceOrder command) =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");

    /// <summary>Reads the read store only. Returns null when the projection has not caught up.</summary>
    public OrderSummary? Query(int orderId) =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");

    /// <summary>
    /// Blocks until the read model has reached <paramref name="version"/>, then reads - a session
    /// token. Returns null only if the projector is stalled.
    /// </summary>
    public OrderSummary? QueryAtLeastVersion(int orderId, long version) =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");

    /// <summary>Applies pending events to the read store. Must be safe to call twice.</summary>
    public int RunProjector() =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");

    /// <summary>Simulates a poison event: the projector stops advancing.</summary>
    public void StallProjector() =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");

    /// <summary>Freshness, not liveness. The read store being up tells you nothing.</summary>
    public ProjectorHealth Health =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");

    /// <summary>Throws away the read model and rebuilds it from the event stream.</summary>
    public int RebuildReadModel() =>
        throw new NotImplementedException("Exercise 10: see practice/Exercise10-Cqrs.md");
}
