using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.EditDraftQuotation;

/// <summary>
/// Updates the DueDate and the set of invited companies on a Draft quotation.
/// Guards: status=Draft, caller owns the draft.
/// </summary>
public record EditDraftQuotationCommand(
    Guid QuotationRequestId,
    DateTime DueDate,
    IReadOnlyList<Guid> CompanyIds) : ICommand<EditDraftQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
