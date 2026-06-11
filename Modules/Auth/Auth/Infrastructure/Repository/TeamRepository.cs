using Auth.Domain.Teams;
using Shared.Pagination;

namespace Auth.Infrastructure.Repository;

public class TeamRepository(AuthDbContext dbContext) : ITeamRepository
{
    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Teams
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Team?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<PaginatedResult<Team>> GetPaginatedAsync(
        string? search,
        string? scope,
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Teams
            .Include(t => t.Members)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search));

        if (!string.IsNullOrWhiteSpace(scope))
            query = query.Where(t => t.Scope == scope);

        return await query
            .OrderBy(t => t.Name)
            .ToPaginatedResultAsync(paginationRequest, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Teams.AnyAsync(t => t.Id == id, cancellationToken);
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        await dbContext.Teams.AddAsync(team, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
