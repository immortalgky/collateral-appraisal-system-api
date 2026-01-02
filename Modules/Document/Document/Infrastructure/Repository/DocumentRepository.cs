using System.Linq.Expressions;
using Document.Domain.Documents;
using Document.Domain.Documents.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Document.Data.Repository;

public class DocumentRepository(DocumentDbContext dbContext)
    : BaseRepository<Domain.Documents.Models.Document, Guid>(dbContext), IDocumentRepository
{
    public async Task<IEnumerable<Domain.Documents.Models.Document>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Documents.ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Expression<Func<Domain.Documents.Models.Document, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await dbContext.Documents.AnyAsync(predicate, cancellationToken);
    }
}