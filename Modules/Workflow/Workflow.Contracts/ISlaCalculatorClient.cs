namespace Workflow;

/// <summary>
/// Cross-module SLA computation surface. Implemented in the Workflow module and consumed by
/// other modules (e.g. Appraisal) without exposing the Workflow DbContext or SlaCalculator internals.
/// </summary>
public interface ISlaCalculatorClient
{
    /// <summary>
    /// Returns the workflow-level DueAt deadline, or null when no Workflow-scope SlaPolicy matches.
    /// Delegates to <c>SlaCalculator.CalculateWorkflowDueAtAsync</c>.
    /// </summary>
    Task<DateTime?> GetWorkflowDueAtAsync(
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
