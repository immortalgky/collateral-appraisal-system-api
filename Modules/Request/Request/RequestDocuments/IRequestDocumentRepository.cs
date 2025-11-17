using System;

namespace Request.RequestDocuments;

public interface IRequestDocumentRepository
{
    Task<RequestDocument> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(RequestDocument requestDocument, CancellationToken cancellationToken = default);
    Task ClearAsync(Guid requestId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
