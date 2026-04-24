using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SendQuotation;

/// <summary>
/// Transitions a Draft quotation to Sent status so invited companies receive invitations.
/// Admin-only. C8: Send is now an explicit step separate from Draft creation.
/// </summary>
public record SendQuotationCommand(Guid QuotationRequestId)
    : ICommand<SendQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
