namespace Common.Application.Features.SystemMetrics.GetCurrentSystemMetrics;

/// <summary>
/// Positional record — column order must match the SELECT list in
/// GetCurrentSystemMetricsQueryHandler.
/// </summary>
public record SystemMetricsCurrentDto(
    string MachineName,
    DateTime TimeStamp,
    DateTime ProcessStartedAt,
    decimal CpuPercent,
    decimal MachineMemoryPercent,
    int WorkingSetMb,
    int GcHeapMb,
    int Gen2PerHour,
    int ThreadCount,
    int ThreadPoolQueue,
    int RequestsPerMin,
    int Http5xxPerMin,
    int? P95Ms,
    int ExceptionsPerMin
);
