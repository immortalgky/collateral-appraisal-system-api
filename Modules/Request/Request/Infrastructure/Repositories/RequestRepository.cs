namespace Request.Infrastructure.Repositories;

public class RequestRepository(RequestDbContext dbContext)
    : BaseRepository<Domain.Requests.Request, Guid>(dbContext), IRequestRepository
{
    private readonly RequestDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Domain.Requests.Request?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Requests
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Requests.Request?> GetByIdWithAllDataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Requests
            .Include(r => r.Documents)
            .Include(r => r.Customers)
            .Include(r => r.Properties)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }
}
