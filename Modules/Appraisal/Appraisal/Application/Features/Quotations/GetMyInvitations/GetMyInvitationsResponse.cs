namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

public record GetMyInvitationsResponse(PaginatedResult<MyInvitationDto> Invitations);
