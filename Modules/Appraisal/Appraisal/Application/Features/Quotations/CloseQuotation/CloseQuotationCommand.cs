using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.CloseQuotation;

/// <summary>
/// Closes a QuotationRequest for new submissions (Sent → UnderAdminReview). Idempotent.
/// Can be called by Admin or the QuotationAutoCloseService (SYSTEM).
/// </summary>
public record CloseQuotationCommand(Guid QuotationRequestId)
    : ICommand<CloseQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
