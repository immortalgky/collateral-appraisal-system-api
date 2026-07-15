namespace Common.Application.Features.Monitoring.GetPendingExternal;

public record PendingExternalFilter(
    string[]? SlaStatus,
    string? Search,
    string[]? ActivityId,
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
