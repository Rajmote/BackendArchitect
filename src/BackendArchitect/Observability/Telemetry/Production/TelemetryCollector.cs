using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BackendArchitect.Observability.Telemetry.Production;

public sealed record CapturedMeasurement(string Instrument, double Value, string Labels);

// What OpenTelemetry's exporter does, in miniature: subscribe to the ActivitySource and the Meter and
// collect what they emit.
//
// Building it by hand shows why the instrumentation is free when nobody listens - and it is also how
// you unit-test spans and metrics without an exporter, a collector or a container.
public sealed class TelemetryCollector : IDisposable
{
    private readonly ActivityListener _activities;
    private readonly MeterListener _measurements;
    private readonly ConcurrentBag<Activity> _captured = [];
    private readonly ConcurrentBag<CapturedMeasurement> _values = [];

    public TelemetryCollector(string sourceName)
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _captured.Add(activity)
        };
        ActivitySource.AddActivityListener(_activities);

        _measurements = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == sourceName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _measurements.SetMeasurementEventCallback<long>(
            (instrument, value, tags, _) => _values.Add(Capture(instrument, value, tags)));
        _measurements.SetMeasurementEventCallback<double>(
            (instrument, value, tags, _) => _values.Add(Capture(instrument, value, tags)));
        _measurements.Start();
    }

    public IReadOnlyList<Activity> Activities => [.. _captured];

    public IReadOnlyList<CapturedMeasurement> Measurements => [.. _values];

    public IReadOnlyList<double> DurationsOf(string instrument) =>
        [.. _values.Where(m => m.Instrument == instrument).Select(m => m.Value)];

    private static CapturedMeasurement Capture<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        where T : struct
    {
        var labels = new List<string>();
        foreach (var tag in tags)
            labels.Add($"{tag.Key}={tag.Value}");

        return new CapturedMeasurement(instrument.Name, Convert.ToDouble(value), string.Join(",", labels));
    }

    public void Dispose()
    {
        _activities.Dispose();
        _measurements.Dispose();
    }
}
