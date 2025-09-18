using Microsoft.EntityFrameworkCore.Storage;
using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public interface IOutboxRepository : IRepository<OutboxMessage, Guid>
{
    Task<IDbContextTransaction> BeginTransaction(CancellationToken cancellationToken = default);
    Task SaveChangeAsync(CancellationToken cancellationToken = default);
}