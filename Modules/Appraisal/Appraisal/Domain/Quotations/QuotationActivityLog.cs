namespace Appraisal.Domain.Quotations;

/// <summary>
/// Append-only audit trail of quotation lifecycle activities.
/// One row per activity; no updates, no deletes.
/// </summary>
public class QuotationActivityLog : Entity<Guid>
{
    public Guid QuotationRequestId { get; private set; }
    public Guid? CompanyQuotationId { get; private set; }
    public Guid? CompanyId { get; private set; }

    public string ActivityName { get; private set; } = null!;
    public DateTime ActionAt { get; private set; }
    public string ActionBy { get; private set; } = null!;
    public string? ActionByRole { get; private set; }
    public string? Remark { get; private set; }

    private QuotationActivityLog() { }

    public static QuotationActivityLog Create(
        Guid quotationRequestId,
        Guid? companyQuotationId,
        Guid? companyId,
        string activityName,
        string actionBy,
        string? actionByRole = null,
        string? remark = null)
    {
        return new QuotationActivityLog
        {
            Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            CompanyQuotationId = companyQuotationId,
            CompanyId = companyId,
            ActivityName = activityName,
            ActionAt = DateTime.UtcNow,
            ActionBy = actionBy,
            ActionByRole = actionByRole,
            Remark = remark
        };
    }
}
