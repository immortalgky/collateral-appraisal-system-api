using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public interface IOutboxReadRepository : IReadRepository<OutboxMessage, Guid>
{
    Task<List<OutboxMessage>> GetMessageAsync(string schema, CancellationToken cancellationToken = default);
}