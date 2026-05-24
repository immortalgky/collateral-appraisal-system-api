namespace Common.Application.Features.Monitoring.GetPendingEvaluations;

public record PendingEvaluationFilter(
    string[]? EvaluationStatus,
    string? Search,
    string? SortBy,
    string? SortDir,
    string? AppraisalCompanyId,
    string[]? AppraisalStatus
);
