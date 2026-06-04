using Auth.Domain.Auditing;
using Auth.Domain.Teams;
using Auth.Infrastructure;
using Auth.Infrastructure.Repository;
using Shared.Pagination;

namespace Auth.Application.Services;

public class TeamService(
    ITeamRepository teamRepository,
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter) : ITeamService
{
    public async Task<Team> CreateTeam(string name, string type, bool isActive, CancellationToken cancellationToken = default)
    {
        var team = Team.Create(name, type, isActive);
        await teamRepository.AddAsync(team, cancellationToken);
        auditWriter.Record(AuditAction.Created, AuditEntityType.Team, team.Id, name);
        await teamRepository.SaveChangesAsync(cancellationToken);
        return team;
    }

    public async Task<PaginatedResult<Team>> GetTeams(string? search, string? type, PaginationRequest paginationRequest, CancellationToken cancellationToken = default)
    {
        return await teamRepository.GetPaginatedAsync(search, type, paginationRequest, cancellationToken);
    }

    public async Task<Team?> GetTeamById(Guid id, CancellationToken cancellationToken = default)
    {
        return await teamRepository.GetByIdWithDetailsAsync(id, cancellationToken);
    }

    public async Task UpdateTeam(Guid id, string name, string type, bool isActive, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Team {id} not found.");

        team.Update(name, type, isActive);
        auditWriter.Record(AuditAction.Updated, AuditEntityType.Team, id, name);
        await teamRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTeamMembers(Guid id, List<Guid> memberUserIds, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Team {id} not found.");

        var beforeIds = team.Members.Select(m => m.UserId).ToList();
        var afterIds = memberUserIds.Distinct().ToList();

        var existing = dbContext.TeamMembers.Where(m => m.TeamId == id);
        dbContext.TeamMembers.RemoveRange(existing);

        foreach (var userId in afterIds)
            dbContext.TeamMembers.Add(new TeamMember { TeamId = id, UserId = userId });

        auditWriter.RecordAssignmentChange(AuditEntityType.Team, id, team.Name, beforeIds, afterIds, "members");
        await teamRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTeam(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Team {id} not found.");

        // Hard delete: remove members first (cascade would handle it, but explicit is clearer)
        var members = dbContext.TeamMembers.Where(m => m.TeamId == id);
        dbContext.TeamMembers.RemoveRange(members);
        dbContext.Teams.Remove(team);

        auditWriter.Record(AuditAction.Deleted, AuditEntityType.Team, id, team.Name);
        await teamRepository.SaveChangesAsync(cancellationToken);
    }
}
