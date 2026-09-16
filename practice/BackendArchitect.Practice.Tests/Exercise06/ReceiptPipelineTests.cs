using BackendArchitect.Practice.Exercise06;

namespace BackendArchitect.Practice.Tests.Exercise06;

// Exercise 06 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * the workers survive — with 1 order in 10 poisoned, all N workers are still running at the end
//   * bounded queue       — PeakQueueDepth never exceeds the capacity you configured
//   * completion          — no ChannelClosedException, and the write stage sees every receipt
//   * cancellation        — a cancelled token stops the run promptly
//
// 🌟 And VERIFY THE TESTS CAN FAIL: move your try/catch OUTSIDE the `await foreach` and confirm the
//    "nothing is lost" test goes red. That placement is the entire lesson.
public class ReceiptPipelineTests
{
    private static Receipt Render(int orderId) => new(orderId, orderId * 1.5m);

    private static Receipt RenderWithPoison(int orderId)
    {
        if (orderId % 10 == 0 && orderId > 0)
            throw new InvalidOperationException($"order {orderId} is malformed");

        return Render(orderId);
    }

    [Fact]
    public async Task ItProcessesEveryOrder()
    {
        var pipeline = new ReceiptPipeline(renderWorkers: 4, capacity: 8, Render);

        var outcome = await pipeline.RunAsync([.. Enumerable.Range(1, 50)]);

        Assert.Equal(50, outcome.Processed);
        Assert.Equal(0, outcome.DeadLettered);
        Assert.Equal(50, pipeline.Written.Count);
    }

    [Fact]
    public async Task AFailingRenderCostsOneOrderNotThePipeline()
    {
        var pipeline = new ReceiptPipeline(renderWorkers: 4, capacity: 8, RenderWithPoison);

        var outcome = await pipeline.RunAsync([.. Enumerable.Range(1, 50)]);

        Assert.Equal(5, outcome.DeadLettered);               // 10, 20, 30, 40, 50
        Assert.Equal(45, outcome.Processed);
        Assert.Equal([10, 20, 30, 40, 50], [.. pipeline.DeadLetters.Order()]);
    }

    [Fact]
    public async Task NothingIsEverLost()
    {
        var pipeline = new ReceiptPipeline(renderWorkers: 4, capacity: 8, RenderWithPoison);

        var outcome = await pipeline.RunAsync([.. Enumerable.Range(1, 200)]);

        Assert.Equal(200, outcome.Processed + outcome.DeadLettered);
    }

    // TODO (you): bounded queue — PeakQueueDepth <= the capacity you passed in, with a slow write stage.

    // TODO (you): completion — a single worker calling Complete() strands its siblings. Prove your
    //             implementation doesn't: every rendered receipt reaches Written.

    // TODO (you): cancellation — pass an already-cancelled token and assert it throws
    //             OperationCanceledException rather than quietly processing everything.
}
