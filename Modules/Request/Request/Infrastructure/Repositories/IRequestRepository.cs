namespace Request.Infrastructure.Repositories;

public interface IRequestRepository : IRepository<Domain.Requests.Request, Guid>
{
    /// <summary>
    /// Gets a request by ID with its documents loaded.
    /// </summary>
    Task<Domain.Requests.Request?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a request by ID with all related data loaded (documents, customers, properties).
    /// Note: Titles are now a separate aggregate and should be loaded via IRequestTitleRepository.
    /// </summary>
    Task<Domain.Requests.Request?> GetByIdWithAllDataAsync(Guid id, CancellationToken cancellationToken = default);
}
