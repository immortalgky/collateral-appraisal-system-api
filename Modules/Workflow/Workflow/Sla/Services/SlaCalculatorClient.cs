namespace Workflow.Sla.Services;

/// <summary>
/// Thin wrapper that exposes <see cref="ISlaCalculator"/> under the cross-module
/// <see cref="ISlaCalculatorClient"/> contract so other modules can consume SLA
/// calculations without a direct reference to the Workflow DbContext or internals.
/// </summary>
public class SlaCalculatorClient(ISlaCalculator slaCalculator) : ISlaCalculatorClient
{
    public Task<WorkflowSlaSnapshot?> GetWorkflowSlaAsync(
        Guid workflowDefinitionId,
        string? loanType,
        string? appraisalType,
        DateTime startedAt,
        CancellationToken ct = default)
        => slaCalculator.GetWorkflowSlaSnapshotAsync(workflowDefinitionId, loanType, appraisalType, startedAt, ct);

    public Task<DateTime?> GetStageDueAtAsync(
        Guid? workflowDefinitionId,
        string startActivityKey,
        DateTime startedAt,
        Guid? companyId,
        string? loanType,
        string? appraisalType,
        Guid? correlationId = null,
        DateTime? appointmentDate = null,
        CancellationToken ct = default)
        => slaCalculator.CalculateStageDueAtAsync(
            workflowDefinitionId, startActivityKey, startedAt, companyId,
            loanType, appraisalType, correlationId, appointmentDate, ct);
}
