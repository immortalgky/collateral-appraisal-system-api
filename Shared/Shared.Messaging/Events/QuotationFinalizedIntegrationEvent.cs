namespace Shared.Messaging.Events;

/// <summary>
/// Published when an admin finalizes a quotation, committing a winning company and final fee.
/// v2: includes AppraisalIds array (may contain multiple appraisals per quotation).
///     FinalFeeAmount remains the total negotiated price; per-appraisal breakdown is in CompanyQuotationItems.
/// Consumed by:
///   - Appraisal module: iterates AppraisalIds, publishes one CompanyAssignedIntegrationEvent per appraisal.
///   - Workflow module:  completes the admin's appraisal-assignment task with decision EXT (per TaskExecutionId).
///   - Notification module: notifies winning company and RM.
/// </summary>
public record QuotationFinalizedIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }

    /// <summary>
    /// v2: all appraisals in the quotation. Replaces the single AppraisalId from v1.
    /// For backward compat, use AppraisalIds[0] where a single id is expected.
    /// </summary>
    public Guid[] AppraisalIds { get; init; } = [];

    /// <summary>
    /// v1 compat: first appraisal id (or Empty if none). Prefer AppraisalIds.
    /// </summary>
    public Guid AppraisalId => AppraisalIds.Length > 0 ? AppraisalIds[0] : Guid.Empty;

    public Guid RequestId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid? TaskExecutionId { get; init; }
    public Guid WinningCompanyId { get; init; }
    public Guid WinningQuotationId { get; init; }
    public decimal FinalFeeAmount { get; init; }

    /// <summary>UserId of the RM who owns the Request. Used by Notification handler.</summary>
    public Guid? RmUserId { get; init; }
}
