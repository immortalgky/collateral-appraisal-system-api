namespace Workflow.AssigneeSelection.Teams;

public class MockTeamService : ITeamService
{
    private static readonly List<TeamInfo> Teams =
    [
        new("team-a", "Internal Team A", TeamType.Internal, true),
        new("team-b", "Internal Team B", TeamType.Internal, true),
        new("ext-company-1", "External Appraisal Co.", TeamType.External, true)
    ];

    private static readonly List<TeamMemberInfo> Members =
    [
        // Team A
        new("staff_a01", "Alice Staff", "team-a", ["appraisal-staff"]),
        new("staff_a02", "Andy Staff", "team-a", ["appraisal-staff"]),
        new("checker_a01", "Anna Checker", "team-a", ["appraisal-checker"]),
        new("verifier_a01", "Aaron Verifier", "team-a", ["appraisal-verifier"]),

        // Team B
        new("staff_b01", "Bob Staff", "team-b", ["appraisal-staff"]),
        new("checker_b01", "Beth Checker", "team-b", ["appraisal-checker"]),
        new("verifier_b01", "Brian Verifier", "team-b", ["appraisal-verifier"]),

        // External company
        new("ext_staff_01", "Eve External", "ext-company-1", ["appraisal-staff"]),
        new("ext_checker_01", "Ed External", "ext-company-1", ["appraisal-checker"]),
        new("ext_verifier_01", "Emma External", "ext-company-1", ["appraisal-verifier"])
    ];

    public Task<TeamInfo?> GetTeamForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var member = Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            return Task.FromResult<TeamInfo?>(null);

        var team = Teams.FirstOrDefault(t => t.TeamId == member.TeamId && t.IsActive);
        return Task.FromResult(team);
    }

    public Task<List<TeamMemberInfo>> GetTeamMembersForActivityAsync(
        string teamId, string activityName, CancellationToken cancellationToken = default)
    {
        var result = Members
            .Where(m => m.TeamId == teamId && m.ActivityRoles.Contains(activityName))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<List<TeamMemberInfo>> GetAllMembersForActivityAsync(
        string activityName, CancellationToken cancellationToken = default)
    {
        var activeTeamIds = Teams.Where(t => t.IsActive).Select(t => t.TeamId).ToHashSet();

        var result = Members
            .Where(m => activeTeamIds.Contains(m.TeamId) && m.ActivityRoles.Contains(activityName))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<TeamInfo?> GetTeamByIdAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var team = Teams.FirstOrDefault(t => t.TeamId == teamId && t.IsActive);
        return Task.FromResult(team);
    }
}
