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

    /// <summary>
    /// True when the target activity already has a Completed execution in this workflow instance
    /// (a route-back/revisit). Computed once per <see cref="AssignmentPipeline.AssignAsync"/> call
    /// via <see cref="Engine.ICascadingAssignmentEngine.IsRouteBackScenarioAsync"/> and cached here so
    /// Stage 2's empty-candidate-pool short-circuit can consult it. Left at the default (false) when
    /// a manual pick (<see cref="RuntimeOverride"/>) is present, since Stage 3 resolves that before
    /// either this or <see cref="Strategies"/> would be read.
    /// </summary>
    public bool IsRevisit { get; set; }

    /// <summary>
    /// The assignment strategy list, resolved once alongside <see cref="IsRevisit"/> (same
    /// RuntimeOverride &gt; DB config &gt; JSON precedence Stage 3 used to apply per-attempt). Consulted
    /// by Stage 2's empty-pool gate to confirm every strategy about to run is pool-independent (e.g.
    /// <c>previous_owner</c>) before bypassing the hard-fail — a pool-dependent strategy (e.g.
    /// <c>pool</c>) must still fail fast on a genuinely empty pool.
    /// </summary>
    public List<string> Strategies { get; set; } = [];

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
