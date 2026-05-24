using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingFollowups;

public record GetPendingFollowupsSummaryQuery(
    PendingFollowupFilter Filter
) : IQuery<MonitoringSummaryDto>;
