using Auth.Domain.Teams;
using Shared.Pagination;

namespace Auth.Infrastructure.Repository;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Team?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Team>> GetPaginatedAsync(string? search, string? type, PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
