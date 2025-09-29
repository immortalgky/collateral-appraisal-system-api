namespace Shared.Messaging.OutboxPatterns.Repository;

public interface IOutboxReadRepository : IReadRepository<OutboxMessage, Guid>
{
    Task<List<OutboxMessage>> GetMessageAsync(CancellationToken cancellationToken = default);
}