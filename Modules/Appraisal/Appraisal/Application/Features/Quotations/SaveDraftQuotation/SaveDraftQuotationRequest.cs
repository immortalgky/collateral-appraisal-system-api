namespace Appraisal.Application.Features.Quotations.SaveDraftQuotation;

public record SaveDraftQuotationItemBody(
    Guid QuotationRequestItemId,
    Guid AppraisalId,
    int ItemNumber,
    decimal FeeAmount,
    decimal Discount,
    decimal? NegotiatedDiscount,
    decimal VatPercent,
    int EstimatedDays,
    string? ItemNotes = null);

public record SaveDraftQuotationRequest(
    string QuotationNumber,
    int EstimatedDays,
    List<SaveDraftQuotationItemBody> Items,
    DateTime? ValidUntil = null,
    DateTime? ProposedStartDate = null,
    DateTime? ProposedCompletionDate = null,
    string? Remarks = null,
    string? TermsAndConditions = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null);
