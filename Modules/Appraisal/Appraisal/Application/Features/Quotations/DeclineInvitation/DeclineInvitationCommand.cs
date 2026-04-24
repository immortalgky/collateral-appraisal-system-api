using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.DeclineInvitation;

/// <summary>
/// Ext-company user declines their quotation invitation.
/// Creates or transitions a CompanyQuotation to Declined status.
/// Triggers early-close check on the parent QuotationRequest.
/// </summary>
public record DeclineInvitationCommand(
    Guid QuotationRequestId,
    Guid CompanyId,
    string Reason
) : ICommand<DeclineInvitationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
