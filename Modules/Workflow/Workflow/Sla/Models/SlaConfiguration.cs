using Shared.DDD;

namespace Workflow.Sla.Models;

public class SlaConfiguration : Entity<Guid>
{
    public string ActivityId { get; private set; } = default!;
    public Guid? WorkflowDefinitionId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? LoanType { get; private set; }
    public int DurationHours { get; private set; }
    public bool UseBusinessDays { get; private set; }
    public int Priority { get; private set; }

    private SlaConfiguration() { }

    public static SlaConfiguration Create(
        string activityId,
        int durationHours,
        bool useBusinessDays,
        int priority,
        Guid? workflowDefinitionId = null,
        Guid? companyId = null,
        string? loanType = null)
    {
        return new SlaConfiguration
        {
            Id = Guid.CreateVersion7(),
            ActivityId = activityId,
            WorkflowDefinitionId = workflowDefinitionId,
            CompanyId = companyId,
            LoanType = loanType,
            DurationHours = durationHours,
            UseBusinessDays = useBusinessDays,
            Priority = priority
        };
    }

    public void Update(
        int durationHours,
        bool useBusinessDays,
        int priority,
        string? loanType = null,
        Guid? companyId = null)
    {
        DurationHours = durationHours;
        UseBusinessDays = useBusinessDays;
        Priority = priority;
        LoanType = loanType;
        CompanyId = companyId;
    }
}
