namespace Shared.Messaging.OutboxPatterns.Repository;

public interface IOutboxReadRepository : IReadRepository<OutboxMessage, Guid>
{
    Task<List<OutboxMessage>> GetMessageAsync(string schema, CancellationToken cancellationToken = default);
}