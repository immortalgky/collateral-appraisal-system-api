namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsQuery(
    PaginationRequest PaginationRequest,
    Guid? AppraisalId = null,
    string? Status = null,
    string? Search = null,
    DateOnly? CutOffTimeFrom = null,
    DateOnly? CutOffTimeTo = null) : IQuery<GetQuotationsResult>;