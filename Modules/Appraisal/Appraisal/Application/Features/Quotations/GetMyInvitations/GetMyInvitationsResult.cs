namespace Appraisal.Application.Features.Quotations.GetMyInvitations;

public record GetMyInvitationsResult(PaginatedResult<MyInvitationDto> Invitations);

public record MyInvitationDto(
    Guid Id,
    string QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string RequestedBy,
    int TotalAppraisals,
    Guid CompanyId,
    DateTime ReceivedAt,
    decimal? TotalFeeAmount,
    DateTime? QuotedAt,
    string? QuotedBy,
    string CompanyStatus,
    bool HasSubmitted);
