using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Configuration.Models;
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

    /// <summary>
    /// DB-backed assignment override (resolved in <see cref="AssignmentContextBuilder"/>) for this
    /// activity/workflow/banking-segment scope. Null = no active row → JSON definition is the baseline.
    /// </summary>
    public TaskAssignmentConfigurationDto? ExternalConfig { get; set; }

    /// <summary>
    /// The assignee group after applying precedence (RuntimeOverride &gt; DB config &gt; JSON definition),
    /// resolved once in <see cref="AssignmentContextBuilder"/>. Both the Stage 2 candidate-pool filter
    /// (<c>TeamFilter</c>) and the Stage 3 engine read this single value so they cannot disagree.
    /// Null/empty = no group configured.
    /// </summary>
    public string? ResolvedAssigneeGroup { get; set; }

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
