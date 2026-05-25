namespace Common.Application.Features.Monitoring.GetPendingInternal;

public record PendingInternalFilter(
    string[]? SlaStatus,
    string? Search,
    string[]? ActivityId,
    string? SortBy,
    string? SortDir,
    string[]? SlaBucket,
    string? Pic,
    string[]? Purpose,
    string[]? PropertyType,
    string[]? TaskType
);
