namespace Request.Data.Repository;

public interface IRequestTitleDocumentRepository
{
    Task Remove(RequestTitleDocument requestTitleDocument);
    
    Task SaveChangeAsync(CancellationToken cancellationToken);
    
    Task<RequestTitleDocument> GetRequestTitleDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
}