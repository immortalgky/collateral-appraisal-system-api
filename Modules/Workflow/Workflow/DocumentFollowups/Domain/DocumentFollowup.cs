using Workflow.DocumentFollowups.Domain.Events;

namespace Workflow.DocumentFollowups.Domain;

/// <summary>
/// Aggregate representing an out-of-band request from a checker for additional documents.
/// One DocumentFollowup is created per "raise" action and bundles multiple line items.
/// Status flow: Open -> (Resolved | Cancelled).
/// Linked to a followup WorkflowInstance that drives notification + assignment to the request maker.
/// </summary>
public class DocumentFollowup : Aggregate<Guid>
{
    // EF maps this collection directly (mirrors WorkflowInstance.Variables pattern).
    public List<DocumentFollowupLineItem> LineItems { get; private set; } = new();

    public Guid AppraisalId { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid RaisingWorkflowInstanceId { get; private set; }
    public Guid RaisingPendingTaskId { get; private set; }
    public string RaisingActivityId { get; private set; } = default!;
    public string RaisingUserId { get; private set; } = default!;
    public Guid? FollowupWorkflowInstanceId { get; private set; }
    public DocumentFollowupStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime RaisedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private DocumentFollowup() { }

    public static DocumentFollowup Raise(
        Guid appraisalId,
        Guid? requestId,
        Guid raisingWorkflowInstanceId,
        Guid raisingPendingTaskId,
        string raisingActivityId,
        string raisingUserId,
        IEnumerable<(string DocumentType, string? Notes)> lineItems)
    {
        if (raisingPendingTaskId == Guid.Empty)
            throw new ArgumentException("RaisingPendingTaskId is required", nameof(raisingPendingTaskId));
        if (string.IsNullOrWhiteSpace(raisingUserId))
            throw new ArgumentException("RaisingUserId is required", nameof(raisingUserId));

        var items = lineItems?.ToList() ?? new();
        if (items.Count == 0)
            throw new ArgumentException("At least one line item is required", nameof(lineItems));

        var followup = new DocumentFollowup
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            RequestId = requestId,
            RaisingWorkflowInstanceId = raisingWorkflowInstanceId,
            RaisingPendingTaskId = raisingPendingTaskId,
            RaisingActivityId = raisingActivityId,
            RaisingUserId = raisingUserId,
            Status = DocumentFollowupStatus.Open,
            RaisedAt = DateTime.Now
        };

        foreach (var (type, notes) in items)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("DocumentType is required for every line item");
            followup.LineItems.Add(DocumentFollowupLineItem.Create(type, notes));
        }

        followup.AddDomainEvent(new DocumentFollowupRaisedDomainEvent(followup.Id, followup.RaisingPendingTaskId));
        return followup;
    }

    public void AttachFollowupWorkflowInstance(Guid workflowInstanceId)
    {
        FollowupWorkflowInstanceId = workflowInstanceId;
    }

    /// <summary>
    /// Marks a single line item as fulfilled by an uploaded document.
    /// Does NOT auto-resolve: the request maker must explicitly Submit.
    /// </summary>
    public void FulfillByUpload(Guid lineItemId, Guid documentId)
    {
        var item = RequireOpenLineItem(lineItemId);
        item.MarkUploaded(documentId);
        // Do NOT auto-resolve: Submit is the only path back.
    }

    /// <summary>
    /// Marks the FIRST open line item that matches the given document type as fulfilled.
    /// Used by the auto-fulfill handler when a document is uploaded against the request.
    /// Returns the line item id if matched, otherwise null.
    /// NOTE: Does NOT auto-resolve the followup. The request maker must explicitly Submit.
    /// </summary>
    public Guid? FulfillFirstMatchingByType(string documentType, Guid documentId)
    {
        if (Status != DocumentFollowupStatus.Open) return null;

        var item = LineItems.FirstOrDefault(li =>
            li.Status == DocumentFollowupLineItemStatus.Pending &&
            string.Equals(li.DocumentType, documentType, StringComparison.OrdinalIgnoreCase));
        if (item is null) return null;

        item.MarkUploaded(documentId);
        return item.Id;
    }

    /// <summary>
    /// Explicitly submitted by the request maker once all items are Uploaded or Declined.
    /// Transitions Open → Resolved and fires DocumentFollowupResolvedDomainEvent.
    /// </summary>
    public void Submit(string submittedByUserId)
    {
        if (string.IsNullOrWhiteSpace(submittedByUserId))
            throw new ArgumentException("SubmittedByUserId is required", nameof(submittedByUserId));
        if (Status == DocumentFollowupStatus.Resolved)
            throw new InvalidOperationException("Followup is already resolved");
        if (Status == DocumentFollowupStatus.Cancelled)
            throw new InvalidOperationException("Followup is cancelled");
        if (LineItems.Any(li => li.Status == DocumentFollowupLineItemStatus.Pending))
            throw new InvalidOperationException("All line items must be Uploaded or Declined before submitting");

        Status = DocumentFollowupStatus.Resolved;
        ResolvedAt = DateTime.Now;
        AddDomainEvent(new DocumentFollowupResolvedDomainEvent(Id, RaisingPendingTaskId));
    }

    public void DeclineLineItem(Guid lineItemId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to decline a line item", nameof(reason));
        var item = RequireOpenLineItem(lineItemId);
        item.MarkDeclined(reason);
        // Do NOT auto-resolve: the request maker must explicitly Submit.
    }

    public void CancelLineItem(Guid lineItemId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to cancel a line item", nameof(reason));
        var item = RequireOpenLineItem(lineItemId);
        item.MarkCancelled(reason);
        // Do NOT auto-resolve: the request maker must explicitly Submit.
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to cancel a followup", nameof(reason));
        if (Status != DocumentFollowupStatus.Open) return;

        foreach (var item in LineItems.Where(li => li.Status == DocumentFollowupLineItemStatus.Pending))
        {
            item.MarkCancelled(reason);
        }

        Status = DocumentFollowupStatus.Cancelled;
        CancellationReason = reason;
        ResolvedAt = DateTime.Now;
        AddDomainEvent(new DocumentFollowupCancelledDomainEvent(Id, RaisingPendingTaskId, reason));
    }

    private DocumentFollowupLineItem RequireOpenLineItem(Guid lineItemId)
    {
        if (Status != DocumentFollowupStatus.Open)
            throw new InvalidOperationException("Followup is not open");
        var item = LineItems.FirstOrDefault(li => li.Id == lineItemId)
            ?? throw new InvalidOperationException($"Line item {lineItemId} not found");
        if (item.Status != DocumentFollowupLineItemStatus.Pending)
            throw new InvalidOperationException("Line item is already resolved");
        return item;
    }

}

public enum DocumentFollowupStatus
{
    Open = 0,
    Resolved = 1,
    Cancelled = 2
}
