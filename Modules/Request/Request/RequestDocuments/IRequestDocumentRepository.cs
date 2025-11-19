using System;

namespace Request.RequestDocuments;

public interface IRequestDocumentRepository
{
    Task<List<RequestDocument>> GetByRequestIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RequestDocument> GetDocByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(RequestDocument requestDocument, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
