using BackendArchitect.Practice.Exercise08;

namespace BackendArchitect.Practice.Tests.Exercise08;

// Exercise 08 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * idempotent retry  — the same requestId twice reserves ONE unit, and returns the same token
//   * stock is never oversold, even with every request retried
//   * fencing           — Commit(33) after Commit(34) is rejected
//   * the minority side refuses every reservation for the whole partition
public class InventoryServiceTests
{
    private static InventoryService NewService(int stock = 10) =>
        new(nodeCount: 5, minoritySize: 2, stock);

    [Fact]
    public void AMajorityOfFiveNodesIsThree()
    {
        var service = NewService();

        Assert.Equal(3, service.Quorum);
    }

    [Fact]
    public void TheMajoritySideCanReserve()
    {
        var service = NewService();

        var result = service.Reserve("request-1", NodeSide.Majority);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.FencingToken);
        Assert.Equal(9, service.Stock);
    }

    [Fact]
    public void TheMinoritySideRefusesWithoutThrowing()
    {
        var service = NewService();

        var result = service.Reserve("request-1", NodeSide.Minority);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(10, service.Stock);        // refusing is the design working, not failing
    }

    // TODO (you): idempotent retry — Reserve("request-1", Majority) twice must reserve ONE unit and
    //             return the same fencing token both times. The caller cannot tell "never arrived"
    //             from "done, response lost", so the retry must be the same operation.

    // TODO (you): no overselling — 100 reservations against stock of 10, every one retried once.
    //             Exactly 10 succeed and Stock lands on 0.

    // TODO (you): fencing — Commit(34) then Commit(33) must reject the second. Safety lives in the
    //             RESOURCE, not in the lock.

    // TODO (you): the whole partition — every minority-side call refuses, for every request id.
}
