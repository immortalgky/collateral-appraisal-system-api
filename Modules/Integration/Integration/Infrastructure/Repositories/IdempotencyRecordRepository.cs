using Integration.Domain.IdempotencyRecords;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Integration.Infrastructure.Repositories;

public interface IIdempotencyRecordRepository : IRepository<IdempotencyRecord, Guid>
{
    Task<IdempotencyRecord?> GetByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}

public class IdempotencyRecordRepository(IntegrationDbContext dbContext)
    : BaseRepository<IdempotencyRecord, Guid>(dbContext), IIdempotencyRecordRepository
{
    private readonly IntegrationDbContext _dbContext = dbContext;

    public async Task<IdempotencyRecord?> GetByKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }
}
