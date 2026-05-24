namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public record PendingQuotationFilter(
    string[]? Status,
    string? Search,
    string? SortBy,
    string? SortDir,
    DateOnly? CutOffTimeFrom = null,
    DateOnly? CutOffTimeTo = null
);
