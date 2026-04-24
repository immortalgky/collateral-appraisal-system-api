namespace Appraisal.Application.Features.Quotations.SubmitQuotation;

public record SubmitQuotationItemBody(
    Guid QuotationRequestItemId,
    Guid AppraisalId,
    int ItemNumber,
    decimal QuotedPrice,
    int EstimatedDays,
    // Optional — Checker path sends these to preserve the draft's fee breakdown.
    decimal? FeeAmount = null,
    decimal? Discount = null,
    decimal? NegotiatedDiscount = null,
    decimal? VatPercent = null);

public record SubmitQuotationRequest(
    string QuotationNumber,
    int EstimatedDays,
    List<SubmitQuotationItemBody> Items,
    DateTime? ValidUntil = null,
    DateTime? ProposedStartDate = null,
    DateTime? ProposedCompletionDate = null,
    string? Remarks = null,
    string? TermsAndConditions = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null);
