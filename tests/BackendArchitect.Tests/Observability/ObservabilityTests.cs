using BackendArchitect.Observability.Telemetry;
using BackendArchitect.Observability.Telemetry.Production;

namespace BackendArchitect.Tests.Observability;

public class ObservabilityTests
{
    [Fact]
    public void The_average_hides_a_slow_tail_that_the_p99_exposes()
    {
        var samples = LatencyStats.FastWithASlowTail(total: 1_000, slowCount: 15, fastMs: 10, slowMs: 2_000);

        var summary = LatencyStats.Summarize(samples);

        Assert.True(summary.AverageMs < 100, "the average sits comfortably inside a 100ms SLA");
        Assert.Equal(10, summary.P50Ms);
        Assert.Equal(10, summary.P95Ms);
        Assert.Equal(2_000, summary.P99Ms);      // the tail the average absorbed
    }

    [Fact]
    public void No_sample_is_anywhere_near_the_average()
    {
        var samples = LatencyStats.FastWithASlowTail(total: 1_000, slowCount: 15, fastMs: 10, slowMs: 2_000);

        var summary = LatencyStats.Summarize(samples);

        // The average describes nobody: every sample is 4x below it or 50x above it.
        Assert.DoesNotContain(samples, sample => Math.Abs(sample - summary.AverageMs) < 1);
    }

    [Fact]
    public void An_unbounded_label_creates_one_series_per_value()
    {
        var report = MetricRegistry.LabelledByOrderId(orders: 100_000);

        Assert.Equal(100_000, report.DistinctSeries);
    }

    [Fact]
    public void Bounded_labels_create_the_product_of_their_value_counts()
    {
        var report = MetricRegistry.LabelledByStatusAndRegion(orders: 100_000);

        Assert.Equal(15, report.DistinctSeries);      // 3 statuses x 5 regions, forever
    }

    [Fact]
    public void A_propagated_traceparent_keeps_one_request_in_one_trace()
    {
        var report = SpanRecorder.CallAcrossServices(propagate: true);

        Assert.Equal(1, report.DistinctTraces);
        Assert.Equal(1, report.RootSpans);
        Assert.Equal(5, report.Spans);
    }

    [Fact]
    public void A_dropped_traceparent_splits_one_request_into_two_traces()
    {
        var report = SpanRecorder.CallAcrossServices(propagate: false);

        Assert.Equal(2, report.DistinctTraces);
        Assert.Equal(2, report.RootSpans);           // the far side started its own
        Assert.Equal(5, report.Spans);               // the same work, now unjoinable
    }

    [Fact]
    public void Child_spans_inherit_the_trace_id_and_point_at_their_parent()
    {
        var recorder = new SpanRecorder();

        using (recorder.Start("outer"))
        using (recorder.Start("inner")) { }

        var inner = recorder.Spans.Single(span => span.Name == "inner");
        var outer = recorder.Spans.Single(span => span.Name == "outer");

        Assert.Equal(outer.TraceId, inner.TraceId);
        Assert.Equal(outer.SpanId, inner.ParentSpanId);
        Assert.Null(outer.ParentSpanId);
    }

    [Fact]
    public void Instrumentation_costs_nothing_when_no_one_is_listening()
    {
        // No collector, so ActivitySource has no listener: StartActivity returns null and the
        // activity?.SetTag calls are no-ops. The domain code runs unchanged.
        Assert.False(OrderTelemetry.Source.HasListeners(), "no collector should be attached here");

        var pricer = new OrderPricer(orderId => orderId * 1.5);

        var total = pricer.Price(orderId: 4);

        Assert.Equal(6, total);
        Assert.Null(System.Diagnostics.Activity.Current);   // nothing was ever created
    }

    [Fact]
    public void A_listener_captures_the_spans_the_domain_emits()
    {
        using var collector = new TelemetryCollector(OrderTelemetry.SourceName);
        var pricer = new OrderPricer(orderId => orderId * 1.5);

        pricer.Price(orderId: 4);

        var root = collector.Activities.Single(activity => activity.OperationName == "price-order");
        var child = collector.Activities.Single(activity => activity.OperationName == "fetch-price");

        Assert.Equal(root.SpanId, child.ParentSpanId);
        Assert.Equal(root.TraceId, child.TraceId);
        Assert.Equal("4", root.GetTagItem("order.id")?.ToString());
    }

    [Fact]
    public void A_listener_captures_the_measurements_too()
    {
        using var collector = new TelemetryCollector(OrderTelemetry.SourceName);
        var pricer = new OrderPricer(orderId => orderId * 1.5);

        for (var order = 1; order <= 10; order++)
            pricer.Price(order);

        Assert.Equal(10, collector.Measurements.Count(m => m.Instrument == "orders.processed"));
        Assert.Equal(10, collector.DurationsOf("orders.duration").Count);
        Assert.All(collector.Measurements, m => Assert.Contains("status=succeeded", m.Labels));
    }

    [Fact]
    public void A_failure_marks_the_span_and_the_counter()
    {
        using var collector = new TelemetryCollector(OrderTelemetry.SourceName);
        var pricer = new OrderPricer(_ => throw new InvalidOperationException("pricing service is down"));

        Assert.Throws<InvalidOperationException>(() => pricer.Price(orderId: 7));

        var root = collector.Activities.Single(activity => activity.OperationName == "price-order");
        Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, root.Status);
        Assert.Contains(collector.Measurements, m => m.Labels.Contains("status=failed"));
    }
}
