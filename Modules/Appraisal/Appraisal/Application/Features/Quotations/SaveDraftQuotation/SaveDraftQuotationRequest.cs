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
    List<SaveDraftQuotationItemBody> Items,
    DateTime? ValidUntil = null,
    DateTime? ProposedStartDate = null,
    DateTime? ProposedCompletionDate = null,
    string? Remarks = null,
    string? TermsAndConditions = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    // When true, the company is opting out ("not participate"). Pricing is ignored/cleared and
    // DeclineReason is persisted as the marker; the flow still goes through Send-to-Checker → Submit.
    bool NotParticipating = false,
    string? DeclineReason = null);
