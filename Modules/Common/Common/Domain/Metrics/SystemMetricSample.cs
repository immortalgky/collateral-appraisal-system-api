namespace Common.Domain.Metrics;

/// <summary>
/// One row per app instance per minute, written by SystemMetricsSampler via raw Dapper (not EF —
/// same pattern as Log.cs). EF only owns the schema, via SystemMetricSampleConfiguration.
/// </summary>
public class SystemMetricSample
{
    public long Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public string MachineName { get; set; } = null!;
    public DateTime ProcessStartedAt { get; set; }
    public decimal CpuPercent { get; set; }
    public decimal MachineMemoryPercent { get; set; }
    public int WorkingSetMb { get; set; }
    public int GcHeapMb { get; set; }
    public int Gen2Collections { get; set; }
    public int ThreadCount { get; set; }
    public int ThreadPoolQueue { get; set; }
    public int RequestsPerMin { get; set; }
    public int Http5xxPerMin { get; set; }
    public int? P95Ms { get; set; }
    public int ExceptionsPerMin { get; set; }
}
