using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public class InboxReadRepository<TDbContext> : BaseReadRepository<InboxMessage, Guid> where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public InboxReadRepository(TDbContext dbContext) : base(dbContext)
    {
    }
}