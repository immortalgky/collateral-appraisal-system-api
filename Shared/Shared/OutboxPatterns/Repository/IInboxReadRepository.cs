using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public interface IInboxReadRepository : IReadRepository<InboxMessage, Guid> { }