using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BackendArchitect.Practice.Exercise07;

public sealed record OrderOutcome(int OrderId, decimal Total, bool Succeeded);

// Exercise 07 — see practice/Exercise07-Observability.md.
//
// Instrument this service so an on-call engineer can answer, at 3am, WITHOUT deploying code:
//   * how many orders are failing, and in which region?      -> a metric
//   * which order was slow, and what inside it was slow?     -> a trace
//   * why did that specific one fail?                        -> a log, joined by trace id
//
// The names are fixed so the starter tests can assert on them.
public static class OrderTelemetry
{
    public const string SourceName = "BackendArchitect.Practice.Orders";

    // TODO (you): an ActivitySource named SourceName.
    public static ActivitySource Source =>
        throw new NotImplementedException("Exercise 07: see practice/Exercise07-Observability.md");

    // TODO (you): a Counter<long> called "orders.processed" on a Meter named SourceName.
    public static Counter<long> Processed =>
        throw new NotImplementedException("Exercise 07: see practice/Exercise07-Observability.md");

    // TODO (you): a Histogram<double> called "orders.duration", unit "ms".
    public static Histogram<double> Duration =>
        throw new NotImplementedException("Exercise 07: see practice/Exercise07-Observability.md");
}

public sealed class InstrumentedOrderService
{
    private readonly Func<int, decimal> _priceOf;
    private readonly string _region;

    public InstrumentedOrderService(Func<int, decimal> priceOf, string region)
    {
        _priceOf = priceOf;
        _region = region;
    }

    /// <summary>
    /// Prices one order. Emits a "process-order" span with a child "fetch-price" span, records the
    /// duration, and counts the outcome. A failure must mark the span AND count as failed - then
    /// rethrow.
    /// </summary>
    public OrderOutcome Process(int orderId) =>
        throw new NotImplementedException("Exercise 07: see practice/Exercise07-Observability.md");
}
