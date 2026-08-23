using Workflow.AssigneeSelection.Teams;
using Workflow.Workflow.Activities.Core;

namespace Workflow.AssigneeSelection.Pipeline;

public class AssignmentFinalizer : IAssignmentFinalizer
{
    private readonly ITeamService _teamService;
    private readonly ILogger<AssignmentFinalizer> _logger;

    public AssignmentFinalizer(ITeamService teamService, ILogger<AssignmentFinalizer> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    public async Task<AssignmentResult> FinalizeAsync(
        AssignmentPipelineContext context, CancellationToken cancellationToken = default)
    {
        var assignee = context.SelectedAssignee ?? "Unassigned";
        var instance = context.ActivityContext.WorkflowInstance;

        // Sync TeamId to the actual assignee's team — either setting it for the first time, or
        // re-syncing it after a route-back to a previous_owner in a different team than whatever
        // context.TeamId was pinned to (e.g. the team of whoever routed back, not the assignee's own).
        // A no-op for pool assignees: GetTeamForUserAsync returns null for a pool string like
        // "Group:Team_x" (it isn't a real userId), so the existing (already-validated) TeamId is kept.
        if (context.Rules.TeamConstrained && assignee != "Unassigned")
        {
            var team = await _teamService.GetTeamForUserAsync(assignee, cancellationToken);
            if (team is not null && team.TeamId != context.TeamId)
            {
                instance.UpdateVariables(new Dictionary<string, object> { ["TeamId"] = team.TeamId });
                context.TeamId = team.TeamId;

                _logger.LogInformation(
                    "Pipeline finalizer: Set TeamId={TeamId} from assignee {Assignee}",
                    team.TeamId, assignee);
            }
        }

        // Build metadata
        var metadata = context.SelectionMetadata ?? new Dictionary<string, object>();
        metadata["pipeline"] = true;
        metadata["teamConstrained"] = context.Rules.TeamConstrained;

        if (!string.IsNullOrEmpty(context.TeamId))
            metadata["teamId"] = context.TeamId;

        if (context.Rules.ExcludeAssigneesFrom.Count > 0)
            metadata["excludeAssigneesFrom"] = context.Rules.ExcludeAssigneesFrom;

        if (context.CandidatePool.Count > 0)
            metadata["candidatePoolSize"] = context.CandidatePool.Count;

        return new AssignmentResult
        {
            IsSuccess = true,
            AssigneeId = assignee,
            Strategy = context.SelectionStrategy ?? "Pipeline",
            Metadata = metadata
        };
    }
}
