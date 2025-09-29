namespace Shared.Messaging.OutboxPatterns.Repository;

public interface IInboxRepository : IRepository<InboxMessage, Guid>
{
    Task<IDbContextTransaction> BeginTransaction(CancellationToken cancellationToken = default);
    Task SaveChangeAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteMessageTimeout(CancellationToken cancellationToken);

}