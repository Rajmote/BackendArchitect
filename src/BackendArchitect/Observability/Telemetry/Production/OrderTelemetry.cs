using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BackendArchitect.Observability.Telemetry.Production;

// The production layer - and the surprise is that it has NO vendor dependency at all.
//
// ActivitySource and Meter live in System.Diagnostics: they ship with .NET, and they ARE the
// OpenTelemetry API for .NET (the projects merged). OpenTelemetry appears exactly once, in Program.cs,
// as the EXPORT layer:
//
//   builder.Services.AddOpenTelemetry()
//       .WithTracing(t => t.AddSource(OrderTelemetry.SourceName)
//                          .AddAspNetCoreInstrumentation()
//                          .AddHttpClientInstrumentation()
//                          .AddOtlpExporter())
//       .WithMetrics(m => m.AddMeter(OrderTelemetry.SourceName).AddOtlpExporter());
//
// So the domain references only the BCL, swapping Jaeger for Azure Monitor is a config change, and
// with no listener attached StartActivity returns null and the instrumentation costs nothing.
public static class OrderTelemetry
{
    public const string SourceName = "BackendArchitect.Orders";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> Processed =
        Meter.CreateCounter<long>("orders.processed", description: "Orders that reached a terminal state");

    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("orders.duration", unit: "ms", description: "Time to price an order");
}

/// <summary>Domain code, instrumented. Nothing here knows what a TracerProvider is.</summary>
public sealed class OrderPricer
{
    private readonly Func<int, double> _priceOf;

    public OrderPricer(Func<int, double> priceOf) => _priceOf = priceOf;

    public double Price(int orderId)
    {
        using var activity = OrderTelemetry.Source.StartActivity("price-order");
        activity?.SetTag("order.id", orderId);              // high cardinality is fine on a SPAN

        var clock = Stopwatch.StartNew();

        try
        {
            using (var lookup = OrderTelemetry.Source.StartActivity("fetch-price"))
            {
                lookup?.SetTag("db.system", "cosmosdb");
                var total = _priceOf(orderId);

                OrderTelemetry.Duration.Record(clock.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("status", "succeeded"));
                OrderTelemetry.Processed.Add(1,
                    new KeyValuePair<string, object?>("status", "succeeded"));   // bounded labels only

                return total;
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            OrderTelemetry.Processed.Add(1, new KeyValuePair<string, object?>("status", "failed"));
            throw;
        }
    }
}
