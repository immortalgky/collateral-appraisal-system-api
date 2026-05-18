namespace Workflow;

/// <summary>
/// Cross-module SLA computation surface. Implemented in the Workflow module and consumed by
/// other modules (e.g. Appraisal) without exposing the Workflow DbContext or SlaCalculator internals.
/// </summary>
public interface ISlaCalculatorClient
{
    /// <summary>
    /// Returns a snapshot containing both the policy DurationHours and the computed DueAt, or null
    /// when no Workflow-scope SlaPolicy matches. Callers that need to store the raw budget
    /// (e.g. Appraisal.SLAHours) should read DurationHours directly rather than recomputing
    /// hours from a calendar delta.
    /// </summary>
    Task<WorkflowSlaSnapshot?> GetWorkflowSlaAsync(
        Guid workflowDefinitionId,
        string? loanType,
        DateTime startedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the stage-level DueAt deadline, or null when no Stage-scope SlaPolicy matches
    /// <paramref name="startActivityKey"/>. Delegates to <c>SlaCalculator.CalculateStageDueAtAsync</c>.
    /// Pass <c>null</c> for <paramref name="workflowDefinitionId"/> when the ID is unknown (e.g.
    /// from the appraisal-creation path); only wildcard policies (WorkflowDefinitionId = null) will match.
    /// </summary>
    Task<DateTime?> GetStageDueAtAsync(
        Guid? workflowDefinitionId,
        string startActivityKey,
        DateTime startedAt,
        Guid? companyId,
        string? loanType,
        CancellationToken ct = default);
}

/// <summary>
/// Workflow-scope SLA budget snapshot returned by <see cref="ISlaCalculatorClient.GetWorkflowSlaAsync"/>.
/// </summary>
public record WorkflowSlaSnapshot(int DurationHours, DateTime DueAt, bool UseBusinessDays);
