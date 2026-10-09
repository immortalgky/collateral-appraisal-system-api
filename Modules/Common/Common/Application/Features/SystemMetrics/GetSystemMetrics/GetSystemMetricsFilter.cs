namespace Common.Application.Features.SystemMetrics.GetSystemMetrics;

public record GetSystemMetricsFilter(DateTime? From, DateTime? To, int? Buckets);
