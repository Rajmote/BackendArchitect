using System.Collections.Concurrent;

namespace BackendArchitect.Observability.Telemetry;

public sealed record SeriesReport(string Strategy, int Measurements, int DistinctSeries);

// A metric is not one number. Every distinct COMBINATION of label values becomes its own time series,
// stored for the whole retention period and scanned by every query that touches the metric.
//
// Put an order id on a label and a million orders becomes a million series: a monitoring bill that
// dwarfs the database, queries that time out, and counters that only ever reach 1 - useless as well as
// expensive.
//
// "Which one?" is a question for traces and logs. Metrics only ever answer "how many".
public sealed class MetricRegistry
{
    private readonly ConcurrentDictionary<string, long> _series = new();

    public int SeriesCount => _series.Count;

    public void Record(string name, params (string Key, string Value)[] labels)
    {
        var key = $"{name}{{{string.Join(",", labels.OrderBy(l => l.Key).Select(l => $"{l.Key}={l.Value}"))}}}";
        _series.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    /// <summary>Labelling by order id - unbounded, one series per order.</summary>
    public static SeriesReport LabelledByOrderId(int orders)
    {
        var registry = new MetricRegistry();
        for (var order = 0; order < orders; order++)
            registry.Record("orders.processed", ("order_id", order.ToString()));

        return new SeriesReport("order_id (unbounded)", orders, registry.SeriesCount);
    }

    /// <summary>Labelling by status and region - bounded, and you can write the full list down.</summary>
    public static SeriesReport LabelledByStatusAndRegion(int orders)
    {
        string[] statuses = ["succeeded", "failed", "rejected"];
        string[] regions = ["eu-west", "eu-north", "us-east", "us-west", "ap-south"];

        var registry = new MetricRegistry();
        for (var order = 0; order < orders; order++)
            registry.Record("orders.processed",
                ("status", statuses[order % statuses.Length]),
                ("region", regions[order % regions.Length]));

        return new SeriesReport("status + region (bounded)", orders, registry.SeriesCount);
    }
}
