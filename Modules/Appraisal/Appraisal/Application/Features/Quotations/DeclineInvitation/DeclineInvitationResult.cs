namespace Appraisal.Application.Features.Quotations.DeclineInvitation;

public record DeclineInvitationResult(
    Guid QuotationRequestId,
    Guid CompanyId,
    string Status,
    /// <summary>True if declining triggered an early close of the quotation.</summary>
    bool EarlyCloseFired);
