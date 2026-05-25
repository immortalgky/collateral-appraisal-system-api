namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

public record GetMyInvitationsQuery(
    PaginationRequest PaginationRequest,
    string? Status = null,
    string? Search = null,
    DateOnly? CutOffTimeFrom = null,
    DateOnly? CutOffTimeTo = null) : IQuery<GetMyInvitationsResult>;
