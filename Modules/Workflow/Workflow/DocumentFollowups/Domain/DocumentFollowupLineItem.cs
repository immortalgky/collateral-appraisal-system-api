namespace Workflow.DocumentFollowups.Domain;

public class DocumentFollowupLineItem
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = default!;
    public string? Notes { get; set; }
    public DocumentFollowupLineItemStatus Status { get; set; }
    public string? Reason { get; set; }
    public Guid? DocumentId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Public for System.Text.Json deserialization (used by EF JSON conversion).
    public DocumentFollowupLineItem() { }

    internal static DocumentFollowupLineItem Create(string documentType, string? notes)
    {
        return new DocumentFollowupLineItem
        {
            Id = Guid.CreateVersion7(),
            DocumentType = documentType,
            Notes = notes,
            Status = DocumentFollowupLineItemStatus.Pending
        };
    }

    internal void MarkUploaded(Guid documentId)
    {
        Status = DocumentFollowupLineItemStatus.Uploaded;
        DocumentId = documentId;
        ResolvedAt = DateTime.UtcNow;
    }

    internal void MarkDeclined(string reason)
    {
        Status = DocumentFollowupLineItemStatus.Declined;
        Reason = reason;
        ResolvedAt = DateTime.UtcNow;
    }

    internal void MarkCancelled(string reason)
    {
        Status = DocumentFollowupLineItemStatus.Cancelled;
        Reason = reason;
        ResolvedAt = DateTime.UtcNow;
    }
}

public enum DocumentFollowupLineItemStatus
{
    Pending = 0,
    Uploaded = 1,
    Declined = 2,
    Cancelled = 3
}
