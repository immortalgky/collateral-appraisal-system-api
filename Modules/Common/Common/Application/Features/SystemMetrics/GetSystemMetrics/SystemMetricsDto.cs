namespace Common.Application.Features.SystemMetrics.GetSystemMetrics;

public record SystemMetricsPoint(
    DateTime Start,
    double? CpuPercent,
    double? MachineMemoryPercent,
    int? ThreadPoolQueue,
    int? P95Ms,
    double? RequestsPerMin,
    int? Http5xxPerMin
);

public record SystemMetricsMachine(string MachineName, IReadOnlyList<SystemMetricsPoint> Points);

public record SystemMetricsRestart(string MachineName, DateTime At);

public record SystemMetricsDto(
    int BucketSeconds,
    IReadOnlyList<SystemMetricsMachine> Machines,
    IReadOnlyList<SystemMetricsRestart> Restarts
);
