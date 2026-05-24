using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

public record GetPendingEvaluationsSummaryQuery(
    PendingEvaluationFilter Filter
) : IQuery<MonitoringSummaryDto>;
