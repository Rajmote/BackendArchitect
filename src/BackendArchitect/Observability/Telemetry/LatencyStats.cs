namespace BackendArchitect.Observability.Telemetry;

public sealed record LatencySummary(int Count, double AverageMs, double P50Ms, double P95Ms, double P99Ms, double MaxMs);

// An average is a single number that describes nobody.
//
// It cannot tell you that a small group of requests is having a terrible time, because it has averaged
// them into invisibility. Percentiles keep them: p50 is the typical experience, p99 is the experience
// that makes customers leave.
public static class LatencyStats
{
    public static LatencySummary Summarize(IReadOnlyList<double> samplesMs)
    {
        ArgumentOutOfRangeException.ThrowIfZero(samplesMs.Count);

        var sorted = samplesMs.Order().ToArray();

        return new LatencySummary(
            sorted.Length,
            sorted.Average(),
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            Percentile(sorted, 0.99),
            sorted[^1]);
    }

    // Nearest-rank: the smallest value at or below which `fraction` of the samples fall.
    public static double Percentile(IReadOnlyList<double> sorted, double fraction)
    {
        var rank = (int)Math.Ceiling(fraction * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }

    /// <summary>A mostly-fast service with a small slow tail - the shape every real service has.</summary>
    public static IReadOnlyList<double> FastWithASlowTail(int total, int slowCount, double fastMs, double slowMs) =>
        [.. Enumerable.Repeat(fastMs, total - slowCount), .. Enumerable.Repeat(slowMs, slowCount)];
}
