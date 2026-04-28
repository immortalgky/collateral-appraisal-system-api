using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;

namespace Workflow.AssigneeSelection.Pipeline;

public class AssignmentPipelineContext
{
    // Input — set by the caller
    public ActivityContext ActivityContext { get; init; } = default!;

    /// <summary>
    /// When assigning for a stage transition on a fan-out item, this is the company Id
    /// (fan-out key). Used by <see cref="AssignmentContextBuilder"/> to resolve
    /// <c>excludeAssigneesFrom: ["&lt;activityId&gt;:&lt;stageName&gt;"]</c> entries.
    /// </summary>
    public Guid? FanOutKey { get; set; }

    // Stage 1 outputs
    public ActivityAssignmentRules Rules { get; set; } = ActivityAssignmentRules.Default;
    public string? TeamId { get; set; }
    public RuntimeOverride? RuntimeOverride { get; set; }
    public Dictionary<string, string> PriorAssignees { get; set; } = new();

    // Stage 2 outputs
    public List<TeamMemberInfo> CandidatePool { get; set; } = [];

    // Stage 3 outputs
    public string? SelectedAssignee { get; set; }
    public string? SelectionStrategy { get; set; }
    public Dictionary<string, object>? SelectionMetadata { get; set; }

    // Stage 4 outputs
    public bool ValidationPassed { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
}
