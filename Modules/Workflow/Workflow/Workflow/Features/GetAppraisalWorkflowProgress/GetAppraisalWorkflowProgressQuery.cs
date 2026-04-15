using Shared.CQRS;

namespace Workflow.Workflow.Features.GetAppraisalWorkflowProgress;

public record GetAppraisalWorkflowProgressQuery(Guid AppraisalId) : IQuery<GetAppraisalWorkflowProgressResponse>;

public class GetAppraisalWorkflowProgressResponse
{
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatus { get; set; }
    public string RouteType { get; set; } = "Unknown";
    public string? CurrentActivityId { get; set; }
    public List<PhaseStepDto> Steps { get; set; } = [];
    public List<ActivityLogItemDto> ActivityLog { get; set; } = [];
}

public class PhaseStepDto
{
    public string Group { get; set; } = default!;
    public string Status { get; set; } = default!; // Completed | Current | Pending
}

public class ActivityLogItemDto
{
    public int SequenceNo { get; set; }
    public string ActivityName { get; set; } = default!;
    public string? TaskDescription { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ActionTaken { get; set; }
    public string? TimeTaken { get; set; }
    public string? Remark { get; set; }
    public string Status { get; set; } = default!; // Completed | Pending
    public string? Group { get; set; }
    public string? ActivityId { get; set; }
    public string? CompanyName { get; set; }
}
