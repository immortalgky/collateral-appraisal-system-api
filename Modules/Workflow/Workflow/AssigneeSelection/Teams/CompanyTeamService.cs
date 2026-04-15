using Dapper;
using Shared.Data;

namespace Workflow.AssigneeSelection.Teams;

/// <summary>
/// Real ITeamService implementation that queries both auth.Teams/TeamMembers
/// (internal teams) and auth.Companies/CompanyId (external teams).
/// Group names are passed from the workflow schema's "assigneeGroup" property
/// and resolved via auth.Groups/auth.GroupUsers tables.
/// </summary>
public class CompanyTeamService : ITeamService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<CompanyTeamService> _logger;

    public CompanyTeamService(ISqlConnectionFactory connectionFactory, ILogger<CompanyTeamService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<TeamInfo?> GetTeamForUserAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userName))
            return null;

        using var connection = _connectionFactory.GetOpenConnection();

        // 1. Check auth.TeamMembers first (internal teams)
        var teamResult = await connection.QueryFirstOrDefaultAsync<TeamRow>(
            """
            SELECT t.Id, t.Name, t.Type, t.IsActive
            FROM auth.Teams t
            INNER JOIN auth.TeamMembers tm ON tm.TeamId = t.Id
            INNER JOIN auth.AspNetUsers u ON u.Id = tm.UserId
            WHERE u.UserName = @UserName AND t.IsActive = 1
            """,
            new { UserName = userName });

        if (teamResult is not null)
        {
            return new TeamInfo(
                teamResult.Id.ToString(),
                teamResult.Name,
                teamResult.Type == "Internal" ? TeamType.Internal : TeamType.External,
                teamResult.IsActive);
        }

        // 2. Fall back to CompanyId (external teams)
        var companyResult = await connection.QueryFirstOrDefaultAsync<CompanyRow>(
            """
            SELECT c.Id, c.Name, c.IsActive
            FROM auth.Companies c
            INNER JOIN auth.AspNetUsers u ON u.CompanyId = c.Id
            WHERE u.UserName = @UserName AND c.IsDeleted = 0
            """,
            new { UserName = userName });

        if (companyResult is null)
            return null;

        return new TeamInfo(
            companyResult.Id.ToString(),
            companyResult.Name,
            TeamType.External,
            companyResult.IsActive);
    }

    public async Task<List<TeamMemberInfo>> GetTeamMembersForActivityAsync(
        string teamId, string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            _logger.LogWarning("No group name provided for team {TeamId}", teamId);
            return [];
        }

        if (!Guid.TryParse(teamId, out var teamGuid))
        {
            _logger.LogWarning("Invalid TeamId (not a GUID): {TeamId}", teamId);
            return [];
        }

        using var connection = _connectionFactory.GetOpenConnection();

        // UNION: members from TeamMembers table + members from CompanyId
        var members = await connection.QueryAsync<UserRow>(
            """
            SELECT u.UserName, u.FirstName, u.LastName
            FROM auth.AspNetUsers u
            INNER JOIN auth.TeamMembers tm ON tm.UserId = u.Id
            INNER JOIN auth.GroupUsers gu ON gu.UserId = u.Id
            INNER JOIN auth.Groups g ON g.Id = gu.GroupId
            WHERE tm.TeamId = @TeamId
              AND g.Name = @GroupName
              AND g.IsDeleted = 0

            UNION

            SELECT u.UserName, u.FirstName, u.LastName
            FROM auth.AspNetUsers u
            INNER JOIN auth.GroupUsers gu ON gu.UserId = u.Id
            INNER JOIN auth.Groups g ON g.Id = gu.GroupId
            WHERE u.CompanyId = @TeamId
              AND g.Name = @GroupName
              AND g.IsDeleted = 0
            """,
            new { TeamId = teamGuid, GroupName = groupName });

        var result = members.Select(m => new TeamMemberInfo(
            m.UserName,
            $"{m.FirstName} {m.LastName}".Trim(),
            teamId,
            [groupName]
        )).ToList();

        return result;
    }

    public async Task<List<TeamMemberInfo>> GetAllMembersForActivityAsync(
        string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            _logger.LogWarning("No group name provided for GetAllMembersForActivityAsync");
            return [];
        }

        using var connection = _connectionFactory.GetOpenConnection();

        var members = await connection.QueryAsync<UserWithTeamRow>(
            """
            SELECT u.UserName, u.FirstName, u.LastName, tm.TeamId AS TeamId
            FROM auth.AspNetUsers u
            INNER JOIN auth.TeamMembers tm ON tm.UserId = u.Id
            INNER JOIN auth.GroupUsers gu ON gu.UserId = u.Id
            INNER JOIN auth.Groups g ON g.Id = gu.GroupId
            WHERE g.Name = @GroupName
              AND g.IsDeleted = 0

            UNION

            SELECT u.UserName, u.FirstName, u.LastName, u.CompanyId AS TeamId
            FROM auth.AspNetUsers u
            INNER JOIN auth.GroupUsers gu ON gu.UserId = u.Id
            INNER JOIN auth.Groups g ON g.Id = gu.GroupId
            WHERE g.Name = @GroupName
              AND g.IsDeleted = 0
              AND u.CompanyId IS NOT NULL
            """,
            new { GroupName = groupName });

        return members.Select(m => new TeamMemberInfo(
            m.UserName,
            $"{m.FirstName} {m.LastName}".Trim(),
            m.TeamId?.ToString() ?? string.Empty,
            [groupName]
        )).ToList();
    }

    public async Task<TeamInfo?> GetTeamByIdAsync(string teamId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(teamId, out var teamGuid))
            return null;

        using var connection = _connectionFactory.GetOpenConnection();

        // 1. Check auth.Teams first (internal teams)
        var teamResult = await connection.QueryFirstOrDefaultAsync<TeamRow>(
            """
            SELECT Id, Name, Type, IsActive
            FROM auth.Teams
            WHERE Id = @Id AND IsActive = 1
            """,
            new { Id = teamGuid });

        if (teamResult is not null)
        {
            return new TeamInfo(
                teamResult.Id.ToString(),
                teamResult.Name,
                teamResult.Type == "Internal" ? TeamType.Internal : TeamType.External,
                teamResult.IsActive);
        }

        // 2. Fall back to auth.Companies
        var companyResult = await connection.QueryFirstOrDefaultAsync<CompanyRow>(
            """
            SELECT Id, Name, IsActive
            FROM auth.Companies
            WHERE Id = @Id AND IsDeleted = 0
            """,
            new { Id = teamGuid });

        if (companyResult is null)
            return null;

        return new TeamInfo(
            companyResult.Id.ToString(),
            companyResult.Name,
            TeamType.External,
            companyResult.IsActive);
    }

    // Dapper row types
    private sealed class TeamRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    private sealed class CompanyRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    private sealed class UserRow
    {
        public string UserName { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
    }

    private sealed class UserWithTeamRow
    {
        public string UserName { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public Guid? TeamId { get; init; }
    }
}
