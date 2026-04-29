using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.EditDraftQuotation;

/// <summary>
/// Updates the DueDate, the set of invited companies, and per-appraisal MaxAppraisalDays on a Draft quotation.
/// Guards: status=Draft, caller owns the draft.
/// </summary>
public record EditDraftQuotationCommand(
    Guid QuotationRequestId,
    DateTime DueDate,
    IReadOnlyList<Guid> CompanyIds,
    IReadOnlyList<EditDraftAppraisalEntry> Appraisals) : ICommand<EditDraftQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record EditDraftAppraisalEntry(Guid AppraisalId, int? MaxAppraisalDays);
