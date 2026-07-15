namespace Common.Application.Features.Monitoring.GetPendingQuotations;

public record PendingQuotationFilter(
    string[]? Status,
    string? QuotationNo,
    string? AppraisalNo,
    string? CustomerName,
    string? SortBy,
    string? SortDir,
    DateOnly? CutOffTimeFrom = null,
    DateOnly? CutOffTimeTo = null,
    string? AppraisalCompanyId = null
);
