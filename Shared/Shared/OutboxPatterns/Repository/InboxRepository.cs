using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public class InboxRepository<TDbContext> : BaseRepository<InboxMessage, Guid>, IInboxRepository where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public InboxRepository(TDbContext dbContext) : base(dbContext)
    {
    }
}