using Shared.DDD;

namespace Workflow.Sla.Models;

public class WorkflowSlaConfiguration : Entity<Guid>
{
    public Guid WorkflowDefinitionId { get; private set; }
    public string? LoanType { get; private set; }
    public int TotalDurationHours { get; private set; }
    public bool UseBusinessDays { get; private set; }
    public int Priority { get; private set; }

    private WorkflowSlaConfiguration() { }

    public static WorkflowSlaConfiguration Create(
        Guid workflowDefinitionId,
        int totalDurationHours,
        bool useBusinessDays,
        int priority,
        string? loanType = null)
    {
        return new WorkflowSlaConfiguration
        {
            Id = Guid.CreateVersion7(),
            WorkflowDefinitionId = workflowDefinitionId,
            LoanType = loanType,
            TotalDurationHours = totalDurationHours,
            UseBusinessDays = useBusinessDays,
            Priority = priority
        };
    }

    public void Update(int totalDurationHours, bool useBusinessDays, int priority, string? loanType = null)
    {
        TotalDurationHours = totalDurationHours;
        UseBusinessDays = useBusinessDays;
        Priority = priority;
        LoanType = loanType;
    }
}
