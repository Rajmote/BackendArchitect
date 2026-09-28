using System.Diagnostics;
using BackendArchitect.Practice.Exercise07;

namespace BackendArchitect.Practice.Tests.Exercise07;

// Exercise 07 — three starter tests showing the target shape.
//
// YOUR JOB: make these pass, then add tests for:
//   * the duration histogram records elapsed milliseconds
//   * a failure sets the span status to Error, counts status=failed, and rethrows
//   * no listener  — HasListeners() is false, Process still works, Activity.Current stays null
//   * cardinality  — distinct label combinations stay constant from 10 orders to 1,000
//
// 🌟 The listener below is what an exporter does, in miniature. No OpenTelemetry package, no collector,
//    no container — spans and metrics are unit-testable in process.
public class InstrumentedOrderServiceTests
{
    private static ActivityListener ListenFor(ICollection<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OrderTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public void ItEmitsAProcessOrderSpan()
    {
        var captured = new List<Activity>();
        using var _ = ListenFor(captured);
        var service = new InstrumentedOrderService(orderId => orderId * 1.5m, region: "eu-west");

        service.Process(orderId: 4);

        Assert.Contains(captured, activity => activity.OperationName == "process-order");
    }

    [Fact]
    public void TheFetchPriceSpanIsAChildOfProcessOrder()
    {
        var captured = new List<Activity>();
        using var _ = ListenFor(captured);
        var service = new InstrumentedOrderService(orderId => orderId * 1.5m, region: "eu-west");

        service.Process(orderId: 4);

        var root = captured.Single(activity => activity.OperationName == "process-order");
        var child = captured.Single(activity => activity.OperationName == "fetch-price");

        Assert.Equal(root.SpanId, child.ParentSpanId);
        Assert.Equal(root.TraceId, child.TraceId);
    }

    [Fact]
    public void TheOrderIdIsATagOnTheSpanNotAMetricLabel()
    {
        var captured = new List<Activity>();
        using var _ = ListenFor(captured);
        var service = new InstrumentedOrderService(orderId => orderId * 1.5m, region: "eu-west");

        service.Process(orderId: 4471);

        var root = captured.Single(activity => activity.OperationName == "process-order");
        Assert.Equal("4471", root.GetTagItem("order.id")?.ToString());
    }

    // TODO (you): the duration histogram — attach a MeterListener and assert "orders.duration" recorded
    //             a value. Hint: SetMeasurementEventCallback<double>, then listener.Start().

    // TODO (you): failure — a priceOf that throws must set ActivityStatusCode.Error on the root span,
    //             count status=failed, and still rethrow to the caller.

    // TODO (you): no listener — Assert.False(OrderTelemetry.Source.HasListeners()) before the call and
    //             Assert.Null(Activity.Current) after. A test that cannot fail proves nothing.

    // TODO (you): cardinality — process 10 orders, then 1,000, and assert the number of DISTINCT label
    //             combinations on orders.processed is identical. That is the whole point of §3.
}
