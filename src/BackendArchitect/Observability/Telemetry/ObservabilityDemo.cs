using BackendArchitect.Observability.Telemetry.Production;

namespace BackendArchitect.Observability.Telemetry;

// Example runner: why averages lie, what cardinality costs, what a dropped traceparent looks like,
// and the same instrumentation with and without a listener attached.
public class ObservabilityDemo
{
    public void Run()
    {
        // --- 1. the average that describes nobody ---
        var samples = LatencyStats.FastWithASlowTail(total: 1_000, slowCount: 15, fastMs: 10, slowMs: 2_000);
        var summary = LatencyStats.Summarize(samples);

        Console.WriteLine("1,000 requests: 985 take 10ms, 15 take 2000ms");
        Console.WriteLine($"  average : {summary.AverageMs,7:F2} ms   <- inside a 100ms SLA, so nobody investigates");
        Console.WriteLine($"  p50     : {summary.P50Ms,7:F2} ms");
        Console.WriteLine($"  p95     : {summary.P95Ms,7:F2} ms");
        Console.WriteLine($"  p99     : {summary.P99Ms,7:F2} ms   <- 1 customer in 100 waits two seconds");
        Console.WriteLine("  -> the average absorbed the slow requests; only the percentile reveals they exist");

        // --- 2. cardinality ---
        const int orders = 100_000;
        var byOrderId = MetricRegistry.LabelledByOrderId(orders);
        var byStatus = MetricRegistry.LabelledByStatusAndRegion(orders);

        Console.WriteLine();
        Console.WriteLine($"{orders:N0} measurements, and how many time series each labelling creates:");
        foreach (var report in new[] { byOrderId, byStatus })
            Console.WriteLine($"  {report.Strategy,-26}: {report.DistinctSeries,7:N0} series");
        Console.WriteLine("  -> every label combination is stored forever; \"which one?\" belongs on a span, not a metric");

        // --- 3. trace propagation ---
        var joined = SpanRecorder.CallAcrossServices(propagate: true);
        var severed = SpanRecorder.CallAcrossServices(propagate: false);

        Console.WriteLine();
        Console.WriteLine("Service A calls service B, 5 spans in total:");
        foreach (var report in new[] { joined, severed })
            Console.WriteLine($"  {report.Scenario,-24}: {report.DistinctTraces} trace(s), {report.RootSpans} root span(s)");
        Console.WriteLine("  -> one dropped header turns one request into two traces nobody can join up later");

        // --- 4. the same instrumentation, with and without a listener ---
        var unobserved = new OrderPricer(orderId => orderId * 1.5);
        for (var order = 1; order <= 100; order++)
            unobserved.Price(order);

        using var collector = new TelemetryCollector(OrderTelemetry.SourceName);
        var observed = new OrderPricer(orderId => orderId * 1.5);
        for (var order = 1; order <= 100; order++)
            observed.Price(order);

        var root = collector.Activities.Where(activity => activity.OperationName == "price-order").ToArray();
        var child = collector.Activities.Where(activity => activity.OperationName == "fetch-price").ToArray();

        Console.WriteLine();
        Console.WriteLine("100 orders priced with NO listener, then 100 with one attached:");
        Console.WriteLine($"  spans captured before the listener existed : 0 (StartActivity returned null)");
        Console.WriteLine($"  spans captured after                       : {collector.Activities.Count} ({root.Length} price-order + {child.Length} fetch-price)");
        Console.WriteLine($"  every fetch-price has price-order as parent: {child.All(c => c.Parent?.OperationName == "price-order")}");
        Console.WriteLine($"  measurements captured                      : {collector.Measurements.Count}");
        Console.WriteLine("  -> identical domain code; the only difference is four lines of registration at startup");
    }
}
