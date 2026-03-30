using Workflow.AssigneeSelection.Teams;

namespace Workflow.AssigneeSelection.Core;

public class AssignmentContext
{
    public Guid WorkflowInstanceId { get; set; }
    public string ActivityName { get; set; } = default!;
    public List<string> AssignmentStrategies { get; set; } = new();
    public List<string> UserGroups { get; set; } = new();
    public string UserCode { get; set; } = default!;
    public DateTime DueDate { get; set; }
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// The user who originally started the workflow instance.
    /// Used by the StartedBy assignee selection strategy.
    /// </summary>
    public string? StartedBy { get; set; }

    /// <summary>
    /// Pre-filtered candidate pool from the assignment pipeline (Stage 2).
    /// When set, selectors should prefer this list over querying their own user sources.
    /// </summary>
    public List<TeamMemberInfo>? CandidatePool { get; set; }

    /// <summary>
    /// Workflow instance variables (runtime state). Used by VariableAssignee strategy.
    /// </summary>
    public Dictionary<string, object>? Variables { get; set; }
}