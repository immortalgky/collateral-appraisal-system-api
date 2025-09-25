using Shared.Data;
using Shared.Data.Models;

namespace Shared.Messaging.OutboxPatterns.Repository;

public interface IInboxReadRepository : IReadRepository<InboxMessage, Guid>
{
    Task<InboxMessage> GetMessageByIdAsync(Guid id,CancellationToken cancellationToken);
}