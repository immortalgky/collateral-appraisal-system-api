namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsQuery(
    PaginationRequest PaginationRequest,
    Guid? AppraisalId = null,
    string[]? Statuses = null,
    string? QuotationNo = null,
    string? AppraisalNo = null,
    string? CustomerName = null,
    DateOnly? CutOffTimeFrom = null,
    DateOnly? CutOffTimeTo = null,
    Guid? CompanyId = null,
    string? SortBy = null,
    string? SortDir = null) : IQuery<GetQuotationsResult>;