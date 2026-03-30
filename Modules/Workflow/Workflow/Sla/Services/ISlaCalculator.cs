namespace Workflow.Sla.Services;

public interface ISlaCalculator
{
    Task<DateTime?> CalculateActivityDueAtAsync(
        string activityId,
        Guid workflowDefinitionId,
        Guid? companyId,
        string? loanType,
        DateTime assignedAt,
        TimeSpan? defaultTimeout,
        DateTime? workflowDueAt,
        CancellationToken ct = default);

    Task<DateTime?> CalculateWorkflowDueAtAsync(
        Guid workflowDefinitionId,
        string? loanType,
        DateTime startedOn,
        CancellationToken ct = default);
}
