using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

public record GetPendingInternalGroupedQuery(
    string GroupBy,
    PendingInternalFilter Filter
) : IQuery<MonitoringGroupedResult>;
