namespace Request.Data.Repository;

public interface IRequestTitleDocumentReadRepository 
{
    Task<List<RequestTitleDocument>> GetRequestTitleDocumentsByTitleIdAsync(Guid titleId, CancellationToken cancellationToken = default);
    
    Task<RequestTitleDocument> GetRequestTitleDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
}