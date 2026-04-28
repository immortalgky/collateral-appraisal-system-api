using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SaveDraftQuotation;

public record SaveDraftQuotationItem(
    Guid QuotationRequestItemId,
    Guid AppraisalId,
    int ItemNumber,
    decimal FeeAmount,
    decimal Discount,
    decimal? NegotiatedDiscount,
    decimal VatPercent,
    int EstimatedDays,
    string? ItemNotes = null);

public record SaveDraftQuotationCommand(
    Guid QuotationRequestId,
    Guid CompanyId,
    string QuotationNumber,
    int EstimatedDays,
    List<SaveDraftQuotationItem> Items,
    DateTime? ValidUntil = null,
    DateTime? ProposedStartDate = null,
    DateTime? ProposedCompletionDate = null,
    string? Remarks = null,
    string? TermsAndConditions = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null)
    : ICommand<SaveDraftQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
