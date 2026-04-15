using Shared.DDD;
using Workflow.Workflow.Activities.Core;

namespace Workflow.Workflow.Models;

public class WorkflowInstance : Entity<Guid>
{
    public Guid WorkflowDefinitionId { get; private set; }
    public Guid WorkflowDefinitionVersionId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? CorrelationId { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public string CurrentActivityId { get; private set; } = default!;
    public string? CurrentAssignee { get; private set; }
    public string? LastCompletedBy { get; private set; }
    public DateTime StartedOn { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string StartedBy { get; private set; } = default!;
    public Dictionary<string, object> Variables { get; private set; } = new();
    public Dictionary<string, RuntimeOverride> RuntimeOverrides { get; private set; } = new();
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? WorkflowDueAt { get; private set; }
    public string? WorkflowSlaStatus { get; private set; }
    public List<BranchActivityState> ActiveBranchActivities { get; private set; } = new();

    public WorkflowDefinition WorkflowDefinition { get; private set; } = default!;
    public List<WorkflowActivityExecution> ActivityExecutions { get; private set; } = new();

    private WorkflowInstance()
    {
        // For EF Core
    }

    /// <summary>
    /// Test/back-compat overload that synthesizes a version id. Production code MUST use the
    /// overload taking an explicit workflowDefinitionVersionId.
    /// </summary>
    public static WorkflowInstance Create(
        Guid workflowDefinitionId,
        string name,
        string? correlationId,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        Dictionary<string, RuntimeOverride>? runtimeOverrides = null)
        => Create(workflowDefinitionId, Guid.NewGuid(), name, correlationId, startedBy, initialVariables,
            runtimeOverrides);

    public static WorkflowInstance Create(
        Guid workflowDefinitionId,
        Guid workflowDefinitionVersionId,
        string name,
        string? correlationId,
        string startedBy,
        Dictionary<string, object>? initialVariables = null,
        Dictionary<string, RuntimeOverride>? runtimeOverrides = null)
    {
        return new WorkflowInstance
        {
            Id = Guid.CreateVersion7(),
            WorkflowDefinitionId = workflowDefinitionId,
            WorkflowDefinitionVersionId = workflowDefinitionVersionId,
            Name = name,
            CorrelationId = correlationId,
            Status = WorkflowStatus.Running,
            CurrentActivityId = string.Empty,
            StartedOn = DateTime.UtcNow,
            StartedBy = startedBy,
            Variables = initialVariables ?? new Dictionary<string, object>(),
            RuntimeOverrides = runtimeOverrides ?? new Dictionary<string, RuntimeOverride>()
        };
    }

    /// <summary>
    /// Migrates this instance to a new workflow definition version, optionally remapping the current activity id.
    /// Only valid for Running instances. Fork/join branch remap is not supported in this method.
    /// </summary>
    public void MigrateToVersion(Guid newVersionId, string? remappedCurrentActivityId)
    {
        if (Status != WorkflowStatus.Running)
            throw new InvalidOperationException(
                $"Cannot migrate workflow instance {Id} in status {Status}; only Running instances can be migrated.");

        WorkflowDefinitionVersionId = newVersionId;

        if (!string.IsNullOrWhiteSpace(remappedCurrentActivityId))
        {
            CurrentActivityId = remappedCurrentActivityId;
        }
    }

    public void SetCurrentActivity(string activityId, string? assignee = null)
    {
        CurrentActivityId = activityId;
        CurrentAssignee = assignee;
    }

    public void SetLastCompletedBy(string? completedBy)
    {
        if (string.IsNullOrWhiteSpace(completedBy)) return;
        if (string.Equals(completedBy, "system", StringComparison.OrdinalIgnoreCase)) return;
        LastCompletedBy = completedBy;
    }

    public void UpdateStatus(WorkflowStatus status, string? errorMessage = null)
    {
        Status = status;
        
        if (status == WorkflowStatus.Completed || status == WorkflowStatus.Failed || status == WorkflowStatus.Cancelled)
        {
            CompletedOn = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            ErrorMessage = errorMessage;
        }
    }

    public void UpdateVariables(Dictionary<string, object> variables)
    {
        foreach (var variable in variables)
        {
            Variables[variable.Key] = variable.Value;
        }
    }

    public void UpdateRuntimeOverrides(Dictionary<string, RuntimeOverride>? runtimeOverrides)
    {
        if (runtimeOverrides != null)
        {
            foreach (var kvp in runtimeOverrides)
            {
                RuntimeOverrides[kvp.Key] = kvp.Value;
            }
        }
    }

    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    public void AddActivityExecution(WorkflowActivityExecution execution)
    {
        ActivityExecutions.Add(execution);
    }

    public void SetWorkflowSla(DateTime dueAt)
    {
        WorkflowDueAt = dueAt;
        WorkflowSlaStatus = "OnTime";
    }

    public void MarkWorkflowAtRisk()
    {
        if (WorkflowSlaStatus == "OnTime")
            WorkflowSlaStatus = "AtRisk";
    }

    public void MarkWorkflowBreached()
    {
        if (WorkflowSlaStatus != "Breached")
            WorkflowSlaStatus = "Breached";
    }

    // --- Branch tracking for fork/join ---

    public void AddBranchActivity(BranchActivityState state)
    {
        ActiveBranchActivities.Add(state);
    }

    public void RemoveBranchActivity(string forkId, string branchId)
    {
        ActiveBranchActivities.RemoveAll(b => b.ForkId == forkId && b.BranchId == branchId);
    }

    public BranchActivityState? GetBranchActivity(string activityId)
    {
        return ActiveBranchActivities.FirstOrDefault(b => b.ActivityId == activityId);
    }

    public bool HasActiveBranches() => ActiveBranchActivities.Any();

    public bool IsInParallelMode() => ActiveBranchActivities.Any();

    public void UpdateBranchActivityId(string forkId, string branchId, string newActivityId)
    {
        var branch = ActiveBranchActivities.FirstOrDefault(b => b.ForkId == forkId && b.BranchId == branchId);
        if (branch != null)
        {
            branch.ActivityId = newActivityId;
        }
    }

    public void ClearBranchActivities(string forkId)
    {
        ActiveBranchActivities.RemoveAll(b => b.ForkId == forkId);
    }
}

public class BranchActivityState
{
    public string ForkId { get; set; } = default!;
    public string BranchId { get; set; } = default!;
    public string ActivityId { get; set; } = default!;
    public string? Assignee { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Completed
}

public enum WorkflowStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
    Suspended
}
