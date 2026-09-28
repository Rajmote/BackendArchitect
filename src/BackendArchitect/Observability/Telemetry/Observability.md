# Observability · logs, metrics & traces

> **Where this sits:** Technology `Observability & Security` → Main topic `Observability` → Sub topic `Logs, metrics, traces & OpenTelemetry`.
> Runnable code: [`LatencyStats.cs`](LatencyStats.cs) · [`MetricRegistry.cs`](MetricRegistry.cs) ·
> [`SpanRecorder.cs`](SpanRecorder.cs) · [`Production/OrderTelemetry.cs`](Production/OrderTelemetry.cs) ·
> [`Production/TelemetryCollector.cs`](Production/TelemetryCollector.cs).
>
> Closes Month 3. Everything built in [§4](../../Concurrency) is invisible until this topic; the queue
> depth from [§4.5](../../Concurrency/Pipelines/Pipelines.md) becomes a metric, and each stage a span.

---

## The one sentence

> **Observability is the ability to answer questions about your system's behaviour *without shipping new
> code to answer them*.**

That last clause is the whole definition. If diagnosing an incident means adding a log line and
redeploying, you were not observable.

| | Answers | Example |
|---|---|---|
| **Monitoring** | questions you knew to ask | "is CPU above 80%?" |
| **Observability** | questions you didn't | "why were *these four* orders slow, and only for Belgian customers?" |

---

## 1. WHAT — the three pillars

```
LOGS       "what happened"       discrete events, rich detail
METRICS    "how much / how often" numbers over time, cheap, aggregatable
TRACES     "where did it go"     one request's path across services
```

Each is bad at what the others are good at:

| | Strength | Weakness |
|---|---|---|
| **Logs** | full detail of one event | expensive at volume, hard to aggregate |
| **Metrics** | cheap, always on, aggregate | **no detail** — cannot answer "which order?" |
| **Traces** | causality and timing across services | usually **sampled**, so not every request is kept |

> 🌟 **Metrics tell you *that* something is wrong. Traces tell you *where*. Logs tell you *why*.**

That is the incident workflow, in order — a dashboard alerts, you find the slow span, you read the
exception.

---

## 2. HOW — structured logging, the highest-value habit

```csharp
_logger.LogInformation($"Order {orderId} failed with {error}");      // ❌
_logger.LogInformation("Order {OrderId} failed with {Error}", orderId, error);   // ✅
```

The rendered message is **identical**. What differs is everything else:

```json
// ❌ interpolated                          // ✅ template
{                                           {
  "Message":                                  "Message": "Order 4471 failed with timeout",
    "Order 4471 failed with timeout"          "OrderId": 4471,
}                                             "Error": "timeout",
                                              "MessageTemplate": "Order {OrderId} failed with {Error}"
                                            }
```

**Three things the interpolated version loses:**

1. **Querying** — `OrderId = 4471` is an indexed field lookup; the other is a regex across terabytes.
2. **Grouping** — the preserved *template* gives every instance of that line one id, so "how often does
   this happen?" is a chart. Interpolated, every line is a unique string with nothing to group by.
3. **Cost when the level is off** — `$"..."` is evaluated *before* the call, so an expensive
   `LogDebug` formats its string, allocates, and is then thrown away on every request. The template
   form passes objects and only formats if someone is listening.

> 🌟 **Interpolation turns your data into text. The template keeps it as data — and defers the cost
> until someone is listening.**

### Levels, since they are routinely misused

| Level | Means | Wakes someone? |
|---|---|---|
| `Trace` / `Debug` | developer detail | no — usually off in production |
| `Information` | a business event happened | no |
| `Warning` | recoverable and unusual | no |
| `Error` | **this request failed** | maybe |
| `Critical` | **the service is failing** | yes |

> 🧠 A retry that eventually succeeded is `Information`, not `Error`. When `Error` is noisy people stop
> reading it — which is how real incidents get missed.

---

## 3. Metrics — three instruments, and one trap

```csharp
Counter<long>      ordersProcessed;   // only goes up        → "how many"
UpDownCounter<int> queueDepth;        // up and down         → "how many right now"
Histogram<double>  requestDuration;   // a distribution      → "how long, and how varied"
```

### 🌟 Averages lie

1,000 requests: 985 take 10 ms, 15 take 2,000 ms. Measured:

```
  average :   39.85 ms   <- inside a 100ms SLA, so nobody investigates
  p50     :   10.00 ms
  p95     :   10.00 ms
  p99     : 2000.00 ms   <- 1 customer in 100 waits two seconds
```

The average is **39.85 ms and it describes nobody** — every single sample is either 4× below it or 50×
above it. There is no "10 slow requests" anywhere on that dashboard: the average absorbed them, so
nobody has any reason to go looking.

> 🌟 **p50 is the typical experience. p99 is the experience that makes people leave.** Alert on p99.

Two things worth carrying:

- **At scale p99 is not rare.** 1% of 90,000 requests/day is **900 unhappy customers every day** — and
  your heaviest users make the most requests, so the tail lands hardest on your best customers.
