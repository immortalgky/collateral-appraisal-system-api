using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingExternal;

public record GetPendingExternalSummaryQuery(
    PendingExternalFilter Filter
) : IQuery<MonitoringSummaryDto>;
