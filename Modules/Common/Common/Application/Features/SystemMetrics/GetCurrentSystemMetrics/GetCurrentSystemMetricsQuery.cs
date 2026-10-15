using Shared.CQRS;

namespace Common.Application.Features.SystemMetrics.GetCurrentSystemMetrics;

public record GetCurrentSystemMetricsQuery : IQuery<IReadOnlyList<SystemMetricsCurrentDto>>;
