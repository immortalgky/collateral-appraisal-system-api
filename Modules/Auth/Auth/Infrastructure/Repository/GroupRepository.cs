using Auth.Domain.Groups;
using Shared.Pagination;

namespace Auth.Infrastructure.Repository;

public class GroupRepository(AuthDbContext dbContext) : IGroupRepository
{
    public async Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Groups
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<Group?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Groups
            .Include(g => g.Users)
            .Include(g => g.MonitoredGroups)
                .ThenInclude(mg => mg.MonitoredGroup)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<PaginatedResult<Group>> GetPaginatedAsync(
        string? search,
        string? scope,
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Groups.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.Name.Contains(search) || g.Description.Contains(search));

        if (!string.IsNullOrWhiteSpace(scope))
            query = query.Where(g => g.Scope == scope);

        return await query
            .OrderBy(g => g.Name)
            .ToPaginatedResultAsync(paginationRequest, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Groups.AnyAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, string scope, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await dbContext.Groups.AnyAsync(
            g => g.Name == name && g.Scope == scope && (excludeId == null || g.Id != excludeId),
            cancellationToken);
    }

    public async Task AddAsync(Group group, CancellationToken cancellationToken = default)
    {
        await dbContext.Groups.AddAsync(group, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
