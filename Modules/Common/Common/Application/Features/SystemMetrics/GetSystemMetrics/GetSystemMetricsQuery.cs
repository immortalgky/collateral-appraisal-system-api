using Shared.CQRS;

namespace Common.Application.Features.SystemMetrics.GetSystemMetrics;

public record GetSystemMetricsQuery(GetSystemMetricsFilter Filter) : IQuery<SystemMetricsDto>;
