using System.Collections.Concurrent;
using System.Diagnostics;

namespace BackendArchitect.Observability.Telemetry;

public sealed record RecordedSpan(string TraceId, string SpanId, string? ParentSpanId, string Name, double DurationMs);

public sealed record TraceReport(string Scenario, int DistinctTraces, int Spans, int RootSpans);

// A hand-rolled tracer, so the mechanics are visible before the real API hides them.
//
//   span   = one timed operation
//   trace  = a tree of spans sharing one TRACE ID
//   parent = who called me, which is what makes it a tree rather than a list
//
// Crossing a process boundary is nothing more than passing the current context in a header. The W3C
// standard spells it:
//
//   traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
//                ^^ ^-------- trace id -----------^ ^--- span id --^ ^^ flags
//
// Drop that header and the far side cannot know it was called: it starts a brand new root trace, and
// one request becomes two unrelated traces that nobody can join up afterwards.
public sealed class SpanRecorder
{
    private readonly AsyncLocal<(string TraceId, string SpanId)?> _current = new();
    private readonly ConcurrentBag<RecordedSpan> _spans = [];

    public IReadOnlyList<RecordedSpan> Spans => [.. _spans];

    public string? CurrentTraceparent =>
        _current.Value is { } context ? $"00-{context.TraceId}-{context.SpanId}-01" : null;

    /// <summary>Adopts an incoming traceparent, so spans started here join the caller's trace.</summary>
    public void Adopt(string? traceparent)
    {
        if (traceparent is null)
            return;

        var parts = traceparent.Split('-');
        if (parts.Length == 4)
            _current.Value = (parts[1], parts[2]);
    }

    public IDisposable Start(string name)
    {
        var parent = _current.Value;
        var traceId = parent?.TraceId ?? NewId(32);
        var spanId = NewId(16);

        _current.Value = (traceId, spanId);
        var clock = Stopwatch.StartNew();

        return new Scope(() =>
        {
            _spans.Add(new RecordedSpan(traceId, spanId, parent?.SpanId, name, clock.Elapsed.TotalMilliseconds));
            _current.Value = parent;
        });
    }

    /// <summary>
    /// Service A handles a request and calls service B. With <paramref name="propagate"/> off, the
    /// header is dropped and B starts its own root trace.
    /// </summary>
    public static TraceReport CallAcrossServices(bool propagate)
    {
        var serviceA = new SpanRecorder();
        var serviceB = new SpanRecorder();

        using (serviceA.Start("POST /orders"))
        {
            using (serviceA.Start("validate")) { }

            using (serviceA.Start("price"))
            {
                var header = serviceA.CurrentTraceparent;

                serviceB.Adopt(propagate ? header : null);
                using (serviceB.Start("GET /quote"))
                using (serviceB.Start("db.query")) { }
            }
        }

        var all = serviceA.Spans.Concat(serviceB.Spans).ToArray();

        // A root span has no parent. One request should produce exactly ONE - a second root means the
        // far side never learned it was called.
        var roots = all.Count(span => span.ParentSpanId is null);

        return new TraceReport(
            propagate ? "traceparent propagated" : "traceparent dropped",
            all.Select(span => span.TraceId).Distinct().Count(),
            all.Length,
            roots);
    }

    private static string NewId(int hexChars) =>
        Convert.ToHexStringLower(RandomNumberGeneratorBytes(hexChars / 2));

    private static byte[] RandomNumberGeneratorBytes(int count)
    {
        var bytes = new byte[count];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
