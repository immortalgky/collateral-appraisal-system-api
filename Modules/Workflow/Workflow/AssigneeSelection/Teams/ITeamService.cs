namespace Workflow.AssigneeSelection.Teams;

public interface ITeamService
{
    Task<TeamInfo?> GetTeamForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<TeamMemberInfo>> GetTeamMembersForActivityAsync(string teamId, string roleName, CancellationToken cancellationToken = default);
    Task<List<TeamMemberInfo>> GetAllMembersForActivityAsync(string roleName, CancellationToken cancellationToken = default);
    Task<TeamInfo?> GetTeamByIdAsync(string teamId, CancellationToken cancellationToken = default);
}