- **Percentiles need a histogram.** You cannot compute one from an average, and you cannot average
  percentiles across replicas (the mean of three p99s is not the p99). That is why `Histogram<double>`
  keeps buckets and lets the backend do the maths.

⚠️ Percentiles are also sensitive to rank. With 990 fast / 10 slow, the nearest-rank p99 lands *exactly*
on the boundary and reports 10 ms. The demo uses 985 / 15 so it falls clearly inside the slow group —
worth knowing before you argue with a dashboard.

### ⚠️ Cardinality — the mistake that costs real money

Every distinct **combination of label values** becomes its own time series, stored for the full
retention period and scanned by every query. Measured, 100,000 orders:

```
  order_id (unbounded)      : 100,000 series
  status + region (bounded) :      15 series
```

A million orders is a million series: a monitoring bill that dwarfs the database and queries that time
out. And the metric is **useless as well as expensive** — a counter that only ever reaches 1 cannot be
charted, alerted on, or aggregated.

> 🌟 **Labels must be low-cardinality and bounded** — status, region, endpoint, error type. Something
> whose complete list you could write down.
> **Never ids, emails, URLs with parameters, or anything unbounded.**

| Question | Pillar |
|---|---|
| "how many orders failed?" | **metric** |
| "which order failed?" | **trace / log** |

> 🧠 **If a label answers "which one?", it does not belong on a metric.**

### What to measure — RED

| | |
|---|---|
| **R**ate | requests per second |
| **E**rrors | how many failed |
| **D**uration | the histogram → p50/p95/p99 |

For the §4.5 pipeline, add **queue depth per stage** — you already know it locates the bottleneck for
free.

---

## 4. Traces — causality across boundaries

A **span** is one timed operation; a **trace** is a tree of spans sharing one **trace id**:

```
trace 4bf92f…                                   ├──────────── 320ms ────────────┤
  └─ POST /orders                               ├────────── 318ms ──────────┤
       ├─ validate                              ├─2ms─┤
       ├─ price                                       ├──── 210ms ────┤
       │    └─ GET pricing-service/quote               ├─── 205ms ───┤    ← the culprit
       └─ store                                                       ├─ 90ms ─┤
```

| Field | Meaning |
|---|---|
| **trace id** | the same for every span in the request |
| **span id** | this operation |
| **parent span id** | who called me → this is what makes it a *tree* |
| attributes | `http.method`, `db.system`, and your own |

### Context propagation

Crossing a process boundary is nothing more than passing the current context in a header — the W3C
`traceparent`:

```
traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
             ^^ ^-------- trace id -----------^ ^--- span id --^ ^^ flags
                                                 becomes the callee's PARENT
```

`HttpClient` sends it automatically and ASP.NET Core reads it automatically, which is why traces span a
whole system with no propagation code of your own. Drop it and the far side cannot know it was called:

```
  traceparent propagated  : 1 trace(s), 1 root span(s)
  traceparent dropped     : 2 trace(s), 2 root span(s)
```

**Same five spans, same work — now two unrelated traces that nobody can join up afterwards.**
One request should produce exactly **one root span**; a second root is the signature of a severed trace.

### ⚠️ What actually breaks it

> 🧠 **Trace propagation is a header convention, not a transport feature.** HTTP/1.1, HTTP/2, gRPC,
> Service Bus all carry `traceparent`. Changing protocol never fixes context loss.

In the order worth checking:

1. **`Activity.Current` was null when the call was made** — context lost across a thread boundary: a
   fire-and-forget `Task.Run`, a manually started `Thread`, a background queue picking work up later.
   *(Every pipeline worker in §4.5 has this hazard.)*
2. **The outgoing handler chain lost its defaults** — .NET injects the header from inside
   `SocketsHttpHandler`; replace or reconfigure the primary handler carelessly and the request still
   works perfectly, it just travels anonymously.
3. **The receiver is not reading it** — propagation is two-sided.
4. **Something in between stripped it** — a gateway, proxy or mesh dropping unknown headers.
5. **Propagation switched off** — `DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION=0`.

**Confirming it takes 30 seconds:** log `Activity.Current?.Id` just before the call, and the incoming
`traceparent` on arrival. Whichever is null tells you which side to fix.

⚠️ **Sampling.** Tracing every request is expensive, so typically 1–10% are kept — which means the trace
for the *specific* failed request may not exist. Tail-based sampling (decide after the request
finishes; keep all errors and slow requests) is the usual answer.

---

## 5. 🌟 Correlation — what welds the three pillars into one tool

**Put the trace id in every log line.**

```json
{ "Message": "Order 4471 priced", "OrderId": 4471, "TraceId": "4bf92f3577b34da6…" }
```

```
1. p99 alert fires                  (metric)
2. find a slow trace                (trace)  → "pricing-service took 205ms"
3. filter logs by that trace id     (logs)   → "connection pool exhausted"
```

Three seconds instead of three hours.

> 🌟 **Without the trace id in the logs you have three disconnected tools. With it, you have one.**

---

## 6. The production layer — and the surprise

