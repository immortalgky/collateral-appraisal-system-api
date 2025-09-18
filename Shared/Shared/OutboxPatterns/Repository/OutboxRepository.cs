using Shared.Data;
using Shared.OutboxPatterns.Models;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace Shared.OutboxPatterns.Repository;

public class OutboxRepository<TDbContext> : BaseRepository<OutboxMessage, Guid>, IOutboxRepository where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public OutboxRepository(TDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IDbContextTransaction> BeginTransaction(CancellationToken cancellationToken = default)
    {
        return await Context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task SaveChangeAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken);
    }
}