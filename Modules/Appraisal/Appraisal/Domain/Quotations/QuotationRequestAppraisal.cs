namespace Appraisal.Domain.Quotations;

/// <summary>
/// Join entity representing an appraisal that belongs to a QuotationRequest (N:M).
/// An appraisal may be part of at most one non-terminal QuotationRequest at any time —
/// enforced via domain guard in QuotationRequest.AddAppraisal and the handler's
/// active-quotation uniqueness check.
/// </summary>
public class QuotationRequestAppraisal
{
    public Guid QuotationRequestId { get; private set; }
    public Guid AppraisalId { get; private set; }
    public DateTime AddedAt { get; private set; }
    public string AddedBy { get; private set; } = null!;

    // EF parameterless ctor
    private QuotationRequestAppraisal()
    {
    }

    public static QuotationRequestAppraisal Create(
        Guid quotationRequestId,
        Guid appraisalId,
        string addedBy)
    {
        return new QuotationRequestAppraisal
        {
            QuotationRequestId = quotationRequestId,
            AppraisalId = appraisalId,
            AddedAt = DateTime.UtcNow,
            AddedBy = addedBy
        };
    }
}
