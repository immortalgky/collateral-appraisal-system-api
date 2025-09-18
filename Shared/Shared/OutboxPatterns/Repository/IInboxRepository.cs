using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public interface IInboxRepository : IRepository<InboxMessage, Guid> {}