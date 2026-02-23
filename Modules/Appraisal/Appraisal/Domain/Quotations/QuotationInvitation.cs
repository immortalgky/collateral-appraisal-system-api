namespace Appraisal.Domain.Quotations;

/// <summary>
/// Tracks companies invited to submit quotations for an RFQ.
/// </summary>
public class QuotationInvitation : Entity<Guid>
{
    public Guid QuotationRequestId { get; private set; }
    public Guid CompanyId { get; private set; }

    // Invitation Details
    public DateTime InvitedAt { get; private set; }
    public bool NotificationSent { get; private set; }
    public DateTime? NotificationSentAt { get; private set; }

    // Company Response
    public DateTime? ViewedAt { get; private set; }
    public string Status { get; private set; } = null!; // Pending, Submitted, Declined, Expired

    private QuotationInvitation()
    {
    }

    public static QuotationInvitation Create(Guid quotationRequestId, Guid companyId)
    {
        return new QuotationInvitation
        {
            Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            CompanyId = companyId,
            InvitedAt = DateTime.UtcNow,
            Status = "Pending",
            NotificationSent = false
        };
    }

    public void MarkNotificationSent()
    {
        NotificationSent = true;
        NotificationSentAt = DateTime.UtcNow;
    }

    public void MarkViewed()
    {
        ViewedAt ??= DateTime.UtcNow;
    }

    public void MarkSubmitted()
    {
        Status = "Submitted";
    }

    public void Decline()
    {
        if (Status != "Pending")
            throw new InvalidOperationException($"Cannot decline invitation in status '{Status}'");

        Status = "Declined";
    }

    public void MarkExpired()
    {
        if (Status == "Pending") Status = "Expired";
    }
}