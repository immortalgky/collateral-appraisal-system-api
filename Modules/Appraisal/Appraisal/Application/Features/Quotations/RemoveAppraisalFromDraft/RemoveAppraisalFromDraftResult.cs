namespace Appraisal.Application.Features.Quotations.RemoveAppraisalFromDraft;

public record RemoveAppraisalFromDraftResult(
    Guid QuotationRequestId,
    int TotalAppraisals,
    /// <summary>True if the quotation was auto-cancelled because no appraisals remain.</summary>
    bool AutoCancelled);
