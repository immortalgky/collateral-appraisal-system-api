using Shared.CQRS;

namespace Common.Application.Features.Dashboard.GetQuotationTaskSummary;

public record GetQuotationTaskSummaryQuery : IQuery<GetQuotationTaskSummaryResult>;

public record GetQuotationTaskSummaryResult(
    int PendingQuotationCreation,
    int WaitingCompanySubmission,
    int WaitingRmSelection
);
