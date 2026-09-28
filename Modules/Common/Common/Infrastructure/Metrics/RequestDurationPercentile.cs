namespace Common.Infrastructure.Metrics;

/// <summary>
/// Pure p95 computation used by SystemMetricsSampler — split out so it's unit-testable without the
/// sampler's MeterListener/BackgroundService/DB plumbing.
/// </summary>
public static class RequestDurationPercentile
{
    /// <summary>
    /// Nearest-rank 95th percentile, in milliseconds, of a set of request durations (in seconds).
    /// Null when there were no requests in the interval.
    /// </summary>
    public static int? P95Ms(IEnumerable<double> durationsSeconds)
    {
        var sortedMs = durationsSeconds.Select(s => s * 1000).OrderBy(ms => ms).ToList();
        if (sortedMs.Count == 0) return null;

        var index = Math.Clamp((int)Math.Ceiling(sortedMs.Count * 0.95) - 1, 0, sortedMs.Count - 1);
        return (int)Math.Round(sortedMs[index]);
    }
}
