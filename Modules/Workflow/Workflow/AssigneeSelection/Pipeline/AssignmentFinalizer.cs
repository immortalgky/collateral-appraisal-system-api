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

        // Set TeamId on first team-constrained assignment if not already set
        if (context.Rules.TeamConstrained && string.IsNullOrEmpty(context.TeamId) && assignee != "Unassigned")
        {
            var team = await _teamService.GetTeamForUserAsync(assignee, cancellationToken);
            if (team is not null)
            {
                instance.UpdateVariables(new Dictionary<string, object> { ["TeamId"] = team.TeamId });
                context.TeamId = team.TeamId;

                _logger.LogInformation(
                    "Pipeline finalizer: Set TeamId={TeamId} from first assignee {Assignee}",
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
