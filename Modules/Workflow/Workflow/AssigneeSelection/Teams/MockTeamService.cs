namespace Workflow.AssigneeSelection.Teams;

public class MockTeamService : ITeamService
{
    private static readonly List<TeamInfo> Teams =
    [
        new("team-a", "Internal Team A", TeamType.Internal, true),
        new("team-b", "Internal Team B", TeamType.Internal, true),
        new("ext-company-1", "External Appraisal Co.", TeamType.External, true),

        // Company-based teams (TeamId = company GUID from seed data)
        // In production, ITeamService will query ApplicationUser by CompanyId
        new("company-thai-appraisal", "Thai Appraisal Co., Ltd.", TeamType.External, true),
        new("company-siam-valuation", "Siam Valuation Group", TeamType.External, true),
        new("company-bangkok-property", "Bangkok Property Consultants", TeamType.External, true)
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

        // External company (legacy mock)
        new("ext_staff_01", "Eve External", "ext-company-1", ["appraisal-staff"]),
        new("ext_checker_01", "Ed External", "ext-company-1", ["appraisal-checker"]),
        new("ext_verifier_01", "Emma External", "ext-company-1", ["appraisal-verifier"]),

        // Thai Appraisal Co. members
        new("thai_staff_01", "Somchai S.", "company-thai-appraisal", ["ext-appraisal-staff"]),
        new("thai_staff_02", "Sureeporn T.", "company-thai-appraisal", ["ext-appraisal-staff"]),
        new("thai_checker_01", "Pranee P.", "company-thai-appraisal", ["ext-appraisal-checker"]),
        new("thai_verifier_01", "Wichai W.", "company-thai-appraisal", ["ext-appraisal-verifier"]),

        // Siam Valuation Group members
        new("siam_staff_01", "Nattapong N.", "company-siam-valuation", ["ext-appraisal-staff"]),
        new("siam_checker_01", "Kanokwan K.", "company-siam-valuation", ["ext-appraisal-checker"]),
        new("siam_verifier_01", "Thawatchai T.", "company-siam-valuation", ["ext-appraisal-verifier"]),

        // Bangkok Property Consultants members
        new("bkk_staff_01", "Pornchai P.", "company-bangkok-property", ["ext-appraisal-staff"]),
        new("bkk_checker_01", "Siriporn S.", "company-bangkok-property", ["ext-appraisal-checker"]),
        new("bkk_verifier_01", "Arthit A.", "company-bangkok-property", ["ext-appraisal-verifier"])
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
        string teamId, string roleName, CancellationToken cancellationToken = default)
    {
        var result = Members
            .Where(m => m.TeamId == teamId && m.ActivityRoles.Contains(roleName))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<List<TeamMemberInfo>> GetAllMembersForActivityAsync(
        string roleName, CancellationToken cancellationToken = default)
    {
        var activeTeamIds = Teams.Where(t => t.IsActive).Select(t => t.TeamId).ToHashSet();

        var result = Members
            .Where(m => activeTeamIds.Contains(m.TeamId) && m.ActivityRoles.Contains(roleName))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<TeamInfo?> GetTeamByIdAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var team = Teams.FirstOrDefault(t => t.TeamId == teamId && t.IsActive);
        return Task.FromResult(team);
    }
}
