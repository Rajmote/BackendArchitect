# Exercise 07 — instrument an order service so 3am is survivable

**Topic:** [§5.1 Observability](../src/BackendArchitect/Observability/Telemetry/Observability.md)
**Difficulty:** ⭐⭐
**Status:** 🔴 in progress — stubs and starter tests are in place.

---

## Scenario

An order service prices orders. It works. It is also completely opaque: when it misbehaves at 3am, the
on-call engineer has nothing but "some orders are slow".

Instrument it so three questions can be answered **without deploying code**:

| Question | Pillar |
|---|---|
| How many orders are failing, and in which region? | metric |
| Which order was slow, and what inside it was slow? | trace |
| Why did that specific one fail? | log, joined by trace id |

---

## What you implement

```csharp
public static class OrderTelemetry
{
    public const string SourceName = "BackendArchitect.Practice.Orders";

    public static ActivitySource Source { get; }        // named SourceName
    public static Counter<long> Processed { get; }      // "orders.processed"
    public static Histogram<double> Duration { get; }   // "orders.duration", unit "ms"
}

public sealed class InstrumentedOrderService
{
    public InstrumentedOrderService(Func<int, decimal> priceOf, string region);
    public OrderOutcome Process(int orderId);
}
```

## Requirements

| # | Requirement |
|---|---|
| 1 | `Process` emits a span named **`process-order`** with a child span **`fetch-price`** |
| 2 | The child's parent is the root — one trace, correct tree |
| 3 | The root span carries the order id as a **tag** (high cardinality is fine on a span) |
| 4 | `orders.processed` is counted once per call, labelled **`status`** and **`region`** only |
| 5 | `orders.duration` records the elapsed milliseconds |
| 6 | A failure sets the span status to `Error`, counts `status=failed`, and **rethrows** |
| 7 | With **no listener attached**, `Process` still works and creates no activity |

## Acceptance criteria

- ✅ Requirements 1–7 hold
- ✅ **No OpenTelemetry package reference.** `System.Diagnostics` and `System.Diagnostics.Metrics` only
- ✅ 🌟 **No unbounded metric labels.** Prove it: assert the number of distinct label combinations stays
  constant as the order count grows from 10 to 1,000
- ✅ Your tests use an `ActivityListener` / `MeterListener`, not an exporter

## Hints (read only when stuck)

<details>
<summary>Where does the order id go?</summary>

`activity?.SetTag("order.id", orderId)` — a span is a per-request record, so unique values are exactly
what it is for. The same value on a **metric label** would create one time series per order: 100,000
orders became 100,000 series in the reference demo, against 15 for `status` + `region`.
</details>

<details>
<summary>Why the `?.` on every activity call</summary>

`StartActivity` returns **null** when nothing is listening. That is the feature — instrumentation costs
nothing in tests and in libraries whose consumers never enable it. `activity?.SetTag(...)` is a no-op,
not a crash.
</details>

<details>
<summary>Requirement 7 — how do you test "no listener"?</summary>

`Assert.False(OrderTelemetry.Source.HasListeners())` before the call, and
`Assert.Null(Activity.Current)` after. Asserting only that the method returns the right number would
pass whether or not a listener existed — a test that cannot fail proves nothing.
</details>

<details>
<summary>Static fields and test isolation</summary>

`ActivitySource` and `Meter` are conventionally `static readonly` — one per library, created once. That
means listeners in different tests can see each other's activity. Keep each test's assertions scoped to
what it created (filter by operation name or tag), or run the class in its own xUnit collection.
</details>
