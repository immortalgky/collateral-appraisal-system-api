using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public record GetPendingQuotationsSummaryQuery(
    PendingQuotationFilter Filter
) : IQuery<MonitoringSummaryDto>;
