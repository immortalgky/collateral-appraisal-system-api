namespace Appraisal.Contracts.Services;

/// <summary>
/// Cross-module contract: Appraisal handlers call this to verify that the current
/// caller owns the active or historical fan-out workflow task for their company on a given quotation.
///
/// Implemented in the Workflow module using IAssignmentRepository +
/// PoolTaskAccess.IsOwner so the Appraisal module never touches WorkflowDbContext.
/// </summary>
public interface IQuotationTaskOwnershipService
{
    /// <summary>
    /// Returns true when the caller holds the active fan-out task that matches
    /// <paramref name="expectedStageName"/> for the given quotation + company.
    ///
    /// Passes straight through when <paramref name="expectedStageName"/> is null —
    /// any active stage for that company is accepted (used by Decline which does not
    /// care which stage is current).
    ///
    /// Internal admins (role "Admin" / "IntAdmin") are always granted ownership.
    /// </summary>
    Task<bool> IsCallerActiveTaskOwnerAsync(
        Guid quotationRequestId,
        Guid companyId,
        string? expectedStageName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the caller previously held a completed fan-out task on the
    /// given quotation (i.e. their task was consumed — the RFQ moved forward).
    /// Used for history-view access: callers keep visibility after their task is done.
    ///
    /// Internal admins (role "Admin" / "IntAdmin") are always granted ownership.
    /// </summary>
    Task<bool> IsCallerHistoricalTaskOwnerAsync(
        Guid quotationRequestId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Strict ownership check for a specific activity's active PendingTask on the quotation.
    /// Unlike <see cref="IsCallerActiveTaskOwnerAsync"/> this does NOT bypass for the Admin /
    /// IntAdmin roles — only the actual assignee (user, group, or group+team) passes.
    /// Used to gate UI affordances that the assigned task owner alone should perform
    /// (e.g. RM "Pick winner").
    /// </summary>
    Task<bool> IsCallerActivityTaskOwnerStrictAsync(
        Guid quotationRequestId,
        string activityId,
        CancellationToken cancellationToken = default);
}
