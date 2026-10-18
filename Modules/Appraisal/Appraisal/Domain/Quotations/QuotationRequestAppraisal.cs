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

    /// <summary>Assigned | Failed | Parked | Skipped. Null until finalize ran with auto-assign.</summary>
    public string? AutoAssignResult { get; private set; }
    public string? AutoAssignMessage { get; private set; }

    /// <summary>Stable code for the UI to translate; null for a plain Assigned result.</summary>
    public string? AutoAssignReasonCode { get; private set; }
    public DateTime? AutoAssignedAt { get; private set; }

    // EF parameterless ctor
    private QuotationRequestAppraisal()
    {
    }

    public static QuotationRequestAppraisal Create(
        Guid quotationRequestId,
        Guid appraisalId,
        string addedBy,
        DateTime addedAt)
    {
        return new QuotationRequestAppraisal
        {
            QuotationRequestId = quotationRequestId,
            AppraisalId = appraisalId,
            AddedAt = addedAt,
            AddedBy = addedBy
        };
    }

    public void RecordAutoAssign(string result, string? message, string? reasonCode, DateTime at)
    {
        AutoAssignResult = result;
        AutoAssignMessage = message;
        AutoAssignReasonCode = reasonCode;
        AutoAssignedAt = at;
    }
}
