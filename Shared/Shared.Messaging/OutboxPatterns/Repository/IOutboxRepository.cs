namespace Shared.Messaging.OutboxPatterns.Repository;

public interface IOutboxRepository : IRepository<OutboxMessage, Guid>
{
    Task<IDbContextTransaction> BeginTransaction(CancellationToken cancellationToken = default);
    Task SaveChangeAsync(CancellationToken cancellationToken = default);
}