The hand-rolled layer and the production layer are **the same instrumentation code**, because .NET's
diagnostics types *are* the OpenTelemetry API (the projects merged).

```csharp
using System.Diagnostics;            // BCL — ships with .NET
using System.Diagnostics.Metrics;    // BCL — ships with .NET

public static class OrderTelemetry
{
    public const string SourceName = "BackendArchitect.Orders";

    public static readonly ActivitySource Source = new(SourceName);
    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> Processed = Meter.CreateCounter<long>("orders.processed");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("orders.duration", "ms");
}
```

**That code has no OpenTelemetry dependency.** OpenTelemetry appears exactly once, in `Program.cs`, as
the *export* layer:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(OrderTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(OrderTelemetry.SourceName)
        .AddOtlpExporter());
```

### Why the type matters architecturally

A `TracerProvider` injected into a domain class would be an OpenTelemetry dependency in your domain
assembly. `ActivitySource` is not:

| | `TracerProvider` (❌) | `ActivitySource` (✅) |
|---|---|---|
| Dependency | domain → OpenTelemetry | domain → BCL only |
| Swapping vendors | touches the domain | one file |
| Cost with no listener | a dependency you must satisfy everywhere | `StartActivity` returns **null** |
| Testing | needs an exporter | attach an `ActivityListener` |

Measured — the identical domain code, run 100 times with no listener and 100 times with one:

```
  spans captured before the listener existed : 0 (StartActivity returned null)
  spans captured after                       : 200 (100 price-order + 100 fetch-price)
  every fetch-price has price-order as parent: True
  measurements captured                      : 200
```

> 🌟 **Instrument with the BCL; export with OpenTelemetry.** Instrumentation belongs *in* the domain,
> where the interesting operations are. The vendor belongs in `Program.cs`.
>
> ⚠️ Moving instrumentation *out* of the domain to keep it clean is the wrong fix: you end up with a
> complete trace that is **empty** — `POST /orders took 300ms` and no idea why.

[`TelemetryCollector.cs`](Production/TelemetryCollector.cs) is what an exporter does, in miniature: an
`ActivityListener` plus a `MeterListener`. It is also how you unit-test spans and metrics with no
exporter, no collector and no container — see
[`ObservabilityTests.cs`](../../../../tests/BackendArchitect.Tests/Observability/ObservabilityTests.cs).

---

## 7. WHO decides — the architect's view

- **Instrument at boundaries first**: inbound requests, outbound calls, database, queue. That is 80% of
  the value for 20% of the spans
- **Own the naming.** `orders.processed`, not `OrdersProcessedCounter`. Follow the OpenTelemetry
  semantic conventions (`http.*`, `db.*`, `messaging.*`) so off-the-shelf dashboards just work
- **Budget cardinality deliberately.** It is a cost decision, and nobody else will make it
- **Correlate or don't bother** — the trace id in the logs is what turns three tools into one
- **Sampling is a policy, not a default.** Decide what you keep *before* an incident
- **Telemetry is a public contract.** Dashboards and alerts depend on metric names; renaming one is a
  breaking change, exactly like renaming a REST field ([§3.2](../../Apis/Rest/Design/RestDesign.md))

> 🌟 **If you cannot answer a new question about production without deploying code, you are not
> observable — you are just logging.**

---

## 8. Demo output

```
1,000 requests: 985 take 10ms, 15 take 2000ms
  average :   39.85 ms   <- inside a 100ms SLA, so nobody investigates
  p50     :   10.00 ms
  p95     :   10.00 ms
  p99     : 2000.00 ms   <- 1 customer in 100 waits two seconds

100,000 measurements, and how many time series each labelling creates:
  order_id (unbounded)      : 100,000 series
  status + region (bounded) :      15 series

Service A calls service B, 5 spans in total:
  traceparent propagated  : 1 trace(s), 1 root span(s)
  traceparent dropped     : 2 trace(s), 2 root span(s)

100 orders priced with NO listener, then 100 with one attached:
  spans captured before the listener existed : 0 (StartActivity returned null)
  spans captured after                       : 200 (100 price-order + 100 fetch-price)
  every fetch-price has price-order as parent: True
  measurements captured                      : 200
```

---

## 9. Warm-up questions

1. Both log forms render the same text. Name three things interpolation loses.
2. Average 39.85 ms, SLA 100 ms, and 15 customers waiting two seconds. What is wrong with the dashboard?
3. Why did labelling a counter by `order_id` produce a 40× monitoring bill — and why is the metric
   useless too?
4. One request, two traces. Name the five things to check, in order.
5. Why is "switch to gRPC" not a fix for a broken trace?
6. A colleague injects `TracerProvider` into a domain class. What do you suggest, and what four things
   does it buy?
7. Where does the trace id have to appear for the three pillars to become one tool?

---

## 10. The three to remember

1. 🌟 **Metrics say *that*, traces say *where*, logs say *why*** — and the trace id in the log line
   welds them together
2. 🌟 **Averages hide outliers; labels must be bounded.** "Which one?" is never a metric question
3. 🌟 **Instrument with the BCL, export with OpenTelemetry** — the domain never names a vendor
