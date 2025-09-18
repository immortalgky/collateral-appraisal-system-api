using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public interface IOutboxReadRepository : IReadRepository<OutboxMessage, Guid>
{
    Task<List<OutboxMessage>> GetAllAsync(string schema, CancellationToken cancellationToken = default);
}