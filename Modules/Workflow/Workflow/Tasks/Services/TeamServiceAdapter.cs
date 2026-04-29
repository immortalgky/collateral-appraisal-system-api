using Appraisal.Contracts.Services;
using WorkflowTeamService = global::Workflow.AssigneeSelection.Teams.ITeamService;

namespace Workflow.Tasks.Services;

/// <summary>
/// Exposes ITeamService (Workflow) as Appraisal.Contracts.Services.ITeamService
/// so the Appraisal module's query handlers can consume it without a direct Workflow reference.
/// Maps Workflow.AssigneeSelection.Teams.TeamInfo → Appraisal.Contracts.Services.TeamInfo.
/// </summary>
internal sealed class TeamServiceAdapter(WorkflowTeamService inner)
    : Appraisal.Contracts.Services.ITeamService
{
    public async Task<Appraisal.Contracts.Services.TeamInfo?> GetTeamForUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var result = await inner.GetTeamForUserAsync(userId, cancellationToken);
        return result is null ? null : new Appraisal.Contracts.Services.TeamInfo(result.TeamId, result.Name);
    }
}
