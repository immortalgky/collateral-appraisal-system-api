using Shared.CQRS;

namespace Common.Application.Features.Dashboard.ReconcileAppraisalCounts;

public record ReconcileAppraisalCountsCommand(
    DateOnly? FromDate,
    DateOnly? ToDate
) : ICommand<ReconcileAppraisalCountsResult>;

public record ReconcileAppraisalCountsResult(
    int ReconciledDays,
    DateOnly From,
    DateOnly To
);
