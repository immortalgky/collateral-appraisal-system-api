using Auth.Domain.Teams;
using Shared.Pagination;

namespace Auth.Application.Services;

public interface ITeamService
{
    Task<Team> CreateTeam(string name, string scope, string? description = null, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Team>> GetTeams(string? search, string? scope, PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
    Task<Team?> GetTeamById(Guid id, CancellationToken cancellationToken = default);
    Task UpdateTeam(Guid id, string name, string scope, string? description = null, CancellationToken cancellationToken = default);
    Task UpdateTeamMembers(Guid id, List<Guid> memberUserIds, CancellationToken cancellationToken = default);
    Task DeleteTeam(Guid id, CancellationToken cancellationToken = default);
}
