namespace Common.Application.Features.Monitoring.GetPendingFollowups;

public record PendingFollowupFilter(
    string[]? SlaStatus,
    string? Search,
    string? SortBy,
    string? SortDir,
    string[]? SlaBucket,
    string? Pic,
    string? PicType,
    string[]? Purpose,
    string[]? PropertyType,
    string[]? TaskType,
    string[]? AppraisalCompanyId
);
