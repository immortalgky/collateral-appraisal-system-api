using Shared.CQRS;

namespace Common.Application.Features.Dashboard.ReconcileCompanyAppraisalSummaries;

public record ReconcileCompanyAppraisalSummariesCommand(
    DateOnly? FromDate,
    DateOnly? ToDate
) : ICommand<ReconcileCompanyAppraisalSummariesResult>;

public record ReconcileCompanyAppraisalSummariesResult(
    int ReconciledRows,
    DateOnly From,
    DateOnly To
);
