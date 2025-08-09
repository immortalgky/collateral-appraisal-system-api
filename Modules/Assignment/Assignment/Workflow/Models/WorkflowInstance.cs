using Shared.DDD;

namespace Assignment.Workflow.Models;

public class WorkflowInstance : Entity<Guid>
{
    public Guid WorkflowDefinitionId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? CorrelationId { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public string CurrentActivityId { get; private set; } = default!;
    public string? CurrentAssignee { get; private set; }
    public DateTime StartedOn { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public string StartedBy { get; private set; } = default!;
    public Dictionary<string, object> Variables { get; private set; } = new();
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }

    public WorkflowDefinition WorkflowDefinition { get; private set; } = default!;
    public List<WorkflowActivityExecution> ActivityExecutions { get; private set; } = new();

    private WorkflowInstance() { }

    public static WorkflowInstance Create(
        Guid workflowDefinitionId,
        string name,
        string? correlationId,
        string startedBy,
        Dictionary<string, object>? initialVariables = null)
    {
        return new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = workflowDefinitionId,
            Name = name,
            CorrelationId = correlationId,
            Status = WorkflowStatus.Running,
            CurrentActivityId = string.Empty,
            StartedOn = DateTime.UtcNow,
            StartedBy = startedBy,
            Variables = initialVariables ?? new Dictionary<string, object>()
        };
    }

    public void SetCurrentActivity(string activityId, string? assignee = null)
    {
        CurrentActivityId = activityId;
        CurrentAssignee = assignee;
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

    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    public void AddActivityExecution(WorkflowActivityExecution execution)
    {
        ActivityExecutions.Add(execution);
    }
}

public enum WorkflowStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
    Suspended
}