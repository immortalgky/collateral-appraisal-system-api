namespace Shared.Messaging.Events;

/// <summary>
/// Published when an IBG quotation request transitions to Sent status.
/// Triggers notifications to each invited external company and spawns the quotation child workflow.
/// </summary>
public record QuotationStartedIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    /// <summary>Legacy single-appraisal field. Prefer AppraisalIds for multi-appraisal quotations.</summary>
    public Guid AppraisalId { get; init; }
    /// <summary>All appraisal IDs covered by this quotation (v2 multi-appraisal).</summary>
    public Guid[] AppraisalIds { get; init; } = [];
    public Guid RequestId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid? TaskExecutionId { get; init; }
    public DateTime DueDate { get; init; }
    public Guid[] InvitedCompanyIds { get; init; } = [];
    /// <summary>UserId of the RM who owns the linked request.</summary>
    public Guid? RmUserId { get; init; }
    /// <summary>Username (employee ID) of the RM who owns the linked request. Used for workflow task assignment.</summary>
    public string? RmUsername { get; init; }
    /// <summary>Username of the admin who sent the quotation (StartedBy for child workflow).</summary>
    public string? StartedByUsername { get; init; }
}
