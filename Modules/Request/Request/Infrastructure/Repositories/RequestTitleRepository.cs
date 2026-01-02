using Request.Domain.RequestTitles;

namespace Request.Infrastructure.Repositories;

public class RequestTitleRepository(RequestDbContext dbContext)
    : BaseRepository<RequestTitle, Guid>(dbContext), IRequestTitleRepository
{
    private readonly RequestDbContext _dbContext = dbContext;

    public async Task<IEnumerable<RequestTitle>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RequestTitles
            .Where(t => t.RequestId == requestId)
            .ToListAsync(cancellationToken);
    }

    public async Task<RequestTitle?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RequestTitles
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<RequestTitle>> GetByRequestIdWithDocumentsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RequestTitles
            .Include(t => t.Documents)
            .Where(t => t.RequestId == requestId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RequestTitles
            .AnyAsync(t => t.RequestId == requestId, cancellationToken);
    }
}
