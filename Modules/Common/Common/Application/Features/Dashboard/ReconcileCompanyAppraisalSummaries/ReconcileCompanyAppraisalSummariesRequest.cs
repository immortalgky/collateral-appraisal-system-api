namespace Common.Application.Features.Dashboard.ReconcileCompanyAppraisalSummaries;

public record ReconcileCompanyAppraisalSummariesRequest(
    DateOnly? FromDate,
    DateOnly? ToDate
);
