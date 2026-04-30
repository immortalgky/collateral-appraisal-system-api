namespace Appraisal.Application.Features.Quotations.RemoveAppraisalFromDraft;

/// <summary>
/// Returned after removing an appraisal from a Draft quotation.
/// <see cref="Status"/> will be "Cancelled" when the removal triggered auto-cancel (last appraisal removed).
/// </summary>
public record RemoveAppraisalFromDraftResult(
    Guid QuotationRequestId,
    int TotalAppraisals,
    string Status);
