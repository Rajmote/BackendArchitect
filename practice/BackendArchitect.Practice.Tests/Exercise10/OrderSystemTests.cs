using BackendArchitect.Practice.Exercise10;

namespace BackendArchitect.Practice.Tests.Exercise10;

// Exercise 10 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * read-your-writes  — QueryAtLeastVersion returns the order the command just wrote
//   * idempotency       — running the projector twice does not duplicate or corrupt the read model
//   * a stalled projector leaves lag rising while Query keeps returning stale-but-valid data
//   * rebuild           — after RebuildReadModel the read store matches the write store exactly
public class OrderSystemTests
{
    [Fact]
    public void TheCommandReturnsWhatItWrote()
    {
        var system = new OrderSystem();

        var written = system.Handle(new PlaceOrder(1, "Alice", 25.00m));

        // The write side already HAS the order - no query needed, and no staleness.
        Assert.Equal(1, written.OrderId);
        Assert.Equal("Alice", written.Customer);
        Assert.True(written.Version > 0);
    }

    [Fact]
    public void TheReadStoreDoesNotHaveItUntilTheProjectorRuns()
    {
        var system = new OrderSystem();
        system.Handle(new PlaceOrder(1, "Alice", 25.00m));

        Assert.Null(system.Query(1));        // not a bug - this is what level 3 costs

        system.RunProjector();

        Assert.NotNull(system.Query(1));
    }

    [Fact]
    public void AStalledProjectorLeavesTheReadStoreHealthyAndWrong()
    {
        var system = new OrderSystem();
        system.Handle(new PlaceOrder(1, "Alice", 25.00m));
        system.RunProjector();

        system.StallProjector();
        system.Handle(new PlaceOrder(2, "Bob", 40.00m));
        system.RunProjector();

        Assert.NotNull(system.Query(1));     // answering, fast, and out of date
        Assert.Null(system.Query(2));
        Assert.True(system.Health.Lag > 0);
        Assert.True(system.Health.Stalled);
    }

    // TODO (you): read-your-writes — QueryAtLeastVersion(id, written.Version) returns the order
    //             without a manual RunProjector() call.

    // TODO (you): idempotency — RunProjector() twice over the same events leaves exactly one row per
    //             order. At-least-once means the projector WILL see an event twice.

    // TODO (you): freshness — with the projector stalled, Query still returns 200-shaped data for old
    //             orders. Assert that only Health reveals the problem.

    // TODO (you): rebuild — delete and RebuildReadModel(), then assert every order written is present.
    //             This is the best argument for level 3: the read side is disposable.
}
