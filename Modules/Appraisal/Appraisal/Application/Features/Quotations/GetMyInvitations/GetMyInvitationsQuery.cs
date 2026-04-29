namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

public record GetMyInvitationsQuery(
    PaginationRequest PaginationRequest,
    string? Status = null,
    string? Search = null,
    DateOnly? DueDateFrom = null,
    DateOnly? DueDateTo = null) : IQuery<GetMyInvitationsResult>;
