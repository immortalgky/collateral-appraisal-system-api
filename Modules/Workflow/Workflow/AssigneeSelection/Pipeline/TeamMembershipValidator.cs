using Workflow.AssigneeSelection.Teams;

namespace Workflow.AssigneeSelection.Pipeline;

public class TeamMembershipValidator : IAssignmentValidator
{
    private readonly ITeamService _teamService;
    private readonly ILogger<TeamMembershipValidator> _logger;

    public TeamMembershipValidator(ITeamService teamService, ILogger<TeamMembershipValidator> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    public async Task<AssignmentValidationResult> ValidateAsync(
        AssignmentPipelineContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Rules.TeamConstrained || string.IsNullOrEmpty(context.SelectedAssignee))
            return AssignmentValidationResult.Valid();

        var teamId = context.TeamId;
        if (string.IsNullOrEmpty(teamId))
        {
            // No team set yet — the finalizer will set it from the assignee's team
            return AssignmentValidationResult.Valid();
        }

        // Pool assignees (e.g. "ExtAdmin:Team_<teamId>") are team-scoped by construction
        // and are not users — GetTeamForUserAsync would always return null for them.
        if (string.Equals(context.SelectionStrategy, "pool", StringComparison.OrdinalIgnoreCase))
        {
            var expectedSuffix = $":Team_{teamId}";
            if (!context.SelectedAssignee.EndsWith(expectedSuffix, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Pool assignee {Assignee} is not scoped to required team {RequiredTeam}",
                    context.SelectedAssignee, teamId);
                return AssignmentValidationResult.Invalid(
                    $"Pool assignee '{context.SelectedAssignee}' is not scoped to team '{teamId}'");
            }

            return AssignmentValidationResult.Valid();
        }

        // Verify the selected assignee belongs to the workflow's team
        var assigneeTeam = await _teamService.GetTeamForUserAsync(context.SelectedAssignee, cancellationToken);

        if (assigneeTeam is null)
        {
            _logger.LogWarning("Selected assignee {Assignee} has no team", context.SelectedAssignee);
            return AssignmentValidationResult.Invalid(
                $"Assignee '{context.SelectedAssignee}' does not belong to any team");
        }

        if (assigneeTeam.TeamId != teamId)
        {
            _logger.LogWarning(
                "Assignee {Assignee} is in team {AssigneeTeam} but workflow requires team {RequiredTeam}",
                context.SelectedAssignee, assigneeTeam.TeamId, teamId);
            return AssignmentValidationResult.Invalid(
                $"Assignee '{context.SelectedAssignee}' belongs to team '{assigneeTeam.Name}' but workflow is constrained to team '{teamId}'");
        }

        return AssignmentValidationResult.Valid();
    }
}
