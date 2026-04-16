namespace Common.Application.Features.Dashboard.ReconcileAppraisalCounts;

public record ReconcileAppraisalCountsRequest(
    DateOnly? FromDate,
    DateOnly? ToDate
);
