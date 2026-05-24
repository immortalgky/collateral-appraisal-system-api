using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingInternal;

public record GetPendingInternalSummaryQuery(
    PendingInternalFilter Filter
) : IQuery<MonitoringSummaryDto>;
