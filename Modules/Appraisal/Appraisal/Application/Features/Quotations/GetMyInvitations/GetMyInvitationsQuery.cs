namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

public record GetMyInvitationsQuery(
    PaginationRequest PaginationRequest,
    string[]? Statuses = null,
    string? QuotationNo = null,
    string? AppraisalNo = null,
    string? CustomerName = null,
    DateOnly? CutOffTimeFrom = null,
    DateOnly? CutOffTimeTo = null,
    string? SortBy = null,
    string? SortDir = null) : IQuery<GetMyInvitationsResult>;
