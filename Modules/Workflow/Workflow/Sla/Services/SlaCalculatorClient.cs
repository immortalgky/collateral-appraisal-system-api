namespace Workflow.Sla.Services;

/// <summary>
/// Thin wrapper that exposes <see cref="ISlaCalculator"/> under the cross-module
/// <see cref="ISlaCalculatorClient"/> contract so other modules can consume SLA
/// calculations without a direct reference to the Workflow DbContext or internals.
/// </summary>
public class SlaCalculatorClient(ISlaCalculator slaCalculator) : ISlaCalculatorClient
{
    public Task<DateTime?> GetWorkflowDueAtAsync(
        Guid workflowDefinitionId,
        string? loanType,
        DateTime startedAt,
        CancellationToken ct = default)
        => slaCalculator.CalculateWorkflowDueAtAsync(workflowDefinitionId, loanType, startedAt, ct);

    public Task<DateTime?> GetStageDueAtAsync(
        Guid? workflowDefinitionId,
        string startActivityKey,
        DateTime startedAt,
        Guid? companyId,
        string? loanType,
        CancellationToken ct = default)
        => slaCalculator.CalculateStageDueAtAsync(workflowDefinitionId, startActivityKey, startedAt, companyId, loanType, ct);
}
