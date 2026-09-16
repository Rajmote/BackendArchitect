namespace BackendArchitect.Practice.Exercise06;

public sealed record Receipt(int OrderId, decimal Total);

public sealed record PipelineOutcome(int Processed, int DeadLettered, int PeakQueueDepth);

// Exercise 06 — see practice/Exercise06-Pipelines.md.
//
// A two-stage pipeline: render a receipt for each order id, then write it out. The render step is
// supplied by the caller and sometimes throws, which is the whole point.
//
//   order ids ──► render (N workers) ──[bounded queue]──► write (1 worker) ──► receipts
//
// Three rules, and every one of them is a bug you have already seen fire:
//   1. a failing render costs ONE order - it must not kill the worker
//   2. nothing is lost: Processed + DeadLettered == the number of order ids
//   3. the queue between the stages never exceeds its capacity
public sealed class ReceiptPipeline
{
    private readonly int _renderWorkers;
    private readonly int _capacity;
    private readonly Func<int, Receipt> _render;

    public ReceiptPipeline(int renderWorkers, int capacity, Func<int, Receipt> render)
    {
        _renderWorkers = renderWorkers;
        _capacity = capacity;
        _render = render;
    }

    /// <summary>
    /// Runs every order id through the pipeline and returns once the last receipt has been written.
    /// Orders whose render throws go to <see cref="DeadLetters"/>; everything else is written.
    /// </summary>
    public Task<PipelineOutcome> RunAsync(IReadOnlyList<int> orderIds, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Exercise 06: see practice/Exercise06-Pipelines.md");

    /// <summary>The order ids whose render threw. A human looks at these.</summary>
    public IReadOnlyList<int> DeadLetters =>
        throw new NotImplementedException("Exercise 06: see practice/Exercise06-Pipelines.md");

    /// <summary>Everything the write stage actually wrote, in the order it wrote them.</summary>
    public IReadOnlyList<Receipt> Written =>
        throw new NotImplementedException("Exercise 06: see practice/Exercise06-Pipelines.md");
}
