using System;
using Request.RequestDocuments;

namespace Request.Data.Repository;

public class RequestDocumentRepository(RequestDbContext dbContext) : IRequestDocumentRepository
{
    public async Task AddAsync(RequestDocument requestDocument, CancellationToken cancellationToken = default)
    {
        await dbContext.RequestDocuments.AddAsync(requestDocument, cancellationToken);
    }

    public Task<RequestDocument> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task ClearAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var docs = dbContext.RequestDocuments.Where(x => x.RequestId == requestId);
        dbContext.RequestDocuments.RemoveRange(docs);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
