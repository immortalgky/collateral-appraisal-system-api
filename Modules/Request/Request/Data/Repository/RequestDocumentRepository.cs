using Request.RequestDocuments;

namespace Request.Data.Repository;

public class RequestDocumentRepository(RequestDbContext dbContext) : IRequestDocumentRepository
{
    public async Task AddAsync(RequestDocument requestDocument, CancellationToken cancellationToken = default)
    {
        await dbContext.RequestDocuments.AddAsync(requestDocument, cancellationToken);
    }

    public async Task<List<RequestDocument>> GetByRequestIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var documents = await dbContext.RequestDocuments
            .Where(x => x.RequestId == id)
            .ToListAsync(cancellationToken);
        return documents;
    }

    public async Task<RequestDocument> GetDocByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.RequestDocuments.FindAsync(id, cancellationToken);
        return document;
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var requestDoc = await GetDocByIdAsync(id, cancellationToken);
        dbContext.RequestDocuments.Remove(requestDoc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
