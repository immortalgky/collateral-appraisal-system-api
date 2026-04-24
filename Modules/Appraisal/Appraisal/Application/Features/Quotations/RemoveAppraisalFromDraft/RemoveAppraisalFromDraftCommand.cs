using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.RemoveAppraisalFromDraft;

/// <summary>
/// Removes an appraisal from a Draft QuotationRequest.
/// If removing the last appraisal, the Draft is auto-cancelled by the domain method.
/// </summary>
public record RemoveAppraisalFromDraftCommand(
    Guid QuotationRequestId,
    Guid AppraisalId
) : ICommand<RemoveAppraisalFromDraftResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
